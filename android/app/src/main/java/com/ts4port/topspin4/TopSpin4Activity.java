package com.ts4port.topspin4;

import org.libsdl.app.SDLActivity;

/**
 * Entry point. SDL3 is built statically into libtopspin4.so by the ReXGlue SDK
 * (thirdparty/CMakeLists.txt forces SDL_SHARED=OFF), so there is no separate
 * libSDL3.so to load: the SDL JNI_OnLoad lives inside libtopspin4.so.
 */
public class TopSpin4Activity extends SDLActivity {
    @Override
    protected String[] getLibraries() {
        return new String[] { "topspin4" };
    }
}
