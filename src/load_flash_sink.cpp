#include "load_flash.h"

namespace topspin4::load_flash {

namespace {
Sink g_sink{};
}

Sink& GetSink() { return g_sink; }

}  // namespace topspin4::load_flash
