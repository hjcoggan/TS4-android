#include "settings/main_menu_sink.h"

namespace topspin4::settings::main_menu {

namespace {
MenuSink g_sink{};
}

MenuSink& GetMenuSink() { return g_sink; }

}  // namespace topspin4::settings::main_menu
