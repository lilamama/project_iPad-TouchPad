#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif

#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <winuser.h>

#include <iostream>
#include <string>
#include <sstream>
#include <cmath>
#include <chrono>
#include <algorithm>
#include <map>

#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "user32.lib")

// Win32 Synthetic Pointer Input API definitions (in case of older SDK headers)
#ifndef PT_PEN
#define PT_PEN 3
#endif

#ifndef POINTER_FEEDBACK_DEFAULT
#define POINTER_FEEDBACK_DEFAULT 1
#endif

#ifndef POINTER_FLAG_NONE
#define POINTER_FLAG_NONE 0x00000000
#define POINTER_FLAG_NEW 0x00000001
#define POINTER_FLAG_INRANGE 0x00000002
#define POINTER_FLAG_INCONTACT 0x00000004
#define POINTER_FLAG_FIRSTBUTTON 0x00000010
#define POINTER_FLAG_PRIMARY 0x00002000
#define POINTER_FLAG_DOWN 0x00010000
#define POINTER_FLAG_UPDATE 0x00020000
#define POINTER_FLAG_UP 0x00040000
#endif

#ifndef PEN_FLAG_BARREL
#define PEN_FLAG_NONE 0x00000000
#define PEN_FLAG_BARREL 0x00000001
#define PEN_FLAG_INVERTED 0x00000002
#define PEN_FLAG_ERASER 0x00000004
#endif

#ifndef PEN_MASK_PRESSURE
#define PEN_MASK_NONE 0x00000000
#define PEN_MASK_PRESSURE 0x00000001
#define PEN_MASK_ROTATION 0x00000002
#define PEN_MASK_TILT_X 0x00000004
#define PEN_MASK_TILT_Y 0x00000008
#endif

// Pointer Injection Function Prototypes
typedef HANDLE HSYNTHETICPOINTERDEVICE;
typedef HSYNTHETICPOINTERDEVICE(WINAPI* PFN_CreateSyntheticPointerDevice)(
    DWORD pointerType, ULONG maxCount, DWORD mode);
typedef BOOL(WINAPI* PFN_InjectSyntheticPointerInput)(
    HSYNTHETICPOINTERDEVICE device, const void* pointerInfo, UINT32 count);
typedef VOID(WINAPI* PFN_DestroySyntheticPointerDevice)(
    HSYNTHETICPOINTERDEVICE device);
typedef BOOL(WINAPI* PFN_SetProcessDpiAwarenessContext)(
    HANDLE dpiContext);

#define DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 ((HANDLE)-4)

// Simple JSON extraction helper for minimal overhead / zero external dependencies
struct PenInputPacket {
    std::string type = "";
    double x = 0.0;
    double y = 0.0;
    double pressure = 0.0;
    double tilt_x = 0.0;
    double tilt_y = 0.0;
    double altitude = -1.0;
    double azimuth = -1.0;
    double roll = 0.0;
    bool has_roll = false;
    bool squeeze = false;
    bool barrel = false;
    bool eraser = false;
};

// Fast zero-dependency JSON value extractor
std::string ExtractJsonString(const std::string& json, const std::string& key) {
    std::string search = "\"" + key + "\"";
    size_t pos = json.find(search);
    if (pos == std::string::npos) return "";
    pos = json.find(':', pos);
    if (pos == std::string::npos) return "";
    pos = json.find('\"', pos);
    if (pos == std::string::npos) return "";
    size_t endPos = json.find('\"', pos + 1);
    if (endPos == std::string::npos) return "";
    return json.substr(pos + 1, endPos - pos - 1);
}

double ExtractJsonNumber(const std::string& json, const std::string& key, double defaultVal = 0.0) {
    std::string search = "\"" + key + "\"";
    size_t pos = json.find(search);
    if (pos == std::string::npos) return defaultVal;
    pos = json.find(':', pos);
    if (pos == std::string::npos) return defaultVal;
    pos++;
    while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\"')) pos++;
    size_t endPos = pos;
    while (endPos < json.size() && (isdigit(json[endPos]) || json[endPos] == '.' || json[endPos] == '-' || json[endPos] == 'e' || json[endPos] == 'E' || json[endPos] == '+')) {
        endPos++;
    }
    if (endPos == pos) return defaultVal;
    try {
        return std::stod(json.substr(pos, endPos - pos));
    } catch (...) {
        return defaultVal;
    }
}

