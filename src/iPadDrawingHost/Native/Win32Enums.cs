using System;

namespace IPadDrawingHost.Native;

[Flags]
public enum PointerFlags : uint
{
    None            = 0x00000000,
    New             = 0x00000001,
    InRange         = 0x00000002,
    InContact       = 0x00000004,
    FirstButton     = 0x00000010,
    SecondButton    = 0x00000020,
    ThirdButton     = 0x00000040,
    FourthButton    = 0x00000080,
    FifthButton     = 0x00000100,
    Primary         = 0x00002000,
    Confidence      = 0x00004000,
    Canceled        = 0x00008000,
    Down            = 0x00010000,
    Update          = 0x00020000,
    Up              = 0x00040000,
    Wheel           = 0x00080000,
    HWheel          = 0x00100000,
    CaptureChanged  = 0x00200000,
    HasTransform    = 0x00400000
}

[Flags]
public enum PenFlags : uint
{
    None     = 0x00000000,
    Barrel   = 0x00000001,
    Inverted = 0x00000002,
    Eraser   = 0x00000004
}

[Flags]
public enum PenMask : uint
{
    None     = 0x00000000,
    Pressure = 0x00000001,
    Rotation = 0x00000002,
    TiltX    = 0x00000004,
    TiltY    = 0x00000008
}

public enum PointerInputType : uint
{
    Pointer  = 0x00000001,
    Touch    = 0x00000002,
    Pen      = 0x00000003,
    Mouse    = 0x00000004,
    Touchpad = 0x00000005
}

public enum PointerFeedbackMode : uint
{
    Default  = 0x00000001,
    Indirect = 0x00000002,
    None     = 0x00000003
}

public enum PointerButtonChangeType : uint
{
    None = 0,
    FirstButtonDown,
    FirstButtonUp,
    SecondButtonDown,
    SecondButtonUp,
    ThirdButtonDown,
    ThirdButtonUp,
    FourthButtonDown,
    FourthButtonUp,
    FifthButtonDown,
    FifthButtonUp
}
