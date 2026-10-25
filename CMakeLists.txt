# Top Spin 4 Recomp. SDK boilerplate lives in cmake/rexglue.cmake.

cmake_minimum_required(VERSION 3.25)
project(topspin4 VERSION 0.1.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(cmake/rexglue.cmake)

set(TOPSPIN4_SOURCES
    src/main.cpp
    src/crash_report.cpp
    src/fault_log.cpp
    src/frame_dumps.cpp
    src/remapped_input.cpp
    src/script_input.cpp
    src/settings/settings_cvars.cpp
    src/settings/settings_file.cpp
    src/user_paths.cpp
)

if(WIN32)
    add_executable(topspin4 WIN32 ${TOPSPIN4_SOURCES})
else()
    add_executable(topspin4 ${TOPSPIN4_SOURCES})
endif()

rexglue_setup_target(topspin4 GPU_PLUGINS xenos)
# Crash report (src/crash_report.cpp): the version and commit go in its manifest.
execute_process(COMMAND git rev-parse --short HEAD WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
    OUTPUT_VARIABLE TOPSPIN4_COMMIT OUTPUT_STRIP_TRAILING_WHITESPACE ERROR_QUIET)
if(NOT TOPSPIN4_COMMIT)
    set(TOPSPIN4_COMMIT "unknown")
endif()
target_compile_definitions(topspin4 PRIVATE
    TOPSPIN4_VERSION="${PROJECT_VERSION}" TOPSPIN4_COMMIT="${TOPSPIN4_COMMIT}")
if(WIN32)
    target_link_libraries(topspin4 PRIVATE dbghelp shell32)
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

# Host unit tests of pure host code (scripts/build.ps1 -Action Test): Catch2 from the SDK tree,
# opt-in and outside the default build, as in WWE All Stars.
option(TOPSPIN4_BUILD_TESTS "Build the host unit tests" OFF)
if(TOPSPIN4_BUILD_TESTS AND REXSDK_DIR)
    enable_testing()
    add_executable(topspin4_host_tests EXCLUDE_FROM_ALL
        tests/host/test_frame_dumps.cpp
        tests/host/test_user_paths.cpp
        tests/host/test_settings.cpp
        tests/host/test_crash_report.cpp
        tests/host/test_pad_bindings.cpp
        src/settings/settings_file.cpp
        ${REXSDK_DIR}/thirdparty/catch2/extras/catch_amalgamated.cpp)
    target_include_directories(topspin4_host_tests PRIVATE src
        ${REXSDK_DIR}/thirdparty/catch2/extras)
    target_link_libraries(topspin4_host_tests PRIVATE rex::runtime)
    add_test(NAME topspin4_host_tests COMMAND topspin4_host_tests)
endif()

if(NOT REXGLUE_ENTRYPOINT_GENERATED_SOURCES)
    # No codegen output yet: the host starts, says so and exits (ticket 03).
    target_compile_definitions(topspin4 PRIVATE TOPSPIN4_NO_GUEST_CODE)
endif()

# The v0.10.0 source-tree runtime does not propagate ImGui's include directory
# to the host, although its public UI headers include imgui.h.
if(REXSDK_DIR)
    target_include_directories(topspin4 PRIVATE
        $<TARGET_PROPERTY:imgui::imgui,INTERFACE_INCLUDE_DIRECTORIES>)
    # The crash report reads the GPU name through the SDK's Vulkan headers, which include
    # RenderDoc's; the settings file is read with toml++ (as in WWE All Stars); the remapped
    # controller driver wraps the SDK's SDL one, whose header includes SDL3's.
    target_include_directories(topspin4 PRIVATE ${REXSDK_DIR}/thirdparty/renderdoc
        ${REXSDK_DIR}/thirdparty/tomlplusplus/include ${REXSDK_DIR}/thirdparty/sdl3/include)
endif()
