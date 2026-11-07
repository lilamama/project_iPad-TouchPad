using System;
using System.Runtime.InteropServices;

namespace IPadDrawingHost.Native;

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;

    public POINT(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() => $"({X}, {Y})";
}

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

[StructLayout(LayoutKind.Sequential)]
public struct PointerInfo
{
    public PointerInputType PointerType;
    public uint PointerId;
    public uint FrameId;
    public PointerFlags PointerFlags;
    public IntPtr SourceDeviceHandle;
    public IntPtr HwndTarget;
    public POINT PtPixelLocation;
    public POINT PtHimetricLocation;
    public POINT PtPixelLocationRaw;
    public POINT PtHimetricLocationRaw;
    public uint DwTime;
    public uint HistoryCount;
    public int InputData;
    public uint KeyStates;
    public ulong PerformanceCount;
    public PointerButtonChangeType ButtonChangeType;
}

[StructLayout(LayoutKind.Sequential)]
public struct PointerPenInfo
{
    public PointerInfo PointerInfo;
    public PenFlags PenFlags;
    public PenMask PenMask;
    public uint Pressure;
    public uint Rotation;
    public int TiltX;
    public int TiltY;
}

[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct PointerTypeInfo
{
    [FieldOffset(0)]
    public PointerInputType Type;

    [FieldOffset(8)]
    public PointerPenInfo PenInfo;
}

public static class Win32Pointer
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CreateSyntheticPointerDevice(
        PointerInputType pointerType,
        uint maxCount,
        PointerFeedbackMode mode);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool InjectSyntheticPointerInput(
        IntPtr device,
        [In] PointerTypeInfo[] pointerInfo,
        uint count);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern void DestroySyntheticPointerDevice(IntPtr device);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiFlag);

    public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = (IntPtr)(-4);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;
    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

    // SendInput definitions for shortcut mapping (e.g. Squeeze -> Undo Ctrl+Z)
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public MOUSEKEYBDHARDWAREINPUT mkhi;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct MOUSEKEYBDHARDWAREINPUT
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_Z = 0x5A;

    public static void SimulateUndo()
    {
        INPUT[] inputs = new INPUT[4];
        // Ctrl down
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].mkhi.ki.wVk = VK_CONTROL;

        // Z down
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].mkhi.ki.wVk = VK_Z;

        // Z up
        inputs[2].type = INPUT_KEYBOARD;
        inputs[2].mkhi.ki.wVk = VK_Z;
        inputs[2].mkhi.ki.dwFlags = KEYEVENTF_KEYUP;

        // Ctrl up
        inputs[3].type = INPUT_KEYBOARD;
        inputs[3].mkhi.ki.wVk = VK_CONTROL;
        inputs[3].mkhi.ki.dwFlags = KEYEVENTF_KEYUP;

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
}
