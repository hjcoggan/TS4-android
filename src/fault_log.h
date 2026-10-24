#pragma once

namespace topspin4::fault_log {

// Logs a symbolized host stack for the first few access violations the runtime does not
// handle (it runs last among the vectored handlers), then lets the search go on. A
// diagnostic for codegen and hook work; crash reports (ticket 11) build on it.
void Install();

}  // namespace topspin4::fault_log
