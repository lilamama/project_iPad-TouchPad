using System;
using System.Runtime.InteropServices;
using System.Threading;
using IPadDrawingHost.Native;
using IPadDrawingHost.Network;

namespace IPadDrawingHost.Input;

public enum SqueezeAction
{
    EraserToggle,
    EraserHold,
    BarrelButton,
    UndoShortcut
}

public class PenStateEngine : IDisposable
{
    private IntPtr _syntheticDevice = IntPtr.Zero;
    private readonly CoordinateMapper _mapper;
    private readonly InputFilter _filter;
    private readonly object _lock = new();

    private enum PenState
    {
        OutOfRange,
        Hovering,
        InContact
    }

    private PenState _currentState = PenState.OutOfRange;
    private uint _frameId = 1;
    private bool _eraserActive = false;
    private bool _prevSqueeze = false;
    private DateTime _lastPacketTime = DateTime.UtcNow;
    private readonly Timer _watchdogTimer;

    public SqueezeAction SqueezeBehavior { get; set; } = SqueezeAction.EraserToggle;
    public bool SmoothingEnabled { get; set; } = true;
    public double PressureSensitivity { get; set; } = 1.0; // 1.0 = linear

    public PenStateEngine(CoordinateMapper mapper)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _filter = new InputFilter();

        InitializeDevice();

