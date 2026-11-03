// Fills the D3D sink (sink.h) with the exe's handlers: the census (census.h), the shader
// bookkeeping and, when it is active, the Native renderer. Call after native::Configure and
// before guest code runs (the app's OnPostSetup).

#pragma once

#include <filesystem>

namespace topspin4::d3d {

// cache_root: where the shader top-up folder goes (native/shader_topup.h).
void InstallHost(const std::filesystem::path& cache_root);

// Session end (window closed, guest exit or the scene run's exit; any number of times): the
// census's last line and Native's draw-skip summary and report.
void OnSessionEnd();

}  // namespace topspin4::d3d
