cmake_minimum_required(VERSION 3.25)

project(AlbaEcosistema
    VERSION 9.0.0
    DESCRIPTION "Ecosistema de programacion Cibao y Xynex Code Editor (Motor, CLI, IDE y Addons)"
    LANGUAGES CXX
)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Deteccion de plataforma objetivo para compilacion portable Windows / macOS / Linux
if(WIN32)
    set(ALBA_DEFAULT_RID "win-x64")
    set(ALBA_EXE_EXT ".exe")
elseif(APPLE)
    if(CMAKE_OSX_ARCHITECTURES MATCHES "x86_64" OR CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64|AMD64")
        set(ALBA_DEFAULT_RID "osx-x64")
    else()
        set(ALBA_DEFAULT_RID "osx-arm64")
    endif()
    set(ALBA_EXE_EXT "")
elseif(UNIX)
    set(ALBA_DEFAULT_RID "linux-x64")
    set(ALBA_EXE_EXT "")
else()
    message(FATAL_ERROR "Plataforma no soportada por esta configuracion. Se requiere Windows, macOS o Linux.")
endif()

set(ALBA_DOTNET_RID "${ALBA_DEFAULT_RID}" CACHE STRING "Identificador RID de .NET para publicacion (win-x64, osx-arm64, osx-x64, linux-x64)")

if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE "Release" CACHE STRING "Tipo de compilacion (Debug/Release)" FORCE)
endif()

find_program(DOTNET_EXECUTABLE dotnet REQUIRED)
message(STATUS "SDK de .NET detectado en: ${DOTNET_EXECUTABLE}")
message(STATUS "Identificador de runtime objetivo (RID): ${ALBA_DOTNET_RID}")

set(ALBA_BIN_DIR "${CMAKE_BINARY_DIR}/bin/${CMAKE_BUILD_TYPE}")
file(MAKE_DIRECTORY "${ALBA_BIN_DIR}")

# Compilacion del motor Alba.Engine
add_custom_target(alba_engine ALL
    COMMAND ${DOTNET_EXECUTABLE} build "${CMAKE_SOURCE_DIR}/src/Alba.Engine/Alba.Engine.csproj"
            -c ${CMAKE_BUILD_TYPE}
            --nologo
    WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
    COMMENT "Compilando motor Alba.Engine (.NET 8)..."
)

# Publicacion autocontenida de Alba.CLI
if(EXISTS "${CMAKE_SOURCE_DIR}/src/Alba.CLI/Alba.CLI.csproj")
    add_custom_target(alba_cli ALL
        COMMAND ${DOTNET_EXECUTABLE} publish "${CMAKE_SOURCE_DIR}/src/Alba.CLI/Alba.CLI.csproj"
                -c ${CMAKE_BUILD_TYPE}
                -r ${ALBA_DOTNET_RID}
                --self-contained true
                -o "${ALBA_BIN_DIR}/cli"
                --nologo
        DEPENDS alba_engine
        WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
        COMMENT "Publicando Alba.CLI para ${ALBA_DOTNET_RID}..."
    )
    install(DIRECTORY "${ALBA_BIN_DIR}/cli" DESTINATION bin OPTIONAL)
endif()

# Publicacion autocontenida de Alba.IDE (Alba Studio 2.0)
if(EXISTS "${CMAKE_SOURCE_DIR}/src/Alba.IDE/Alba.IDE.csproj")
    add_custom_target(alba_ide ALL
        COMMAND ${DOTNET_EXECUTABLE} publish "${CMAKE_SOURCE_DIR}/src/Alba.IDE/Alba.IDE.csproj"
                -c ${CMAKE_BUILD_TYPE}
                -r ${ALBA_DOTNET_RID}
                --self-contained true
                -o "${ALBA_BIN_DIR}/ide"
                --nologo
        DEPENDS alba_engine
        WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
        COMMENT "Publicando Alba.IDE (Avalonia UI) para ${ALBA_DOTNET_RID}..."
    )
    install(DIRECTORY "${ALBA_BIN_DIR}/ide" DESTINATION bin OPTIONAL)
endif()

if(EXISTS "${CMAKE_SOURCE_DIR}/native-launcher/CMakeLists.txt")
    add_subdirectory(native-launcher)
endif()

# Recursos portables integrados (addons, documentacion y ejemplos)
add_custom_target(alba_recursos ALL
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${CMAKE_SOURCE_DIR}/addons" "${ALBA_BIN_DIR}/addons"
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${CMAKE_SOURCE_DIR}/docs" "${ALBA_BIN_DIR}/docs"
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${CMAKE_SOURCE_DIR}/examples" "${ALBA_BIN_DIR}/examples"
    COMMENT "Copiando /addons, /docs y /examples al directorio portable..."
)
install(DIRECTORY "${CMAKE_SOURCE_DIR}/addons" DESTINATION bin)
install(DIRECTORY "${CMAKE_SOURCE_DIR}/docs" DESTINATION bin)
install(DIRECTORY "${CMAKE_SOURCE_DIR}/examples" DESTINATION bin)

# Pruebas automatizadas con CTest
enable_testing()

add_test(
    NAME AlbaEngineUnitTests
    COMMAND ${DOTNET_EXECUTABLE} test "${CMAKE_SOURCE_DIR}/tests/Alba.Engine.Tests/Alba.Engine.Tests.csproj"
            -c ${CMAKE_BUILD_TYPE}
            --nologo
            --verbosity normal
    WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
)

if(EXISTS "${CMAKE_SOURCE_DIR}/tests/Alba.IDE.Tests/Alba.IDE.Tests.csproj")
    add_test(
        NAME AlbaIdeUnitTests
        COMMAND ${DOTNET_EXECUTABLE} test "${CMAKE_SOURCE_DIR}/tests/Alba.IDE.Tests/Alba.IDE.Tests.csproj"
                -c ${CMAKE_BUILD_TYPE}
                --nologo
                --verbosity normal
        WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
    )
endif()

if(EXISTS "${CMAKE_SOURCE_DIR}/tests/Alba.NativeHost.Tests/launcher_smoke.cpp")
    add_executable(alba_native_smoke
        tests/Alba.NativeHost.Tests/launcher_smoke.cpp
    )
    add_test(
        NAME AlbaNativeHostSmokeTest
        COMMAND alba_native_smoke
    )
endif()

# Configuracion base de CPack
set(CPACK_PACKAGE_NAME "XynexCodeEditor")
set(CPACK_PACKAGE_VENDOR "CynexStudios")
set(CPACK_PACKAGE_VERSION ${PROJECT_VERSION})
set(CPACK_PACKAGE_DESCRIPTION_SUMMARY "Xynex Code Editor 9.0 — Lenguaje Cibao e IDE multiplataforma")

if(WIN32)
    set(CPACK_GENERATOR "ZIP")
else()
    set(CPACK_GENERATOR "TGZ")
endif()

include(CPack)
