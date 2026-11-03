#ifdef __ANDROID__
#include <android/native_activity.h>

extern "C" void android_main(struct android_app* app) {
    (void)app;
}
#endif
