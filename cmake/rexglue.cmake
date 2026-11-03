# Adapted from the Windows reference's cmake/rexglue.cmake.
# WIN32/MSVC-specific bits dropped for the Android/macOS port.

# SDK version
set(REXSDK_VERSION "" CACHE STRING "Override SDK version (leave empty for default)")

# Find SDK
set(REXSDK_DIR "" CACHE PATH "Path to rexglue-sdk source tree")
if(REXSDK_DIR)
    add_subdirectory("${REXSDK_DIR}" rexglue-sdk)
    message(STATUS "Using ReXGlue SDK from source tree: ${REXSDK_DIR}")
else()
    if(REXSDK_VERSION)
        find_package(rexglue ${REXSDK_VERSION} EXACT QUIET CONFIG)
    else()
        find_package(rexglue 0.10.0 QUIET CONFIG)
    endif()
    if(NOT rexglue_FOUND)
        message(FATAL_ERROR
            "ReXGlue SDK not found. Either:\n"
            "  - Set REXSDK_DIR to the rexglue-sdk source tree\n"
            "  - Install the SDK package and ensure it is on CMAKE_PREFIX_PATH")
    endif()
    message(STATUS "Found ReXGlue SDK ${REXGLUE_VERSION_STRING} at ${rexglue_DIR}")
endif()

# Host target name used by codegen-emitted dll_targets.cmake.
if(NOT DEFINED REXGLUE_HOST_TARGET)
    set(REXGLUE_HOST_TARGET ${PROJECT_NAME})
endif()

set(REXGLUE_RECOMP_DEBUG_INFO "line-tables-only" CACHE STRING
    "Debug info level for generated code: line-tables-only, full, or none")

# Recomp options (host/Android; no WIN32/MSVC EH flags).
set(REXGLUE_RECOMP_OPTIONS "")
if(REXGLUE_RECOMP_DEBUG_INFO STREQUAL "none")
    list(APPEND REXGLUE_RECOMP_OPTIONS
        $<$<CXX_COMPILER_ID:Clang,AppleClang,GNU>:-g0>)
elseif(REXGLUE_RECOMP_DEBUG_INFO STREQUAL "line-tables-only")
    list(APPEND REXGLUE_RECOMP_OPTIONS
        $<$<CXX_COMPILER_ID:Clang,AppleClang>:-gline-tables-only>)
endif()

# The recompiled guest code is the CPU bottleneck on the Nova (measured ~70 ms/frame with the
# renderer at 0.6 ms), and the NDK default is a plain armv8-a. -O3 plus the Cortex-A510/A710/X3
# feature set (dot-product) is safe on this SoC and costs nothing but build time. These come after
# the configuration's -O2, so they win.
list(APPEND REXGLUE_RECOMP_OPTIONS
    $<$<AND:$<PLATFORM_ID:Android>,$<CXX_COMPILER_ID:Clang>>:-O3>
    $<$<AND:$<PLATFORM_ID:Android>,$<CXX_COMPILER_ID:Clang>>:-march=armv8.2-a+dotprod>)