        // 50ms watchdog timer to release pen if UDP connection drops during stroke
        _watchdogTimer = new Timer(WatchdogCallback, null, 100, 50);
    }

    private void InitializeDevice()
    {
        // PT_PEN = 3, maxCount = 1, Feedback = Default (provides cursor feedback in drawing apps)
        _syntheticDevice = Win32Pointer.CreateSyntheticPointerDevice(
            PointerInputType.Pen,
            1,
            PointerFeedbackMode.Default
        );

        if (_syntheticDevice == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"Failed to create Synthetic Pointer Device for Pen. Win32 Error: {err}. " +
                "Ensure your system supports Windows 8/10/11 Pointer Injection."
            );
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[Win32 Pen Engine] Synthetic Pen Pointer Device created successfully.");
        Console.ResetColor();
    }

    public void ProcessPayload(PenPayload payload)
    {
        lock (_lock)
        {
            _lastPacketTime = DateTime.UtcNow;

            // Handle Squeeze Actions (Apple Pencil Pro)
            HandleSqueeze(payload.Squeeze);

            // Filter inputs if smoothing is enabled
            double x = payload.X;
            double y = payload.Y;
            double pressure = payload.Pressure;

            if (SmoothingEnabled)
            {
                (x, y, pressure) = _filter.Filter(x, y, pressure);
            }

            // Apply custom pressure sensitivity curve
            if (Math.Abs(PressureSensitivity - 1.0) > 0.001 && pressure > 0)
            {
                pressure = Math.Pow(pressure, PressureSensitivity);
            }

            POINT targetPixel = _mapper.MapCoordinates(x, y);

            uint resolvedPressure = (uint)Math.Clamp(Math.Round(pressure * 1024.0), 0, 1024);
            int resolvedTiltX = payload.GetResolvedTiltX();
            int resolvedTiltY = payload.GetResolvedTiltY();
            uint resolvedRotation = payload.GetResolvedRotation();

            bool isContact = resolvedPressure > 0 || string.Equals(payload.Type, "down", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(payload.Type, "move", StringComparison.OrdinalIgnoreCase);

            if (string.Equals(payload.Type, "up", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(payload.Type, "cancel", StringComparison.OrdinalIgnoreCase))
            {
                isContact = false;
                resolvedPressure = 0;
            }

            // Transition logic based on current state and incoming contact request
            switch (_currentState)
            {
                case PenState.OutOfRange:
                    if (isContact)
                    {
                        // In Windows Ink, transition: OutOfRange -> InRange/Down
                        InjectPen(targetPixel, resolvedPressure, resolvedTiltX, resolvedTiltY, resolvedRotation,
                            PointerFlags.InRange | PointerFlags.InContact | PointerFlags.Down,
                            payload.Barrel, _eraserActive || payload.Eraser);
                        _currentState = PenState.InContact;
                    }
                    else
                    {
                        // Hover enter
                        InjectPen(targetPixel, 0, resolvedTiltX, resolvedTiltY, resolvedRotation,
                            PointerFlags.InRange | PointerFlags.Update,
                            payload.Barrel, _eraserActive || payload.Eraser);
                        _currentState = PenState.Hovering;
                    }
                    break;

                case PenState.Hovering:
                    if (isContact)
                    {
                        // Pen touches screen
                        InjectPen(targetPixel, resolvedPressure, resolvedTiltX, resolvedTiltY, resolvedRotation,
                            PointerFlags.InRange | PointerFlags.InContact | PointerFlags.Down,
                            payload.Barrel, _eraserActive || payload.Eraser);
                        _currentState = PenState.InContact;
                    }
                    else
                    {
                        // Pen hovering and moving
                        InjectPen(targetPixel, 0, resolvedTiltX, resolvedTiltY, resolvedRotation,
                            PointerFlags.InRange | PointerFlags.Update,
                            payload.Barrel, _eraserActive || payload.Eraser);
                    }
                    break;

                case PenState.InContact:
                    if (isContact)
                    {
                        // Pen drawing / dragging
                        InjectPen(targetPixel, resolvedPressure, resolvedTiltX, resolvedTiltY, resolvedRotation,
                            PointerFlags.InRange | PointerFlags.InContact | PointerFlags.Update,
                            payload.Barrel, _eraserActive || payload.Eraser);
                    }
                    else
                    {
                        // Pen lifted off glass
                        InjectPen(targetPixel, 0, resolvedTiltX, resolvedTiltY, resolvedRotation,
                            PointerFlags.InRange | PointerFlags.Up,
                            payload.Barrel, _eraserActive || payload.Eraser);
                        _currentState = PenState.Hovering;
                        _filter.Reset();
                    }
                    break;
            }
        }
    }

    private void HandleSqueeze(bool squeezeState)
    {
        // Detect rising edge of squeeze trigger
        if (squeezeState && !_prevSqueeze)
        {
            switch (SqueezeBehavior)
            {
                case SqueezeAction.EraserToggle:
                    _eraserActive = !_eraserActive;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Pencil Pro Squeeze] Eraser mode toggled: {(_eraserActive ? "ON" : "OFF")}");
                    Console.ResetColor();
                    break;

                case SqueezeAction.UndoShortcut:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("[Pencil Pro Squeeze] Triggering Undo (Ctrl+Z)...");
                    Console.ResetColor();
                    Win32Pointer.SimulateUndo();
                    break;
            }
        }

        if (SqueezeBehavior == SqueezeAction.EraserHold)
        {
            _eraserActive = squeezeState;
        }

        _prevSqueeze = squeezeState;
    }

    private void InjectPen(
        POINT pt,
        uint pressure,
        int tiltX,
        int tiltY,
        uint rotation,
        PointerFlags pointerFlags,
        bool barrel,
        bool eraser)
    {
        if (_syntheticDevice == IntPtr.Zero) return;

        PenFlags penFlags = PenFlags.None;
        if (barrel || (SqueezeBehavior == SqueezeAction.BarrelButton && _prevSqueeze))
        {
            penFlags |= PenFlags.Barrel;
        }

        if (eraser)
        {
            penFlags |= PenFlags.Eraser | PenFlags.Inverted;
        }

        PenMask penMask = PenMask.Pressure | PenMask.TiltX | PenMask.TiltY;
        if (rotation > 0)
        {
            penMask |= PenMask.Rotation;
        }

        PointerTypeInfo[] typeInfo = new PointerTypeInfo[1];
        typeInfo[0].Type = PointerInputType.Pen;

        // PointerInfo setup
        typeInfo[0].PenInfo.PointerInfo.PointerType = PointerInputType.Pen;
        typeInfo[0].PenInfo.PointerInfo.PointerId = 1;
        typeInfo[0].PenInfo.PointerInfo.FrameId = _frameId++;
        typeInfo[0].PenInfo.PointerInfo.PointerFlags = pointerFlags;
        typeInfo[0].PenInfo.PointerInfo.PtPixelLocation = pt;
        typeInfo[0].PenInfo.PointerInfo.PtHimetricLocation = pt;
        typeInfo[0].PenInfo.PointerInfo.PtPixelLocationRaw = pt;
        typeInfo[0].PenInfo.PointerInfo.PtHimetricLocationRaw = pt;

        // Pen-specific info
        typeInfo[0].PenInfo.PenFlags = penFlags;
        typeInfo[0].PenInfo.PenMask = penMask;
        typeInfo[0].PenInfo.Pressure = Math.Min(pressure, 1024);
        typeInfo[0].PenInfo.TiltX = Math.Clamp(tiltX, -90, 90);
        typeInfo[0].PenInfo.TiltY = Math.Clamp(tiltY, -90, 90);
        typeInfo[0].PenInfo.Rotation = Math.Clamp(rotation, 0, 359);

        bool success = Win32Pointer.InjectSyntheticPointerInput(_syntheticDevice, typeInfo, 1);
        if (!success)
        {
            int err = Marshal.GetLastWin32Error();
            // Error 87 is ERROR_INVALID_PARAMETER (usually flags transition mismatch)
            // Error 5 is Access Denied (if elevated window has focus and host is unelevated)
            if (err != 0)
            {
                // Silently or verbosely handle when needed
            }
        }
    }

    private void WatchdogCallback(object? state)
    {
        lock (_lock)
        {
            if (_currentState == PenState.OutOfRange) return;

            var elapsed = (DateTime.UtcNow - _lastPacketTime).TotalMilliseconds;
            if (elapsed > 100)
            {
                // Packet timeout: cleanly release contact and return to out of range
                if (_currentState == PenState.InContact)
                {
                    var bounds = _mapper.GetTargetBounds();
                    POINT fallbackPt = new POINT(bounds.left + bounds.width / 2, bounds.top + bounds.height / 2);
                    InjectPen(fallbackPt, 0, 0, 0, 0, PointerFlags.InRange | PointerFlags.Up, false, false);
                }

                _currentState = PenState.OutOfRange;
                _filter.Reset();
            }
        }
    }

    public void Dispose()
    {
        _watchdogTimer.Dispose();
        lock (_lock)
        {
            if (_syntheticDevice != IntPtr.Zero)
            {
                Win32Pointer.DestroySyntheticPointerDevice(_syntheticDevice);
                _syntheticDevice = IntPtr.Zero;
            }
        }
        GC.SuppressFinalize(this);
    }
}
