// Fills the D3D sink (sink.h) with the exe's handlers: the census (census.h) and the Native
// renderer. Call before guest code runs (the app's OnPostSetup).

#pragma once

namespace topspin4::d3d {

void InstallHost();

// Session end (window closed or the scene run's exit): the census's last line.
void OnSessionEnd();

}  // namespace topspin4::d3d
