# Top Spin 4 Recomp. SDK boilerplate lives in cmake/rexglue.cmake.

cmake_minimum_required(VERSION 3.25)
project(topspin4 VERSION 0.1.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(cmake/rexglue.cmake)

set(TOPSPIN4_SOURCES
    src/main.cpp
    src/fault_log.cpp
    src/script_input.cpp
)

if(WIN32)
    add_executable(topspin4 WIN32 ${TOPSPIN4_SOURCES})
else()
    add_executable(topspin4 ${TOPSPIN4_SOURCES})
endif()

rexglue_setup_target(topspin4 GPU_PLUGINS xenos)
if(WIN32)
    target_link_libraries(topspin4 PRIVATE dbghelp)
    # The SDK stages its DLLs next to the exe only when the exe relinks, and a runtime
    # change that keeps the exports (or any GPU plugin change) does not relink it, so the
    # run would use stale DLLs. Copy them on every build.
    add_custom_target(topspin4_stage_dlls ALL
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            $<TARGET_RUNTIME_DLLS:topspin4> $<TARGET_FILE:rexgpu-xenos> $<TARGET_FILE_DIR:topspin4>
        COMMAND_EXPAND_LISTS
        VERBATIM)
    add_dependencies(topspin4_stage_dlls topspin4)
endif()

# The SDK's -ffp-model=strict does not reach these targets (it is scoped to the SDK's
# directory), so contraction is off explicitly: a PPC fmul followed by fadd never fuses.
foreach(topspin4_target topspin4 topspin4_recomp topspin4_Loader_DLL topspin4_Swing_DLL)
    if(TARGET ${topspin4_target})
        target_compile_options(${topspin4_target} PRIVATE -ffp-contract=off)
    endif()
endforeach()

if(NOT REXGLUE_ENTRYPOINT_GENERATED_SOURCES)
    # No codegen output yet: the host starts, says so and exits (ticket 03).
    target_compile_definitions(topspin4 PRIVATE TOPSPIN4_NO_GUEST_CODE)
endif()

# The v0.10.0 source-tree runtime does not propagate ImGui's include directory
# to the host, although its public UI headers include imgui.h.
if(REXSDK_DIR)
    target_include_directories(topspin4 PRIVATE
        $<TARGET_PROPERTY:imgui::imgui,INTERFACE_INCLUDE_DIRECTORIES>)
endif()
