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
        add_dependencies(${target_name}_recomp ${target_name}_codegen)
        target_link_libraries(${target_name} PRIVATE
            ${target_name}_recomp)
        add_dependencies(${target_name} ${target_name}_codegen)
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
    rexglue_configure_target(${target_name} ${ARGN})
endmacro()

if(REXGLUE_ENTRYPOINT_GENERATED_SOURCES)
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
endif()

if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot/dll_targets.cmake")
    include(generated/boot/dll_targets.cmake)
endif()
