package com.ts4port.topspin4;

import android.content.Context;
import android.os.Bundle;
import android.os.PowerManager;
import android.system.ErrnoException;
import android.system.Os;
import android.util.Log;
import android.view.View;
import android.view.WindowManager;

import org.libsdl.app.SDLActivity;

import java.io.File;

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

        // Optional custom Vulkan driver: drop a .zip into files/drivers/.
        File driverZip = findDriverZip(new File(dataRoot, "drivers"));
        if (driverZip != null) {
            setenv("TS4_GPU_DRIVER", driverZip.getAbsolutePath());
            Log.i(TAG, "custom GPU driver: " + driverZip.getAbsolutePath());
        }
    }

    private File findDriverZip(File dir) {
        if (!dir.isDirectory()) {
            return null;
        }
        File[] files = dir.listFiles();
        if (files == null) {
            return null;
        }
        for (File f : files) {
            if (f.isFile() && f.getName().toLowerCase().endsWith(".zip")) {
                return f;
            }
        }
        return null;
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
        return new String[] { "rexruntimed", "topspin4" };
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