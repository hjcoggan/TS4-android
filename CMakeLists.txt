# Top Spin 4 port (Android arm64-v8a + host dev builds).
#
# The app layer is the Windows reference's src/ (ts4-port/src, copied from
# _ref/Top-Spin-4-Recomp) with the platform shim from docs/windows-api-audit.md in front of it:
# src/platform/{paths,dialog,thread,audio,fonts,crash}*.cpp. Shared code includes no windows.h.
#
# SDK boilerplate mirrors the reference's cmake/rexglue.cmake, adapted; see docs/app-port.md for
# what is ported, stubbed and left.

cmake_minimum_required(VERSION 3.25)
project(topspin4 VERSION 1.0.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

option(TS4_WITH_REXGLUE "Enable ReXGlue SDK integration" OFF)
option(TOPSPIN4_DEVELOPER_BUILD "Developer build: keep the F6 cvar overlay" ON)

if(TS4_WITH_REXGLUE)
    include(cmake/rexglue.cmake)
endif()

# Generated codegen sources, only if codegen has been run. The build must work without them:
# main.cpp then links NoGuestCodeApp (TOPSPIN4_NO_GUEST_CODE) and no guest code is expected.
set(TOPSPIN4_GENERATED_SOURCES "")
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot/sources.cmake")
    if(TS4_WITH_REXGLUE AND DEFINED REXGLUE_ENTRYPOINT_GENERATED_SOURCES)
        set(TOPSPIN4_GENERATED_SOURCES ${REXGLUE_ENTRYPOINT_GENERATED_SOURCES})
    else()
        include(generated/boot/sources.cmake)
        set(TOPSPIN4_GENERATED_SOURCES ${GENERATED_SOURCES})
    endif()
endif()

# The app layer, as in the reference's CMakeLists.txt. Windows-only translation units
# (crash_report.cpp, topspin4.rc) are added below.
set(TOPSPIN4_SOURCES
    src/main.cpp
    src/achievement_toast.cpp
    src/not_set_up.cpp
    src/d3d/census.cpp
    src/d3d/host.cpp
    src/fault_log.cpp
    src/frame_dumps.cpp
    src/load_flash_host.cpp
    src/cutaway_rate_host.cpp
    src/module_shaders.cpp
    src/native/geometry_cache.cpp
    src/native/live_topup.cpp
    src/native/native_renderer.cpp
    src/native/pipelines.cpp
    src/native/pipeline_parts.cpp
    src/native/pipeline_store.cpp
    src/native/prebuild_plan.cpp
    src/native/prepare_output.cpp
    src/native/shader_library.cpp
    src/native/shader_pack.cpp
    src/native/shader_topup.cpp
    src/native/texture_layout.cpp
    src/native/textures.cpp
    src/native/topup_converter.cpp
    src/native/vk_context.cpp
    src/native/write_watch.cpp
    src/relaunch.cpp
    src/remapped_input.cpp
    src/script_input.cpp
    src/settings/schema_export.cpp
    src/settings/settings_cvars.cpp
    src/settings/settings_file.cpp
    src/settings/settings_page.cpp
    src/user_paths.cpp
)

# The platform shim (docs/windows-api-audit.md). Android and the macOS/Linux host use the POSIX
# files; Windows keeps its own implementations so the reference's code still compiles there.
if(WIN32)
    set(TOPSPIN4_PLATFORM_SOURCES
        src/platform/paths_win32.cpp
        src/platform/dialog_win32.cpp
        src/platform/thread_win32.cpp
        src/platform/audio_win32.cpp
        src/platform/fonts_win32.cpp
        src/platform/crash_win32.cpp
    )
    # The real report (dbghelp, MiniDumpWriteDump) and the Launcher hand-over.
    list(APPEND TOPSPIN4_SOURCES src/crash_report.cpp)
else()
    set(TOPSPIN4_PLATFORM_SOURCES
        src/platform/paths_posix.cpp
        src/platform/dialog_posix.cpp
        src/platform/thread_posix.cpp
        src/platform/audio_posix.cpp
        src/platform/fonts_posix.cpp
        src/platform/crash_posix.cpp
        # Android has no dbghelp/MiniDumpWriteDump: the crash_report API is stubbed.
        src/platform/crash_report_stub.cpp
    )
endif()

if(TS4_WITH_REXGLUE)
    if(ANDROID)
        add_library(topspin4 SHARED
            ${TOPSPIN4_SOURCES}
            ${TOPSPIN4_PLATFORM_SOURCES}
            src/android_main.cpp
        )
        set_target_properties(topspin4 PROPERTIES OUTPUT_NAME topspin4)
        target_link_libraries(topspin4 PRIVATE android log vulkan)
        # 16 KB-page devices (Android 15+): the same .so loads on 4 KB and 16 KB kernels.
        target_link_options(topspin4 PRIVATE "-Wl,-z,max-page-size=16384")
    else()
        add_executable(topspin4
            ${TOPSPIN4_SOURCES}
            ${TOPSPIN4_PLATFORM_SOURCES}
        )
    endif()
else()
    # No SDK: prove the toolchain and the presets only (see src/host_main.cpp).
    if(ANDROID)
        add_library(topspin4 SHARED src/host_main.cpp)
        set_target_properties(topspin4 PROPERTIES OUTPUT_NAME topspin4)
        target_link_libraries(topspin4 PRIVATE android log)
        target_link_options(topspin4 PRIVATE "-Wl,-z,max-page-size=16384")
    else()
        add_executable(topspin4 src/host_main.cpp)
    endif()
endif()

target_include_directories(topspin4 PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/src
)

if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot")
    target_include_directories(topspin4 PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot")
endif()

if(NOT TOPSPIN4_GENERATED_SOURCES)
    # No codegen output yet: main.cpp starts, says so and quits (the reference's ticket 03 path).
    target_compile_definitions(topspin4 PRIVATE TOPSPIN4_NO_GUEST_CODE)
endif()

# rexglue_setup_target() compiles the generated sources into its own OBJECT library
# (topspin4_recomp); adding them here as well duplicates every guest symbol.

if(TOPSPIN4_DEVELOPER_BUILD)
    target_compile_definitions(topspin4 PRIVATE TOPSPIN4_DEVELOPER_BUILD=1)
endif()

# Contraction off: a PPC fmul followed by fadd never fuses.
target_compile_options(topspin4 PRIVATE -ffp-contract=off)

if(TS4_WITH_REXGLUE)
    rexglue_setup_target(topspin4 GPU_PLUGINS xenos)
endif()

# The D3D hooks for each Module's copy of Direct3D (src/d3d/sink.h) and the sink both sides link.
# Generated at configure time from symbols/d3d-functions.csv (copied from the reference).
include(cmake/d3d_hooks.cmake)
if(TS4_WITH_REXGLUE AND TARGET topspin4_d3d_sink)
    target_link_libraries(topspin4 PRIVATE topspin4_d3d_sink)
    target_compile_options(topspin4_d3d_sink PRIVATE -ffp-contract=off)
endif()

# zstd (decompression only) and smol-v unpack the shader pack. The reference takes them from
# .tools/xenosrecomp (BSD-3 / MIT); this tree vendors the same files under src/thirdparty
# (provenance: docs/app-port.md).
set(TOPSPIN4_ZSTD "${CMAKE_CURRENT_SOURCE_DIR}/src/thirdparty/zstd")
set(TOPSPIN4_SMOLV "${CMAKE_CURRENT_SOURCE_DIR}/src/thirdparty/smolv")
add_library(topspin4_native_deps STATIC
    ${TOPSPIN4_ZSTD}/common/debug.c
    ${TOPSPIN4_ZSTD}/common/entropy_common.c
    ${TOPSPIN4_ZSTD}/common/error_private.c
    ${TOPSPIN4_ZSTD}/common/fse_decompress.c
    ${TOPSPIN4_ZSTD}/common/xxhash.c
    ${TOPSPIN4_ZSTD}/common/zstd_common.c
    ${TOPSPIN4_ZSTD}/decompress/huf_decompress.c
    ${TOPSPIN4_ZSTD}/decompress/zstd_ddict.c
    ${TOPSPIN4_ZSTD}/decompress/zstd_decompress.c
    ${TOPSPIN4_ZSTD}/decompress/zstd_decompress_block.c
    ${TOPSPIN4_SMOLV}/smolv.cpp)
target_compile_definitions(topspin4_native_deps PRIVATE ZSTD_DISABLE_ASM)
target_compile_options(topspin4_native_deps PRIVATE -w)
target_include_directories(topspin4_native_deps PUBLIC ${TOPSPIN4_ZSTD} ${TOPSPIN4_SMOLV})
if(TS4_WITH_REXGLUE)
    target_link_libraries(topspin4 PRIVATE topspin4_native_deps)
endif()

# The Native renderer un-tiles textures with the SDK's Xenos texture layout code (BSD-3), which
# lives in the GPU plugin rather than the host runtime.
# Host unit tests of the renderer's pure code, run on the build machine (no device, no game data).
# Catch2 and the SDK come from the SDK tree; opt-in and outside the default build.
option(TOPSPIN4_BUILD_TESTS "Build the host unit tests" OFF)
if(TOPSPIN4_BUILD_TESTS AND REXSDK_DIR)
    enable_testing()
    file(GLOB TOPSPIN4_HOST_TEST_SOURCES CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/tests/native/*.cpp")
    add_executable(topspin4_host_tests EXCLUDE_FROM_ALL
        ${TOPSPIN4_HOST_TEST_SOURCES}
        # The pure parts of the renderer the tests cover.
        src/native/live_topup.cpp
        src/native/pipeline_parts.cpp
        src/native/pipeline_store.cpp
        src/native/prebuild_plan.cpp
        src/native/prepare_output.cpp
        src/native/shader_pack.cpp
        src/native/shader_topup.cpp
        src/native/texture_layout.cpp
        src/native/write_watch.cpp
        # Platform bits the renderer's pure code calls (file access for the shader top-up).
        src/platform/paths_posix.cpp
        ${REXSDK_DIR}/src/graphics/pipeline/texture/util.cpp
        ${REXSDK_DIR}/thirdparty/catch2/extras/catch_amalgamated.cpp)
    target_include_directories(topspin4_host_tests PRIVATE
        src ${REXSDK_DIR}/thirdparty/catch2/extras ${REXSDK_DIR}/thirdparty/xxHash
        ${REXSDK_DIR}/thirdparty/tomlplusplus/include)
    target_link_libraries(topspin4_host_tests PRIVATE rex::runtime topspin4_native_deps)
    add_test(NAME topspin4_host_tests COMMAND topspin4_host_tests)
endif()

if(TS4_WITH_REXGLUE AND REXSDK_DIR)
    target_sources(topspin4 PRIVATE ${REXSDK_DIR}/src/graphics/pipeline/texture/util.cpp)
    target_include_directories(topspin4 PRIVATE ${REXSDK_DIR}/thirdparty/xxHash)
endif()

# The shipped pipeline key list (ADR-0010): beside the exe on Windows, pushed into cache/ on
# Android (the data-prep script does that; see docs/app-port.md).
set(TOPSPIN4_PIPELINE_KEYS "${CMAKE_CURRENT_SOURCE_DIR}/data/pipeline-keys.txt")
if(NOT ANDROID AND TS4_WITH_REXGLUE AND EXISTS "${TOPSPIN4_PIPELINE_KEYS}")
    add_custom_command(OUTPUT "${CMAKE_CURRENT_BINARY_DIR}/pipeline-keys.txt"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${TOPSPIN4_PIPELINE_KEYS}"
            "${CMAKE_CURRENT_BINARY_DIR}/pipeline-keys.txt"
        DEPENDS "${TOPSPIN4_PIPELINE_KEYS}"
        COMMENT "Copying the shipped pipeline key list"
        VERBATIM)
    add_custom_target(topspin4_pipeline_keys ALL DEPENDS "${CMAKE_CURRENT_BINARY_DIR}/pipeline-keys.txt")
    add_dependencies(topspin4 topspin4_pipeline_keys)
endif()

# The achievement toast's sound: sounds/ beside the exe on Windows, userdata/sounds on Android.
# It is a game file, so it is not in the repository -- drop your own copy here to embed it (the
# runtime falls back to the game folder's sounds/achievement.wav either way).
set(TOPSPIN4_ACHIEVEMENT_SOUND "${CMAKE_CURRENT_SOURCE_DIR}/data/sounds/achievement.wav")
if(NOT ANDROID AND TS4_WITH_REXGLUE AND EXISTS "${TOPSPIN4_ACHIEVEMENT_SOUND}")
    add_custom_command(OUTPUT "${CMAKE_CURRENT_BINARY_DIR}/sounds/achievement.wav"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${TOPSPIN4_ACHIEVEMENT_SOUND}"
            "${CMAKE_CURRENT_BINARY_DIR}/sounds/achievement.wav"
        DEPENDS "${TOPSPIN4_ACHIEVEMENT_SOUND}"
        COMMENT "Copying the achievement sound"
        VERBATIM)
    add_custom_target(topspin4_sounds ALL DEPENDS "${CMAKE_CURRENT_BINARY_DIR}/sounds/achievement.wav")
    add_dependencies(topspin4 topspin4_sounds)
endif()

# The shader pack's converter id is the XenosRecomp patch series' hash. The series is the
# reference's (copied into data/xenosrecomp, because ts4-port/patches belongs to the SDK work).
# A pack converted on the Mac with the same series matches; with no series the id is 0 and
# shader_pack.cpp skips the check.
file(GLOB TOPSPIN4_XENOSRECOMP_PATCHES CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/data/xenosrecomp/*.patch")
list(SORT TOPSPIN4_XENOSRECOMP_PATCHES)
set(TOPSPIN4_XENOSRECOMP_SERIES "")
foreach(topspin4_patch IN LISTS TOPSPIN4_XENOSRECOMP_PATCHES)
    file(READ "${topspin4_patch}" topspin4_patch_text HEX)
    string(APPEND TOPSPIN4_XENOSRECOMP_SERIES "${topspin4_patch_text}")
endforeach()
if(TOPSPIN4_XENOSRECOMP_SERIES)
    string(SHA256 TOPSPIN4_XENOSRECOMP_SHA256 "${TOPSPIN4_XENOSRECOMP_SERIES}")
    string(SUBSTRING "${TOPSPIN4_XENOSRECOMP_SHA256}" 0 16 TOPSPIN4_EXPECTED_CONVERTER_ID)
else()
    set(TOPSPIN4_EXPECTED_CONVERTER_ID "0")
endif()

# Crash report manifest facts, and the Native renderer's converter id.
execute_process(COMMAND git rev-parse --short HEAD WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
    OUTPUT_VARIABLE TOPSPIN4_COMMIT OUTPUT_STRIP_TRAILING_WHITESPACE ERROR_QUIET)
if(NOT TOPSPIN4_COMMIT)
    set(TOPSPIN4_COMMIT "unknown")
endif()
target_compile_definitions(topspin4 PRIVATE
    TOPSPIN4_VERSION="${PROJECT_VERSION}"
    TOPSPIN4_COMMIT="${TOPSPIN4_COMMIT}"
    TOPSPIN4_EXPECTED_SHADER_CONVERTER_ID=0x${TOPSPIN4_EXPECTED_CONVERTER_ID}ull
)
# The tests check the pipeline keys and converter id the build would ship, so they need the same
# facts (defined here, after the ids above are computed).
if(TARGET topspin4_host_tests)
    target_compile_definitions(topspin4_host_tests PRIVATE
        TOPSPIN4_EXPECTED_SHADER_CONVERTER_ID=0x${TOPSPIN4_EXPECTED_CONVERTER_ID}ull
        TOPSPIN4_PIPELINE_KEYS="${TOPSPIN4_PIPELINE_KEYS}"
    )
endif()

# The v0.10.0 source-tree runtime does not propagate ImGui's include directory to the host,
# although its public UI headers include imgui.h. The crash report and settings code also reach
# RenderDoc's, toml++'s and SDL3's headers.
if(TS4_WITH_REXGLUE AND REXSDK_DIR)
    target_include_directories(topspin4 PRIVATE
        $<TARGET_PROPERTY:imgui::imgui,INTERFACE_INCLUDE_DIRECTORIES>
        ${REXSDK_DIR}/thirdparty/renderdoc
        ${REXSDK_DIR}/thirdparty/tomlplusplus/include
        ${REXSDK_DIR}/thirdparty/sdl3/include
    )
endif()

if(WIN32 AND TS4_WITH_REXGLUE)
    target_link_libraries(topspin4 PRIVATE dbghelp shell32 winmm)
    enable_language(RC)
    target_sources(topspin4 PRIVATE src/topspin4.rc)
    set_source_files_properties(src/topspin4.rc PROPERTIES COMPILE_DEFINITIONS
        "TOPSPIN4_VERSION_MAJOR=${PROJECT_VERSION_MAJOR};TOPSPIN4_VERSION_MINOR=${PROJECT_VERSION_MINOR};TOPSPIN4_VERSION_PATCH=${PROJECT_VERSION_PATCH};TOPSPIN4_VERSION_TEXT=\"${PROJECT_VERSION}\";TOPSPIN4_DESCRIPTION=\"Top Spin 4 v${PROJECT_VERSION}\"")
endif()

# PGO: the recompiled objects need the profiling runtime at link time too (TS4_PGO=generate in
# cmake/rexglue.cmake adds the compile flags).
if(TS4_PGO STREQUAL "generate" AND ANDROID)
    foreach(pgo_target topspin4 topspin4_Loader_DLL topspin4_Swing_DLL)
        if(TARGET ${pgo_target})
            target_link_options(${pgo_target} PRIVATE -fprofile-generate)
        endif()
    endforeach()
endif()
