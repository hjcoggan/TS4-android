#include "cutaway_rate.h"

namespace topspin4::cutaway_rate {

namespace {
Sink g_sink{};
}

Sink& GetSink() { return g_sink; }

}  // namespace topspin4::cutaway_rate
