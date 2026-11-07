using System;

namespace IPadDrawingHost.Input;

/// <summary>
/// Low-latency adaptive filter for pen coordinates, pressure, and tilt.
/// Reduces Wi-Fi packet jitter while maintaining instantaneous response for quick strokes.
/// </summary>
public class InputFilter
{
    private bool _initialized;
    private double _filteredX;
    private double _filteredY;
    private double _filteredPressure;

    /// <summary>
    /// Jitter filter weight [0.0 = no smoothing, 1.0 = heavy smoothing].
    /// Default is 0.15 for minimal latency while removing quantization steps.
    /// </summary>
    public double SmoothingFactor { get; set; } = 0.15;

    public void Reset()
    {
        _initialized = false;
    }

    public (double x, double y, double pressure) Filter(double rawX, double rawY, double rawPressure)
    {
        if (!_initialized)
        {
            _filteredX = rawX;
            _filteredY = rawY;
            _filteredPressure = rawPressure;
            _initialized = true;
            return (rawX, rawY, rawPressure);
        }

        // Adaptive smoothing: if movement is very fast, reduce smoothing to prevent lag
        double dx = rawX - _filteredX;
        double dy = rawY - _filteredY;
        double distSq = dx * dx + dy * dy;

        double alpha = SmoothingFactor;
        if (distSq > 0.005) // Fast flick or quick stroke
        {
            alpha = Math.Min(1.0, alpha * 2.5);
        }

        _filteredX += (rawX - _filteredX) * (1.0 - alpha);
        _filteredY += (rawY - _filteredY) * (1.0 - alpha);
        _filteredPressure += (rawPressure - _filteredPressure) * (1.0 - (alpha * 0.5));

        return (_filteredX, _filteredY, _filteredPressure);
    }
}
