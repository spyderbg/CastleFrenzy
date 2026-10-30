cmake_minimum_required(VERSION 3.24)

project(FortressDefender LANGUAGES C CXX)

if(NOT CMAKE_SYSTEM_NAME STREQUAL "Linux" AND NOT WIN32)
    message(FATAL_ERROR "The supported CMake build targets 64-bit Linux and Windows.")
endif()
if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR "Castle Frenzy requires a 64-bit compiler. Remove cached -m32 flags and configure with --fresh.")
endif()
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CASTLE_ASSET_DIR "${CMAKE_CURRENT_SOURCE_DIR}/Build/bin" CACHE PATH "Game resource directory")

if(WIN32)
    add_compile_definitions(WIN32 _WINDOWS NOMINMAX UNICODE _UNICODE _CRT_SECURE_NO_WARNINGS
        "$<$<CONFIG:Debug>:_DEBUG>")
    include(cmake/windows64.cmake)
else()
    include(cmake/linux64.cmake)
    find_package(OpenAL REQUIRED)
    find_package(X11 REQUIRED)
    find_library(XXF86VM_LIBRARY Xxf86vm REQUIRED)
endif()
find_package(OpenGL REQUIRED)
include(cmake/sprite_yaml.cmake)

set(IRR_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/Lib/Irrlicht")
set(IRR_SRC "${IRR_ROOT}/source/Irrlicht")
file(GLOB ENGINE_SOURCES CONFIGURE_DEPENDS "${IRR_SRC}/*.cpp")
file(GLOB ZLIB_SOURCES CONFIGURE_DEPENDS "${IRR_SRC}/zlib/*.c")
file(GLOB PNG_SOURCES CONFIGURE_DEPENDS "${IRR_SRC}/libpng/png*.c")
# Compile the codec, using this fork's allocator instead of platform backends.
file(GLOB JPEG_SOURCES CONFIGURE_DEPENDS "${IRR_SRC}/jpeglib/j*.c")
list(FILTER JPEG_SOURCES EXCLUDE REGEX "/(jmem(ansi|dos|mac|name|nobs)|jpegtran)\\.c$")
add_library(Irrlicht STATIC ${ENGINE_SOURCES} ${ZLIB_SOURCES} ${PNG_SOURCES} ${JPEG_SOURCES}
    "${IRR_ROOT}/tools/ResFile/src/resFile.cpp")
target_include_directories(Irrlicht PUBLIC
    "${IRR_ROOT}/include" "${IRR_ROOT}/tools/ColladaConverter/include"
    "${IRR_ROOT}/tools/ResFile/include" "${IRR_SRC}"
    PRIVATE "${IRR_SRC}/zlib" "${IRR_SRC}/libpng" "${IRR_SRC}/jpeglib"
    "${IRR_SRC}/particlesystem/include" ${X11_INCLUDE_DIR})
target_compile_definitions(Irrlicht PUBLIC _IRR_STATIC_LIB_ PRIVATE
    PNG_NO_MMX_CODE PNG_NO_MNG_FEATURES PNG_THREAD_UNSAFE_OK)
target_link_libraries(Irrlicht PUBLIC OpenGL::GL OpenGL::GLU)
if(WIN32)
    target_link_libraries(Irrlicht PUBLIC winmm user32 gdi32 shell32 advapi32)
else()
    target_link_libraries(Irrlicht PUBLIC ${X11_LIBRARIES} ${XXF86VM_LIBRARY} ${CMAKE_DL_LIBS})
endif()

add_executable(BdaeCheck EXCLUDE_FROM_ALL tools/linux/bdae_check.cpp)
target_link_libraries(BdaeCheck PRIVATE Irrlicht)
set_target_properties(BdaeCheck PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")

file(GLOB SOUND_SOURCES CONFIGURE_DEPENDS "Lib/SoundLib/src/*.cpp")
add_library(SoundLib STATIC ${SOUND_SOURCES})
target_include_directories(SoundLib PUBLIC "Lib/SoundLib/include" ${OPENAL_INCLUDE_DIR})
target_link_libraries(SoundLib PUBLIC Irrlicht ${OPENAL_LIBRARY})

file(GLOB_RECURSE GAME_SOURCES CONFIGURE_DEPENDS "Src/*.cpp")
list(FILTER GAME_SOURCES EXCLUDE REGEX "/PlatformSpecifics/(Linux|Win32|IPhone)/|AccelerometerWin32\\.cpp$|MediaPlayerWin32\\.cpp$")
list(FILTER GAME_SOURCES EXCLUDE REGEX "/Sprites/SpriteYaml\\.cpp$")
add_executable(FortressDefender ${GAME_SOURCES})
target_include_directories(FortressDefender PRIVATE Src)
if(WIN32)
    target_sources(FortressDefender PRIVATE Src/PlatformSpecifics/Win32/main.cpp
        Src/IO/Accelerometer/AccelerometerWin32.cpp)
    target_compile_definitions(FortressDefender PRIVATE OS_WIN32)
    add_custom_command(TARGET FortressDefender POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "$<TARGET_FILE:OpenAL>" "$<TARGET_FILE_DIR:FortressDefender>"
        VERBATIM)
    set_property(TARGET FortressDefender PROPERTY VS_DEBUGGER_WORKING_DIRECTORY
        "$<TARGET_FILE_DIR:FortressDefender>")
else()
    target_sources(FortressDefender PRIVATE Src/PlatformSpecifics/Linux/main.cpp)
    target_compile_definitions(FortressDefender PRIVATE OS_LINUX)
endif()
target_link_libraries(FortressDefender PRIVATE SoundLib SpriteYaml)
set_target_properties(FortressDefender PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
# The default asset location works regardless of the process working directory.
target_compile_definitions(FortressDefender PRIVATE CASTLE_ASSET_DIR="${CASTLE_ASSET_DIR}")
if(EXISTS "${CASTLE_DEPS}/usr/lib/x86_64-linux-gnu/dri")
    target_compile_definitions(FortressDefender PRIVATE
        CASTLE_LOCAL_DRI_DIR="${CASTLE_DEPS}/usr/lib/x86_64-linux-gnu/dri"
        CASTLE_LOCAL_LIBRARY_DIR="${CASTLE_DEPS}/usr/lib/x86_64-linux-gnu:${CASTLE_DEPS}/usr/lib/x86_64-linux-gnu/pulseaudio")
endif()

add_custom_target(run
    COMMAND "$<TARGET_FILE:FortressDefender>"
    DEPENDS FortressDefender WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/Build"
    USES_TERMINAL)

include(CTest)
if(BUILD_TESTING)
    add_executable(SpriteYamlCheck tests/linux/sprite_yaml.cpp)
    target_link_libraries(SpriteYamlCheck PRIVATE SpriteYaml)
    add_test(NAME sprite_yaml COMMAND SpriteYamlCheck
        "${CMAKE_CURRENT_SOURCE_DIR}/tests/fixtures/sprites/dizzy_effect.yaml"
        "${CMAKE_CURRENT_SOURCE_DIR}/tests/fixtures/sprites/dizzy_effect.bsprite")
    file(GLOB GAME_SPRITE_YAML CONFIGURE_DEPENDS "Data/Sprites/*/*.yaml" "Data/Sprites/*/*.yml")
    add_test(NAME sprite_yaml_assets COMMAND SpriteYamlCheck --validate ${GAME_SPRITE_YAML})

    add_executable(XmlRoundtripCheck tests/linux/xml_roundtrip.cpp)
    target_link_libraries(XmlRoundtripCheck PRIVATE Irrlicht)
    add_test(NAME xml_roundtrip COMMAND XmlRoundtripCheck)

    add_executable(Resource64Check tests/linux/resource64.cpp
        "${IRR_ROOT}/tools/ResFile/src/resFile.cpp")
    target_include_directories(Resource64Check PRIVATE "${IRR_ROOT}/tools/ResFile/include")
    add_test(NAME resource64 COMMAND Resource64Check)

endif()

if(WIN32)
    install(TARGETS FortressDefender OpenAL RUNTIME DESTINATION bin)
    install(DIRECTORY "${CASTLE_ASSET_DIR}/" DESTINATION assets
        PATTERN "*.sprite" EXCLUDE PATTERN "*.bsprite" EXCLUDE
        PATTERN "resources.pack" EXCLUDE PATTERN ".svn" EXCLUDE PATTERN "*.pvr" EXCLUDE)
    # OpenAL Soft is distributed under the LGPL; preserve its license and source URL.
    install(FILES "${openal_soft_SOURCE_DIR}/COPYING" DESTINATION licenses RENAME OpenAL-Soft.txt)
    install(FILES Lib/yaml-cpp.LICENSE DESTINATION licenses RENAME yaml-cpp.txt)
    install(FILES tools/windows/README.txt DESTINATION .)
    set(CPACK_GENERATOR ZIP)
    set(CPACK_PACKAGE_FILE_NAME CastleFrenzy_Windows_x64)
    include(CPack)
endif()

if(WIN32 AND MINGW)
    foreach(target FortressDefender BdaeCheck Resource64Check XmlRoundtripCheck SpriteYamlCheck)
        if(TARGET ${target})
            add_custom_command(TARGET ${target} POST_BUILD
                COMMAND ${CMAKE_COMMAND} -E copy_if_different ${CASTLE_MINGW_RUNTIME_DLLS}
                    "$<TARGET_FILE_DIR:${target}>"
                VERBATIM)
        endif()
    endforeach()
    install(FILES ${CASTLE_MINGW_RUNTIME_DLLS} DESTINATION bin)
endif()