bool ExtractJsonBool(const std::string& json, const std::string& key, bool defaultVal = false) {
    std::string search = "\"" + key + "\"";
    size_t pos = json.find(search);
    if (pos == std::string::npos) return defaultVal;
    pos = json.find(':', pos);
    if (pos == std::string::npos) return defaultVal;
    size_t truePos = json.find("true", pos);
    size_t falsePos = json.find("false", pos);
    size_t commaPos = json.find(',', pos);
    size_t bracePos = json.find('}', pos);
    size_t limit = (std::min)(commaPos != std::string::npos ? commaPos : 999999ULL,
                              bracePos != std::string::npos ? bracePos : 999999ULL);

    if (truePos != std::string::npos && truePos < limit) return true;
    if (falsePos != std::string::npos && falsePos < limit) return false;
    return defaultVal;
}

PenInputPacket ParsePayload(const std::string& json) {
    PenInputPacket p;
    p.type = ExtractJsonString(json, "type");
    p.x = ExtractJsonNumber(json, "x", 0.0);
    p.y = ExtractJsonNumber(json, "y", 0.0);
    p.pressure = ExtractJsonNumber(json, "pressure", 0.0);
    p.tilt_x = ExtractJsonNumber(json, "tilt_x", 0.0);
    p.tilt_y = ExtractJsonNumber(json, "tilt_y", 0.0);
    p.altitude = ExtractJsonNumber(json, "altitude", -1.0);
    p.azimuth = ExtractJsonNumber(json, "azimuth", -1.0);
    p.roll = ExtractJsonNumber(json, "roll", 0.0);
    p.has_roll = (json.find("\"roll\"") != std::string::npos);
    p.squeeze = ExtractJsonBool(json, "squeeze", false);
    p.barrel = ExtractJsonBool(json, "barrel", false);
    p.eraser = ExtractJsonBool(json, "eraser", false);
    return p;
}

void SimulateUndoKey() {
    INPUT inputs[4] = {};
    inputs[0].type = INPUT_KEYBOARD;
    inputs[0].ki.wVk = VK_CONTROL;

    inputs[1].type = INPUT_KEYBOARD;
    inputs[1].ki.wVk = 'Z';

    inputs[2].type = INPUT_KEYBOARD;
    inputs[2].ki.wVk = 'Z';
    inputs[2].ki.dwFlags = KEYEVENTF_KEYUP;

    inputs[3].type = INPUT_KEYBOARD;
    inputs[3].ki.wVk = VK_CONTROL;
    inputs[3].ki.dwFlags = KEYEVENTF_KEYUP;

    SendInput(4, inputs, sizeof(INPUT));
}

