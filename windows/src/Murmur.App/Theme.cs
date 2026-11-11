using System.Windows;
using System.Windows.Media;

namespace Murmur.App;

/// <summary>Single source of truth for sizes, colors and timings of the app's own UI (same values as the Mac's Theme.swift).</summary>
public static class Theme
{
    public static class Pill
    {
        /// <summary>The invisible, click-through canvas the pill sits in; large enough for its widest state.</summary>
        public const double CanvasWidth = 380;
        public const double CanvasHeight = 64;
        /// <summary>Distance from the screen's work area edges (taskbar excluded).</summary>
        public const double ScreenMargin = 12;
        public const double IdleWidth = 46;
        public const double IdleHeight = 24;
        public const double PillHeight = 30;
        public const double HorizontalPadding = 14;
        public const double ContentSpacing = 8;
        // a full pixel: a half-pixel hairline partly vanishes on 100% scaling
        public const double EdgeWidth = 1;
        public const double MessageFontSize = 13;
        public const double IconFontSize = 12;
        public const double SpinnerSize = 14;
        public const double SpinnerStroke = 2;
        public static readonly Duration SpinnerTurn = new(TimeSpan.FromMilliseconds(800));
        public static readonly Duration MorphDuration = new(TimeSpan.FromMilliseconds(280));

        // dynamic island look: near-solid black, a faint edge so it still reads on dark wallpapers
        private const byte BackgroundAlpha = 240;
        private const byte EdgeAlpha = 31;
        private const byte SecondaryAlpha = 153;
        public static readonly Brush Background = Frozen(Color.FromArgb(BackgroundAlpha, 0, 0, 0));
        public static readonly Brush Edge = Frozen(Color.FromArgb(EdgeAlpha, byte.MaxValue, byte.MaxValue, byte.MaxValue));
        public static readonly Brush Foreground = Brushes.White;
        public static readonly Brush Secondary = Frozen(Color.FromArgb(SecondaryAlpha, byte.MaxValue, byte.MaxValue, byte.MaxValue));

        /// <summary>The idle waveform glyph: bar heights from left to right.</summary>
        public static readonly IReadOnlyList<double> IdleGlyphBars = [4, 8, 11, 8, 4];
    }

    public static class VoiceBar
    {
        public const double BarWidth = 2;
        public const double BarSpacing = 2;
        public const double MinBarHeight = 2;
        public const double MaxBarHeight = 15;
    }

    public static class Icons
    {
        /// <summary>Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on Windows 10 (same code points).</summary>
        public static readonly FontFamily Font = new("Segoe Fluent Icons, Segoe MDL2 Assets");
        public const string Lock = "";
        public const string Warning = "";
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
