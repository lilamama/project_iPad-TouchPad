cmake_minimum_required(VERSION 3.15)
project(iPadDrawingHost LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_executable(iPadDrawingHost iPadDrawingHost.cpp)

target_link_libraries(iPadDrawingHost PRIVATE
    ws2_32
    user32
)