# Include entrypoint generated code if codegen has been run.
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot/sources.cmake")
    include(generated/boot/sources.cmake)
    set(REXGLUE_ENTRYPOINT_GENERATED_SOURCES ${GENERATED_SOURCES})
    set(REXGLUE_ENTRYPOINT_INCLUDE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot")

    set_source_files_properties(${REXGLUE_ENTRYPOINT_GENERATED_SOURCES}
        PROPERTIES COMPILE_OPTIONS "${REXGLUE_RECOMP_OPTIONS}")
endif()

function(rexglue_apply_recomp_settings target_name pch_header)
    target_precompile_headers(${target_name} PRIVATE "${pch_header}")
    target_compile_options(${target_name} PRIVATE ${REXGLUE_RECOMP_OPTIONS})
endfunction()

macro(rexglue_setup_target target_name)
    if(REXGLUE_ENTRYPOINT_GENERATED_SOURCES)
        add_library(${target_name}_recomp OBJECT
            ${REXGLUE_ENTRYPOINT_GENERATED_SOURCES})
        target_include_directories(${target_name}_recomp PRIVATE
            ${CMAKE_CURRENT_SOURCE_DIR}
            ${CMAKE_CURRENT_SOURCE_DIR}/src
            ${REXGLUE_ENTRYPOINT_INCLUDE_DIR}
        )
        target_link_libraries(${target_name}_recomp PRIVATE rex::runtime)
        rexglue_apply_target_settings(${target_name}_recomp)
        rexglue_apply_recomp_settings(${target_name}_recomp
            "${REXGLUE_ENTRYPOINT_INCLUDE_DIR}/topspin4_pch.h")
        if(TARGET ${target_name}_codegen)
            add_dependencies(${target_name}_recomp ${target_name}_codegen)
        endif()
        target_link_libraries(${target_name} PRIVATE
            ${target_name}_recomp)
        if(TARGET ${target_name}_codegen)
            add_dependencies(${target_name} ${target_name}_codegen)
        endif()
    endif()
    target_include_directories(${target_name} PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${CMAKE_CURRENT_SOURCE_DIR}/src
        ${REXGLUE_ENTRYPOINT_INCLUDE_DIR}
    )
    target_link_libraries(${target_name} PRIVATE rex::runtime)
    if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/metadata/icons")
        rexglue_embed_metadata(${target_name}
            DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/metadata/icons"
            PREFIX "icons")
    endif()
    if(ANDROID)
        # Android: the app is a library loaded by SDLActivity, whose entry is SDL_main from
        # src/android_main.cpp. The SDK's windowed_app_main_sdl.cpp defines a plain main() that
        # SDLActivity never calls, so it is not compiled here; the pieces
        # rexglue_configure_target() would add are added explicitly instead.
        target_sources(${target_name} PRIVATE ${REXGLUE_SHARE_DIR}/rex_app.cpp)
        target_compile_definitions(${target_name} PRIVATE
            REXGLUE_BUILD_CONFIG="$<CONFIG>")
        rexglue_apply_target_settings(${target_name})
        # The GPU plugin is dlopen'd by soname on Android (SDK patch), never linked.
        if(TARGET rexgpu-xenos)
            add_dependencies(${target_name} rexgpu-xenos)
        endif()
    else()
        rexglue_configure_target(${target_name} ${ARGN})
    endif()
endmacro()

# Codegen runs as part of the build only where the host recompiler exists (the SDK does not
# cross-compile src/rexglue for Android). On Android the generated sources are those the Mac's
# host build already wrote into generated/boot; they are compiled, not regenerated.
if(REXGLUE_ENTRYPOINT_GENERATED_SOURCES AND TARGET rex::rexglue AND NOT ANDROID)
    add_custom_command(
        OUTPUT "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot/codegen.build.stamp"
               ${REXGLUE_ENTRYPOINT_GENERATED_SOURCES}
        COMMAND $<TARGET_FILE:rex::rexglue> codegen ${CMAKE_CURRENT_SOURCE_DIR}/topspin4_manifest.toml
        DEPFILE "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot/codegen.d"
        WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
        COMMENT "Generating recompiled code for topspin4"
        VERBATIM
    )
    add_custom_target(topspin4_codegen
        DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot/codegen.build.stamp")
elseif(REXGLUE_ENTRYPOINT_GENERATED_SOURCES AND NOT TARGET topspin4_codegen)
    # Android (and any build without the host recompiler): codegen already ran on the Mac, but
    # the generated dll_targets.cmake still names topspin4_codegen as a dependency.
    add_custom_target(topspin4_codegen)
endif()

if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot/dll_targets.cmake")
    include(generated/boot/dll_targets.cmake)
endif()
