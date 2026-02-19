cmake_minimum_required(VERSION 3.20)
project(XInputProxy VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Output name must match the original DLL
set(OUTPUT_NAME "xinput1_4")

# Source files
set(SOURCES
    xinput_proxy.cpp
    pipe_client.cpp
)

set(HEADERS
    pipe_client.h
)

# Create the proxy DLL
add_library(${PROJECT_NAME} SHARED ${SOURCES} ${HEADERS})

# Set output name without any prefix
set_target_properties(${PROJECT_NAME} PROPERTIES
    OUTPUT_NAME ${OUTPUT_NAME}
    PREFIX ""
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

# Link Windows libraries
target_link_libraries(${PROJECT_NAME} PRIVATE
    kernel32
)

# Use the .def file for exports
set_target_properties(${PROJECT_NAME} PROPERTIES
    LINK_FLAGS "/DEF:${CMAKE_CURRENT_SOURCE_DIR}/xinput1_4.def"
)

# Compiler definitions
target_compile_definitions(${PROJECT_NAME} PRIVATE
    _UNICODE
    UNICODE
    WIN32_LEAN_AND_MEAN
)

# Enable warnings
if(MSVC)
    target_compile_options(${PROJECT_NAME} PRIVATE /W4)
else()
    target_compile_options(${PROJECT_NAME} PRIVATE -Wall -Wextra)
endif()

# Installation
install(TARGETS ${PROJECT_NAME}
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
)

message(STATUS "XInputProxy will be built as: ${OUTPUT_NAME}.dll")
