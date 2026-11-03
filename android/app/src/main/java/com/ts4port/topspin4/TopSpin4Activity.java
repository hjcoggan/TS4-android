package com.ts4port.topspin4;

import android.content.Context;
import android.os.Build;
import android.os.Bundle;
import android.os.PowerManager;
import android.system.ErrnoException;
import android.system.Os;
import android.util.Log;
import android.view.View;
import android.view.WindowManager;

import org.libsdl.app.SDLActivity;

import java.io.BufferedInputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.Locale;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/**
 * Entry point. SDL3 is built statically into the SDK runtime (librexruntimed.so;
 * thirdparty/CMakeLists.txt forces SDL_SHARED=OFF), so there is no separate
 * libSDL3.so: SDL's JNI_OnLoad is in the runtime, and SDL_main is in libtopspin4.so.
 *
 * <p>Prepares the game-data folder and environment variables before SDL loads
 * the native library, enables immersive fullscreen / keep-screen-on, and
 * verifies that the pushed game data is present (native-free error screen
 * otherwise).</p>
 */
public class TopSpin4Activity extends SDLActivity {
    private static final String TAG = "TopSpin4";

    private File mDataRoot;
    private boolean mDataMissing;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        File external = getExternalFilesDir(null);
        File dataRoot = external != null ? external : getFilesDir();
        mDataRoot = dataRoot;

        File defaultXex = new File(new File(dataRoot, "gamedata"), "default.xex");
        mDataMissing = !defaultXex.isFile();

        if (!mDataMissing) {
            prepareEnvironment(dataRoot);
        }

        super.onCreate(savedInstanceState);

        if (mDataMissing) {
            showMissingDataScreen(dataRoot);
            return;
        }

