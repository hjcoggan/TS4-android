#include "d3d/host.h"

#include "d3d/census.h"
#include "d3d/sink.h"

namespace topspin4::d3d {
namespace {

void Present(Module module, const uint8_t*, uint32_t, uint32_t) { census::OnPresent(module); }

}  // namespace

void InstallHost() {
  Sink& sink = GetSink();
  if (census::Enabled()) {
    sink.present = &Present;
    sink.mirror_commit = &census::OnMirrorCommit;
  }
}

void OnSessionEnd() { census::LogFinal(); }

}  // namespace topspin4::d3d
