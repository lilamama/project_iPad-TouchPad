@echo off
echo ========================================================
echo Building iPadDrawingHost (C++)
echo ========================================================

where cl >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    echo Compiling with MSVC cl.exe...
    cl /EHsc /O2 /std:c++20 iPadDrawingHost.cpp /link user32.lib ws2_32.lib /out:iPadDrawingHost.exe
    if %ERRORLEVEL% EQU 0 (
        echo Build succeeded: iPadDrawingHost.exe
        goto done
    )
)

where g++ >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    echo Compiling with MinGW g++...
    g++ -O3 -std=c++20 iPadDrawingHost.cpp -lws2_32 -luser32 -o iPadDrawingHost.exe
    if %ERRORLEVEL% EQU 0 (
        echo Build succeeded: iPadDrawingHost.exe
        goto done
    )
)

echo.
echo Neither MSVC (cl.exe) nor MinGW (g++) was found in PATH.
echo Please run this from a Visual Studio Developer Command Prompt or install CMake/MSVC.
:done