        // Keep the screen on while playing.
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);

        // Sustained performance mode where the device supports it.
        PowerManager pm = (PowerManager) getSystemService(Context.POWER_SERVICE);
        if (pm != null && pm.isSustainedPerformanceModeSupported()) {
            getWindow().setSustainedPerformanceMode(true);
        }

        hideSystemBars();
    }

    private void prepareEnvironment(File dataRoot) {
        // Ensure the expected sub-folders exist.
        for (String name : new String[] { "gamedata", "updatedata", "userdata", "cache", "drivers" }) {
            File dir = new File(dataRoot, name);
            if (!dir.isDirectory()) {
                dir.mkdirs();
            }
        }

        setenv("XDG_DATA_HOME", dataRoot.getAbsolutePath());
        setenv("HOME", dataRoot.getAbsolutePath());
        // Base folder for the native runtime (REX_APP_FOLDER + REX_EXECUTABLE_FOLDER).
        setenv("REX_APP_FOLDER", dataRoot.getAbsolutePath());
        setenv("REX_EXECUTABLE_FOLDER", dataRoot.getAbsolutePath());

        // Optional custom Vulkan driver: drop an ADPKG into files/drivers/.
        configureGpuDriver(dataRoot);

        // Display options (aspect / refresh), read from files/userdata/display.conf.
        configureDisplay(dataRoot);
    }

    /**
     * Display options, read from {@code files/userdata/display.conf} so they can be changed
     * without a rebuild. They drive SDK cvars through the environment (REX_VIDEO_MODE_*), which
     * outrank the Settings file and are never saved into it.
     *
     * <pre>
     *   width=1280      # guest video mode width
     *   height=960      # guest video mode height (960 = 4:3 on a 1280-wide screen)
     *   refresh=120     # guest video mode refresh rate in Hz
     *   aspect=4:3      # convenience: derives height from width
     * </pre>
     *
     * A missing file leaves the SDK defaults (1280x720 at 60 Hz).
     */
    private void configureDisplay(File dataRoot) {
        File config = new File(new File(dataRoot, "userdata"), "display.conf");
        int width = 0;
        int height = 0;
        float refresh = 0f;
        if (config.isFile()) {
            try (java.io.BufferedReader reader = new java.io.BufferedReader(
                         new java.io.FileReader(config))) {
                String raw;
                while ((raw = reader.readLine()) != null) {
                    String line = raw.trim();
                    if (line.isEmpty() || line.startsWith("#") || line.startsWith(";")) {
                        continue;
                    }
                    int eq = line.indexOf('=');
                    if (eq < 0) {
                        continue;
                    }
                    String key = line.substring(0, eq).trim().toLowerCase(java.util.Locale.ROOT);
                    String value = line.substring(eq + 1).trim();
                    try {
                        switch (key) {
                            case "width":
                                width = Integer.parseInt(value);
                                break;
                            case "height":
                                height = Integer.parseInt(value);
                                break;
                            case "refresh":
                                refresh = Float.parseFloat(value);
                                break;
                            case "aspect":
                                // Only 4:3 and 16:9 matter here; the width is the screen's.
                                if (value.equals("4:3") && width > 0) {
                                    height = width * 3 / 4;
                                } else if (value.equals("16:9") && width > 0) {
                                    height = width * 9 / 16;
                                }
                                break;
                            default:
                                Log.w(TAG, "display.conf: unknown key " + key);
                                break;
                        }
                    } catch (NumberFormatException e) {
                        Log.w(TAG, "display.conf: bad value for " + key + ": " + value);
                    }
                }
            } catch (java.io.IOException e) {
                Log.w(TAG, "display.conf: cannot read " + config, e);
            }
        }
        if (width > 0) {
            setenv("REX_VIDEO_MODE_WIDTH", Integer.toString(width));
        }
        if (height > 0) {
            setenv("REX_VIDEO_MODE_HEIGHT", Integer.toString(height));
        }
        if (refresh > 0) {
            setenv("REX_VIDEO_MODE_REFRESH_RATE", Float.toString(refresh));
            // Ask the Android window for the same refresh rate; the surface (and therefore the
            // Vulkan swapchain) follows it. Requires the panel to advertise the mode.
            try {
                android.view.WindowManager.LayoutParams attributes = getWindow().getAttributes();
                attributes.preferredRefreshRate = refresh;
                getWindow().setAttributes(attributes);
            } catch (RuntimeException e) {
                Log.w(TAG, "display: cannot request " + refresh + " Hz", e);
            }
        }
        Log.i(TAG, "display: width=" + width + " height=" + height + " refresh=" + refresh);
    }

    /**
     * Extracts an ADPKG (a zip: meta.json + the driver .so) from the external
     * files directory into app-internal storage and exports the paths the SDK's
     * libadrenotools wrapper reads (REX_NATIVE_LIB_DIR, REX_GPU_DRIVER_TMP,
     * REX_GPU_DRIVER_DIR, REX_GPU_DRIVER_NAME).
     *
     * <p>adrenotools refuses to dlopen a driver from external storage, so the
     * extraction target is {@code getFilesDir()/drivers/&lt;name&gt;/} even though
     * the package itself is pushed under {@code Android/data/.../files/drivers/}.
     * When no package is present nothing is set and the SDK falls back to
     * {@code dlopen("libvulkan.so")}: the system driver.</p>
     */
    private void configureGpuDriver(File dataRoot) {
        File driversDir = new File(dataRoot, "drivers");
        // A/B switch: `files/drivers/disable` (any contents) forces the system
        // Vulkan loader without touching the package or reinstalling the APK.
        if (new File(driversDir, "disable").exists()) {
            Log.i(TAG, "custom GPU driver: disabled by " + new File(driversDir, "disable")
                    + "; using the system vulkan loader");
            return;
        }

        File pkg = findDriverPackage(driversDir);
        if (pkg == null) {
            Log.i(TAG, "custom GPU driver: none; using the system vulkan loader");
            return;
        }

        String meta;
        try {
            meta = readZipEntry(pkg, "meta.json");
        } catch (IOException e) {
            Log.e(TAG, "custom GPU driver: cannot read " + pkg, e);
            return;
        }
        if (meta == null) {
            Log.e(TAG, "custom GPU driver: " + pkg + " has no meta.json");
            return;
        }

        String libraryName = jsonString(meta, "libraryName");
        String name = jsonString(meta, "name");
        String packageVersion = jsonString(meta, "packageVersion");
        String driverVersion = jsonString(meta, "driverVersion");
        String vendor = jsonString(meta, "vendor");
        int minApi = jsonInt(meta, "minApi", 0);
        if (libraryName == null || libraryName.isEmpty()) {
            Log.e(TAG, "custom GPU driver: meta.json has no libraryName");
            return;
        }
        if (minApi > Build.VERSION.SDK_INT) {
            Log.w(TAG, "custom GPU driver: " + name + " needs API " + minApi
                    + ", device is API " + Build.VERSION.SDK_INT + "; using the system driver");
            return;
        }

        File driversRoot = new File(getFilesDir(), "drivers");
        File target = new File(driversRoot, "turnip");
        String stamp = pkg.length() + ":" + pkg.lastModified() + ":" + libraryName;
        File stampFile = new File(target, ".package-stamp");

        boolean extracted = new File(target, libraryName).isFile()
                && readText(stampFile).equals(stamp);
        if (extracted) {
            Log.i(TAG, "custom GPU driver: already extracted " + target);
        } else {
            try {
                extractPackage(pkg, target);
                writeText(stampFile, stamp);
                extracted = true;
                Log.i(TAG, "custom GPU driver: extracted " + pkg + " -> " + target);
            } catch (IOException e) {
                Log.e(TAG, "custom GPU driver: extraction failed", e);
            }
        }
        if (!extracted || !new File(target, libraryName).isFile()) {
            Log.e(TAG, "custom GPU driver: " + libraryName + " not present; using the system driver");
            return;
        }

        setenv("REX_NATIVE_LIB_DIR", getApplicationInfo().nativeLibraryDir);
        setenv("REX_GPU_DRIVER_TMP", getCacheDir().getAbsolutePath());
        setenv("REX_GPU_DRIVER_DIR", target.getAbsolutePath());
        setenv("REX_GPU_DRIVER_NAME", libraryName);
        Log.i(TAG, "custom GPU driver: " + name + " [" + vendor + " " + driverVersion
                + ", package " + packageVersion + "] library=" + libraryName);
    }

    /** The first {@code turnip.adpkg}, else any {@code *.adpkg}, else any {@code *.zip}. */
    private File findDriverPackage(File dir) {
        if (!dir.isDirectory()) {
            return null;
        }
        File fallback = null;
        File[] files = dir.listFiles();
        if (files == null) {
            return null;
        }
        Arrays.sort(files);
        for (File f : files) {
            if (!f.isFile()) {
                continue;
            }
            String lower = f.getName().toLowerCase(Locale.ROOT);
            if (lower.equals("turnip.adpkg")) {
                return f;
            }
            if (fallback == null && (lower.endsWith(".adpkg") || lower.endsWith(".zip"))) {
                fallback = f;
            }
        }
        return fallback;
    }

    private static String readZipEntry(File zip, String wanted) throws IOException {
        try (ZipInputStream in = new ZipInputStream(new BufferedInputStream(new FileInputStream(zip)))) {
            ZipEntry entry;
            while ((entry = in.getNextEntry()) != null) {
                if (!entry.isDirectory() && wanted.equals(entry.getName())) {
                    return readAll(in);
                }
            }
        }
        return null;
    }

    private static void extractPackage(File zip, File target) throws IOException {
        String targetPrefix = target.getCanonicalPath() + File.separator;
        deleteRecursively(target);
        if (!target.mkdirs()) {
            throw new IOException("cannot create " + target);
        }
        try (ZipInputStream in = new ZipInputStream(new BufferedInputStream(new FileInputStream(zip)))) {
            byte[] buffer = new byte[64 * 1024];
            ZipEntry entry;
            while ((entry = in.getNextEntry()) != null) {
                String entryName = entry.getName();
                if (entryName.startsWith("/") || entryName.contains("..")) {
                    throw new IOException("unsafe zip entry: " + entryName);
                }
                File out = new File(target, entryName);
                if (!out.getCanonicalPath().startsWith(targetPrefix)) {
                    throw new IOException("zip entry escapes target: " + entryName);
                }
                if (entry.isDirectory()) {
                    if (!out.isDirectory() && !out.mkdirs()) {
                        throw new IOException("cannot create " + out);
                    }
                    continue;
                }
                File parent = out.getParentFile();
                if (parent != null && !parent.isDirectory() && !parent.mkdirs()) {
                    throw new IOException("cannot create " + parent);
                }
                try (FileOutputStream os = new FileOutputStream(out)) {
                    int count;
                    while ((count = in.read(buffer)) > 0) {
                        os.write(buffer, 0, count);
                    }
                }
            }
        }
    }

    private static void deleteRecursively(File file) {
        File[] children = file.listFiles();
        if (children != null) {
            for (File child : children) {
                deleteRecursively(child);
            }
        }
        file.delete();
    }

    /** Minimal single-line JSON string lookup; no JSON dependency needed. */
    private static String jsonString(String json, String key) {
        Matcher m = Pattern.compile("\"" + Pattern.quote(key) + "\"\\s*:\\s*\"([^\"]*)\"")
                .matcher(json);
        return m.find() ? m.group(1) : null;
    }

    private static int jsonInt(String json, String key, int fallback) {
        Matcher m = Pattern.compile("\"" + Pattern.quote(key) + "\"\\s*:\\s*(\\d+)").matcher(json);
        return m.find() ? Integer.parseInt(m.group(1)) : fallback;
    }

    private static String readAll(InputStream in) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = in.read(buffer)) > 0) {
            out.write(buffer, 0, count);
        }
        return out.toString(StandardCharsets.UTF_8.name());
    }

    private static String readText(File file) {
        try {
            byte[] data = new byte[(int) file.length()];
            try (FileInputStream in = new FileInputStream(file)) {
                int offset = 0;
                while (offset < data.length) {
                    int count = in.read(data, offset, data.length - offset);
                    if (count < 0) {
                        break;
                    }
                    offset += count;
                }
            }
            return new String(data, StandardCharsets.UTF_8);
        } catch (IOException e) {
            return "";
        }
    }

    private static void writeText(File file, String text) throws IOException {
        try (FileOutputStream out = new FileOutputStream(file)) {
            out.write(text.getBytes(StandardCharsets.UTF_8));
        }
    }

    private void setenv(String name, String value) {
        try {
            Os.setenv(name, value, true);
        } catch (ErrnoException e) {
            Log.e(TAG, "setenv " + name + " failed", e);
        }
    }

    /**
     * Native-free error screen shown when the pushed game data is absent.
     */
    private void showMissingDataScreen(File dataRoot) {
        android.widget.ScrollView scroll = new android.widget.ScrollView(this);
        android.widget.TextView text = new android.widget.TextView(this);
        text.setText(
                "Top Spin 4 \u2014 game data missing\n\n"
                        + "Expected file:\n"
                        + new File(dataRoot, "gamedata/default.xex").getAbsolutePath()
                        + "\n\nPush prepared data from your Mac:\n\n"
                        + "PKG=com.ts4port.topspin4\n"
                        + "BASE=/sdcard/Android/data/$PKG/files\n"
                        + "adb shell mkdir -p $BASE\n"
                        + "adb push gamedata   $BASE/\n"
                        + "adb push updatedata $BASE/\n"
                        + "adb push userdata   $BASE/\n"
                        + "adb push cache      $BASE/\n\n"
                        + "Then relaunch the app.");
        text.setTextSize(14);
        text.setPadding(32, 32, 32, 32);
        scroll.addView(text);
        setContentView(scroll);
    }

    @Override
    public void loadLibraries() {
        if (mDataMissing) {
            throw new UnsatisfiedLinkError(
                    "Game data missing: "
                            + new File(mDataRoot, "gamedata/default.xex").getAbsolutePath()
                            + "\n\nPush prepared data from your Mac:\n"
                            + "PKG=com.ts4port.topspin4\n"
                            + "BASE=/sdcard/Android/data/$PKG/files\n"
                            + "adb shell mkdir -p $BASE\n"
                            + "adb push gamedata   $BASE/\n"
                            + "adb push updatedata $BASE/\n"
                            + "adb push userdata   $BASE/\n"
                            + "adb push cache      $BASE/\n");
        }
        super.loadLibraries();
    }

    @Override
    protected String[] getLibraries() {
        // SDL3 is static inside the SDK runtime, so JNI_OnLoad is in librexruntimed;
        // SDL_main is in libtopspin4. Release builds name the runtime "rexruntime".
        return new String[] { BuildConfig.REX_RUNTIME_LIBRARY, "topspin4" };
    }

    private void hideSystemBars() {
        View decor = getWindow().getDecorView();
        decor.setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                        | View.SYSTEM_UI_FLAG_FULLSCREEN
                        | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                        | View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                        | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                        | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN);
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus && !mDataMissing) {
            hideSystemBars();
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (!mDataMissing) {
            hideSystemBars();
        }
    }
}
