using System;
using System.Text.Json.Serialization;

namespace IPadDrawingHost.Network;

public class PenPayload
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("pressure")]
    public double Pressure { get; set; }

    [JsonPropertyName("tilt_x")]
    public double? TiltX { get; set; }

    [JsonPropertyName("tilt_y")]
    public double? TiltY { get; set; }

    [JsonPropertyName("altitude")]
    public double? Altitude { get; set; }

    [JsonPropertyName("azimuth")]
    public double? Azimuth { get; set; }

    [JsonPropertyName("roll")]
    public double? Roll { get; set; }

    [JsonPropertyName("roll_radians")]
    public double? RollRadians { get; set; }

    [JsonPropertyName("squeeze")]
    public bool Squeeze { get; set; }

    [JsonPropertyName("barrel")]
    public bool Barrel { get; set; }

    [JsonPropertyName("eraser")]
    public bool Eraser { get; set; }

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; set; }

    /// <summary>
    /// Computes TiltX (-90 to 90 degrees) using direct tilt_x or altitude/azimuth.
    /// In iOS: altitude is angle from screen surface (0 to pi/2),
    /// azimuth is angle from x-axis (0 to 2*pi).
    /// </summary>
    public int GetResolvedTiltX()
    {
        if (TiltX.HasValue)
        {
            return (int)Math.Clamp(Math.Round(TiltX.Value), -90, 90);
        }

        if (Altitude.HasValue && Azimuth.HasValue)
        {
            // Spherical coordinate conversion to Windows tiltX:
            // tiltX is angle between y-z plane and stylus
            double alt = Altitude.Value;
            double azm = Azimuth.Value;
            double sinTiltX = Math.Cos(alt) * Math.Cos(azm);
            double tiltRad = Math.Asin(Math.Clamp(sinTiltX, -1.0, 1.0));
            return (int)Math.Clamp(Math.Round(tiltRad * 180.0 / Math.PI), -90, 90);
        }

        return 0;
    }

    /// <summary>
    /// Computes TiltY (-90 to 90 degrees) using direct tilt_y or altitude/azimuth.
    /// </summary>
    public int GetResolvedTiltY()
    {
        if (TiltY.HasValue)
        {
            return (int)Math.Clamp(Math.Round(TiltY.Value), -90, 90);
        }

        if (Altitude.HasValue && Azimuth.HasValue)
        {
            // tiltY is angle between x-z plane and stylus
            double alt = Altitude.Value;
            double azm = Azimuth.Value;
            double sinTiltY = Math.Cos(alt) * Math.Sin(azm);
            double tiltRad = Math.Asin(Math.Clamp(sinTiltY, -1.0, 1.0));
            return (int)Math.Clamp(Math.Round(tiltRad * 180.0 / Math.PI), -90, 90);
        }

        return 0;
    }

    /// <summary>
    /// Resolves Apple Pencil Pro barrel roll to 0-359 degrees.
    /// </summary>
    public uint GetResolvedRotation()
    {
        if (Roll.HasValue)
        {
            double r = Roll.Value % 360.0;
            if (r < 0) r += 360.0;
            return (uint)Math.Clamp((int)Math.Round(r), 0, 359);
        }

        if (RollRadians.HasValue)
        {
            double deg = (RollRadians.Value * 180.0 / Math.PI) % 360.0;
            if (deg < 0) deg += 360.0;
            return (uint)Math.Clamp((int)Math.Round(deg), 0, 359);
        }

        return 0;
    }

    /// <summary>
    /// Resolves normalized pressure (0.0 to 1.0) to Windows Ink 0..1024 range.
    /// </summary>
    public uint GetResolvedPressure()
    {
        double clamped = Math.Clamp(Pressure, 0.0, 1.0);
        return (uint)Math.Clamp(Math.Round(clamped * 1024.0), 0, 1024);
    }
}
