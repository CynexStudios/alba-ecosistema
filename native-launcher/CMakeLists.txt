add_executable(alba-host
    src/main.cpp
)

set_target_properties(alba-host PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${ALBA_BIN_DIR}"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${ALBA_BIN_DIR}"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${ALBA_BIN_DIR}"
    RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO "${ALBA_BIN_DIR}"
    RUNTIME_OUTPUT_DIRECTORY_MINSIZEREL "${ALBA_BIN_DIR}"
)

if(WIN32)
    target_compile_definitions(alba-host PRIVATE ALBA_PLATFORM_WINDOWS=1)
elseif(APPLE)
    target_compile_definitions(alba-host PRIVATE ALBA_PLATFORM_MACOS=1)
else()
    target_compile_definitions(alba-host PRIVATE ALBA_PLATFORM_LINUX=1)
endif()

install(TARGETS alba-host RUNTIME DESTINATION bin)
