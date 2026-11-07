using System;
using IPadDrawingHost.Native;

namespace IPadDrawingHost.Input;

public enum ScreenMappingMode
{
    PrimaryScreen,
    VirtualDesktop,
    CustomRect
}

public class CoordinateMapper
{
    public ScreenMappingMode Mode { get; set; } = ScreenMappingMode.PrimaryScreen;
    public bool MaintainAspectRatio { get; set; } = true;
    public double SourceAspectRatio { get; set; } = 4.0 / 3.0; // Standard iPad aspect ratio

    public int CustomLeft { get; set; } = 0;
    public int CustomTop { get; set; } = 0;
    public int CustomWidth { get; set; } = 1920;
    public int CustomHeight { get; set; } = 1080;

    public (int left, int top, int width, int height) GetTargetBounds()
    {
        return Mode switch
        {
            ScreenMappingMode.VirtualDesktop => (
                Win32Pointer.GetSystemMetrics(Win32Pointer.SM_XVIRTUALSCREEN),
                Win32Pointer.GetSystemMetrics(Win32Pointer.SM_YVIRTUALSCREEN),
                Win32Pointer.GetSystemMetrics(Win32Pointer.SM_CXVIRTUALSCREEN),
                Win32Pointer.GetSystemMetrics(Win32Pointer.SM_CYVIRTUALSCREEN)
            ),
            ScreenMappingMode.CustomRect => (
                CustomLeft,
                CustomTop,
                CustomWidth,
                CustomHeight
            ),
            _ => (
                0,
                0,
                Win32Pointer.GetSystemMetrics(Win32Pointer.SM_CXSCREEN),
                Win32Pointer.GetSystemMetrics(Win32Pointer.SM_CYSCREEN)
            )
        };
    }

    public POINT MapCoordinates(double x, double y)
    {
        var (targetLeft, targetTop, targetWidth, targetHeight) = GetTargetBounds();

        // If coordinates are already screen pixels (> 1.0)
        if (x > 1.0 || y > 1.0)
        {
            int px = (int)Math.Clamp(Math.Round(x), targetLeft, targetLeft + targetWidth - 1);
            int py = (int)Math.Clamp(Math.Round(y), targetTop, targetTop + targetHeight - 1);
            return new POINT(px, py);
        }

        // Clamp normalized coordinates
        double normX = Math.Clamp(x, 0.0, 1.0);
        double normY = Math.Clamp(y, 0.0, 1.0);

        if (!MaintainAspectRatio || SourceAspectRatio <= 0)
        {
            int screenX = targetLeft + (int)Math.Round(normX * (targetWidth - 1));
            int screenY = targetTop + (int)Math.Round(normY * (targetHeight - 1));
            return new POINT(screenX, screenY);
        }

        // Letterbox / Pillarbox mapping to maintain tablet aspect ratio
        double targetAspect = (double)targetWidth / targetHeight;
        double activeWidth, activeHeight;
        double offsetX, offsetY;

        if (targetAspect > SourceAspectRatio)
        {
            // Screen is wider than tablet: pillarbox (add black bars left & right)
            activeHeight = targetHeight;
            activeWidth = activeHeight * SourceAspectRatio;
            offsetX = (targetWidth - activeWidth) / 2.0;
            offsetY = 0;
        }
        else
        {
            // Screen is taller than tablet: letterbox (add black bars top & bottom)
            activeWidth = targetWidth;
            activeHeight = activeWidth / SourceAspectRatio;
            offsetX = 0;
            offsetY = (targetHeight - activeHeight) / 2.0;
        }

        int finalX = targetLeft + (int)Math.Round(offsetX + normX * (activeWidth - 1));
        int finalY = targetTop + (int)Math.Round(offsetY + normY * (activeHeight - 1));

        return new POINT(finalX, finalY);
    }
}
