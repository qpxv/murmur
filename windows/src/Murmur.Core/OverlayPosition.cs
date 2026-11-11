namespace Murmur.Core;

/// <summary>Where the overlay pill sits on screen: one cell of a 3x3 grid.</summary>
public enum OverlayPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}

/// <summary>A rectangle in screen units (WPF device-independent pixels, top-left origin, y grows down).</summary>
public readonly record struct ScreenRect(double Left, double Top, double Width, double Height);

/// <summary>A point in screen units (top-left origin).</summary>
public readonly record struct ScreenPoint(double X, double Y);

/// <summary>Which column of the 3x3 grid.</summary>
public enum HorizontalEdge
{
    Leading,
    Center,
    Trailing,
}

/// <summary>Which row of the 3x3 grid.</summary>
public enum VerticalEdge
{
    Top,
    Middle,
    Bottom,
}

public static class OverlayPositions
{

    /// <summary>
    /// Top-left corner of a pill of the given size inside <paramref name="bounds"/> (the screen's
    /// work area), kept <paramref name="margin"/> away from the edges.
    /// </summary>
    public static ScreenPoint Origin(this OverlayPosition position, double width, double height, ScreenRect bounds, double margin)
    {
        var x = position.Horizontal() switch
        {
            HorizontalEdge.Leading => bounds.Left + margin,
            HorizontalEdge.Center => bounds.Left + ((bounds.Width - width) / 2),
            _ => bounds.Left + bounds.Width - margin - width,
        };
        var y = position.Vertical() switch
        {
            VerticalEdge.Top => bounds.Top + margin,
            VerticalEdge.Middle => bounds.Top + ((bounds.Height - height) / 2),
            _ => bounds.Top + bounds.Height - margin - height,
        };
        return new ScreenPoint(x, y);
    }

    public static HorizontalEdge Horizontal(this OverlayPosition position) => position switch
    {
        OverlayPosition.TopLeft or OverlayPosition.MiddleLeft or OverlayPosition.BottomLeft => HorizontalEdge.Leading,
        OverlayPosition.TopCenter or OverlayPosition.MiddleCenter or OverlayPosition.BottomCenter => HorizontalEdge.Center,
        _ => HorizontalEdge.Trailing,
    };

    public static VerticalEdge Vertical(this OverlayPosition position) => position switch
    {
        OverlayPosition.TopLeft or OverlayPosition.TopCenter or OverlayPosition.TopRight => VerticalEdge.Top,
        OverlayPosition.MiddleLeft or OverlayPosition.MiddleCenter or OverlayPosition.MiddleRight => VerticalEdge.Middle,
        _ => VerticalEdge.Bottom,
    };
}
