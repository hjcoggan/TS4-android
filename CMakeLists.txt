# Top Spin 4 port (Android arm64-v8a + host dev builds).
# SDK boilerplate mirrors the Windows reference's cmake/rexglue.cmake, adapted.

cmake_minimum_required(VERSION 3.25)
project(topspin4 VERSION 1.0.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

option(TS4_WITH_REXGLUE "Enable ReXGlue SDK integration (unverified on Android)" OFF)

if(TS4_WITH_REXGLUE)
    include(cmake/rexglue.cmake)
endif()

# Generated codegen sources, only if codegen has been run.
set(TOPSPIN4_GENERATED_SOURCES "")
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot/sources.cmake")
    if(TS4_WITH_REXGLUE AND DEFINED REXGLUE_ENTRYPOINT_GENERATED_SOURCES)
        set(TOPSPIN4_GENERATED_SOURCES ${REXGLUE_ENTRYPOINT_GENERATED_SOURCES})
    else()
        include(generated/boot/sources.cmake)
        set(TOPSPIN4_GENERATED_SOURCES ${GENERATED_SOURCES})
    endif()
endif()

if(ANDROID)
    add_library(topspin4 SHARED
        src/android_main.cpp
        ${TOPSPIN4_GENERATED_SOURCES}
    )
    set_target_properties(topspin4 PROPERTIES OUTPUT_NAME topspin4)
    target_link_libraries(topspin4 PRIVATE android log vulkan)
else()
    add_executable(topspin4
        src/host_main.cpp
        ${TOPSPIN4_GENERATED_SOURCES}
    )
endif()

if(TS4_WITH_REXGLUE)
    rexglue_setup_target(topspin4)
endif()

target_include_directories(topspin4 PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/src
)

if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot")
    target_include_directories(topspin4 PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot")
endif()

if(NOT TOPSPIN4_GENERATED_SOURCES)
    target_compile_definitions(topspin4 PRIVATE TOPSPIN4_NO_GUEST_CODE)
endif()

# Contraction off: a PPC fmul followed by fadd never fuses.
target_compile_options(topspin4 PRIVATE -ffp-contract=off)
