#include "settings/options_row_sink.h"

namespace topspin4::settings::options_row {

namespace {
RowSink g_sink{};
}

RowSink& GetRowSink() { return g_sink; }

}  // namespace topspin4::settings::options_row