int main(int argc, char* argv[]) {
    int port = 8080;
    std::cout << "===================================================================\n";
    std::cout << "   Windows Ink Host for iPad Drawing & Apple Pencil Pro (C++)\n";
    std::cout << "===================================================================\n\n";

    // 1. Enable Per-Monitor V2 DPI awareness
    HMODULE hUser32 = GetModuleHandleW(L"user32.dll");
    if (!hUser32) hUser32 = LoadLibraryW(L"user32.dll");
    if (hUser32) {
        PFN_SetProcessDpiAwarenessContext fnSetDpi =
            (PFN_SetProcessDpiAwarenessContext)GetProcAddress(hUser32, "SetProcessDpiAwarenessContext");
        if (fnSetDpi) {
            fnSetDpi(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
    }

    // 2. Resolve Synthetic Pointer APIs
    PFN_CreateSyntheticPointerDevice fnCreateSyntheticPointerDevice =
        (PFN_CreateSyntheticPointerDevice)GetProcAddress(hUser32, "CreateSyntheticPointerDevice");
    PFN_InjectSyntheticPointerInput fnInjectSyntheticPointerInput =
        (PFN_InjectSyntheticPointerInput)GetProcAddress(hUser32, "InjectSyntheticPointerInput");
    PFN_DestroySyntheticPointerDevice fnDestroySyntheticPointerDevice =
        (PFN_DestroySyntheticPointerDevice)GetProcAddress(hUser32, "DestroySyntheticPointerDevice");

    if (!fnCreateSyntheticPointerDevice || !fnInjectSyntheticPointerInput || !fnDestroySyntheticPointerDevice) {
        std::cerr << "[Error] Windows Synthetic Pointer API is not supported on this Windows version.\n";
        return 1;
    }

    // Create Synthetic Pen Pointer Device
    HSYNTHETICPOINTERDEVICE hDevice = fnCreateSyntheticPointerDevice(PT_PEN, 1, POINTER_FEEDBACK_DEFAULT);
    if (!hDevice) {
        std::cerr << "[Error] Failed to create synthetic pointer device. Win32 Error: " << GetLastError() << "\n";
        return 1;
    }
    std::cout << "[Init] Synthetic Pen device initialized successfully.\n";

    // 3. Initialize Winsock UDP Server
    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        std::cerr << "[Error] WSAStartup failed.\n";
        fnDestroySyntheticPointerDevice(hDevice);
        return 1;
    }

    SOCKET udpSocket = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (udpSocket == INVALID_SOCKET) {
        std::cerr << "[Error] Socket creation failed.\n";
        WSACleanup();
        fnDestroySyntheticPointerDevice(hDevice);
        return 1;
    }

    // Disable SIO_UDP_CONNRESET (10054) on Windows
    DWORD dwBytesReturned = 0;
    BOOL bNewBehavior = FALSE;
    WSAIoctl(udpSocket, _WSAIOW(IOC_VENDOR, 12), &bNewBehavior, sizeof(bNewBehavior),
             NULL, 0, &dwBytesReturned, NULL, NULL);

    // Set 1MB receive buffer for 240Hz packets
    int rcvBuf = 1024 * 1024;
    setsockopt(udpSocket, SOL_SOCKET, SO_RCVBUF, (char*)&rcvBuf, sizeof(rcvBuf));

    sockaddr_in serverAddr{};
    serverAddr.sin_family = AF_INET;
    serverAddr.sin_port = htons(port);
    serverAddr.sin_addr.s_addr = INADDR_ANY;

    if (bind(udpSocket, (sockaddr*)&serverAddr, sizeof(serverAddr)) == SOCKET_ERROR) {
        std::cerr << "[Error] Bind failed on port " << port << ". Win32 Error: " << WSAGetLastError() << "\n";
        closesocket(udpSocket);
        WSACleanup();
        fnDestroySyntheticPointerDevice(hDevice);
        return 1;
    }

    int screenWidth = GetSystemMetrics(SM_CXSCREEN);
    int screenHeight = GetSystemMetrics(SM_CYSCREEN);
    std::cout << "[Display] Primary Monitor Resolution: " << screenWidth << " x " << screenHeight << "\n";
    std::cout << "[Server] UDP Server listening on port " << port << "...\n";
    std::cout << "[Ready] In drawing apps (Photoshop/Krita), enable Windows Ink. Press Ctrl+C to stop.\n\n";

    // Tracking state
    enum class PenState { OutOfRange, Hovering, InContact };
    PenState currentState = PenState::OutOfRange;
    UINT32 frameId = 1;
    bool eraserToggled = false;
    bool prevSqueeze = false;

    char buffer[4096];
    sockaddr_in clientAddr{};
    int clientAddrLen = sizeof(clientAddr);

    while (true) {
        int bytesRead = recvfrom(udpSocket, buffer, sizeof(buffer) - 1, 0,
                                 (sockaddr*)&clientAddr, &clientAddrLen);
        if (bytesRead <= 0) continue;

        buffer[bytesRead] = '\0';
        std::string jsonStr(buffer);
        PenInputPacket p = ParsePayload(jsonStr);

        // Apple Pencil Pro Squeeze: Toggle Eraser or trigger hotkey
        if (p.squeeze && !prevSqueeze) {
            eraserToggled = !eraserToggled;
            std::cout << "[Pencil Pro Squeeze] Eraser toggled: " << (eraserToggled ? "ON" : "OFF") << "\n";
        }
        prevSqueeze = p.squeeze;

        // Coordinate Mapping (Normalized -> Screen Pixels)
        int targetX = 0;
        int targetY = 0;
        if (p.x > 1.0 || p.y > 1.0) {
            targetX = (int)p.x;
            targetY = (int)p.y;
        } else {
            targetX = (int)std::round(p.x * (screenWidth - 1));
            targetY = (int)std::round(p.y * (screenHeight - 1));
        }

        // Pressure (0.0 to 1.0 -> 0 to 1024)
        UINT32 winPressure = (UINT32)std::clamp((int)std::round(p.pressure * 1024.0), 0, 1024);

        // Tilt calculation
        int tiltX = (int)std::clamp(std::round(p.tilt_x), -90.0, 90.0);
        int tiltY = (int)std::clamp(std::round(p.tilt_y), -90.0, 90.0);
        if (p.altitude >= 0 && p.azimuth >= 0) {
            double sinTiltX = std::cos(p.altitude) * std::cos(p.azimuth);
            tiltX = (int)std::clamp(std::round(std::asin(sinTiltX) * 180.0 / 3.1415926535), -90.0, 90.0);
            double sinTiltY = std::cos(p.altitude) * std::sin(p.azimuth);
            tiltY = (int)std::clamp(std::round(std::asin(sinTiltY) * 180.0 / 3.1415926535), -90.0, 90.0);
        }

        // Apple Pencil Pro Barrel Roll (0 to 359 degrees)
        UINT32 rotation = 0;
        if (p.has_roll) {
            double r = std::fmod(p.roll, 360.0);
            if (r < 0) r += 360.0;
            rotation = (UINT32)std::clamp((int)std::round(r), 0, 359);
        }

        // Contact determination
        bool isContact = (winPressure > 0) || (p.type == "down") || (p.type == "move");
        if (p.type == "up" || p.type == "cancel") {
            isContact = false;
            winPressure = 0;
        }

        DWORD pointerFlags = POINTER_FLAG_NONE;
        if (currentState == PenState::OutOfRange) {
            if (isContact) {
                pointerFlags = POINTER_FLAG_INRANGE | POINTER_FLAG_INCONTACT | POINTER_FLAG_DOWN;
                currentState = PenState::InContact;
            } else {
                pointerFlags = POINTER_FLAG_INRANGE | POINTER_FLAG_UPDATE;
                currentState = PenState::Hovering;
            }
        } else if (currentState == PenState::Hovering) {
            if (isContact) {
                pointerFlags = POINTER_FLAG_INRANGE | POINTER_FLAG_INCONTACT | POINTER_FLAG_DOWN;
                currentState = PenState::InContact;
            } else {
                pointerFlags = POINTER_FLAG_INRANGE | POINTER_FLAG_UPDATE;
            }
        } else if (currentState == PenState::InContact) {
            if (isContact) {
                pointerFlags = POINTER_FLAG_INRANGE | POINTER_FLAG_INCONTACT | POINTER_FLAG_UPDATE;
            } else {
                pointerFlags = POINTER_FLAG_INRANGE | POINTER_FLAG_UP;
                currentState = PenState::Hovering;
            }
        }

        // Construct POINTER_TYPE_INFO
        POINTER_TYPE_INFO typeInfo{};
        typeInfo.type = PT_PEN;

        typeInfo.penInfo.pointerInfo.pointerType = PT_PEN;
        typeInfo.penInfo.pointerInfo.pointerId = 1;
        typeInfo.penInfo.pointerInfo.frameId = frameId++;
        typeInfo.penInfo.pointerInfo.pointerFlags = pointerFlags;
        typeInfo.penInfo.pointerInfo.ptPixelLocation.x = targetX;
        typeInfo.penInfo.pointerInfo.ptPixelLocation.y = targetY;
        typeInfo.penInfo.pointerInfo.ptHimetricLocation.x = targetX;
        typeInfo.penInfo.pointerInfo.ptHimetricLocation.y = targetY;

        typeInfo.penInfo.penMask = PEN_MASK_PRESSURE | PEN_MASK_TILT_X | PEN_MASK_TILT_Y;
        if (p.has_roll) {
            typeInfo.penInfo.penMask |= PEN_MASK_ROTATION;
        }

        typeInfo.penInfo.pressure = winPressure;
        typeInfo.penInfo.tiltX = tiltX;
        typeInfo.penInfo.tiltY = tiltY;
        typeInfo.penInfo.rotation = rotation;

        if (p.barrel) {
            typeInfo.penInfo.penFlags |= PEN_FLAG_BARREL;
        }
        if (eraserToggled || p.eraser) {
            typeInfo.penInfo.penFlags |= PEN_FLAG_ERASER | PEN_FLAG_INVERTED;
        }

        fnInjectSyntheticPointerInput(hDevice, &typeInfo, 1);
    }

    closesocket(udpSocket);
    WSACleanup();
    fnDestroySyntheticPointerDevice(hDevice);
    return 0;
}
