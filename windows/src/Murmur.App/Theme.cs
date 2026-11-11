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
        /// <summary>Each bar glides to its new height instead of jumping (the mac's level animation).</summary>
        public static readonly Duration LevelAnimation = new(TimeSpan.FromMilliseconds(80));
    }

    public static class Window
    {
        public const double Width = 920;
        public const double Height = 660;
        public const double MinWidth = 780;
        public const double MinHeight = 540;
        public const double SidebarWidth = 210;
        public const double SidebarItemPadding = 8;
        public const double ContentPadding = 24;
        public const double SectionSpacing = 24;
        public const double RowSpacing = 10;
        public const double LabelGap = 16;
        public const double TitleFontSize = 26;
        public const double HeaderFontSize = 14;
        public const double CaptionFontSize = 12;
        public const double CardPadding = 16;
        public const double CardCornerRadius = 8;
        /// <summary>Dictations on Home.</summary>
        public const int RecentCount = 5;
        /// <summary>History renders this many rows at most; search finds older ones.</summary>
        public const int HistoryPageSize = 200;
        /// <summary>Height of a dictation preview on Home (about three lines); History shows it all.</summary>
        public const double PreviewHeight = 60;
        public const double FieldWidth = 320;
        public const double NarrowFieldWidth = 56;
    }

    public static class StatCard
    {
        public const double MinWidth = 170;
        public const double Spacing = 12;
        public const double ValueFontSize = 26;
    }

    public static class Typing
    {
        public const int MinWordsPerMinute = 10;
        public const int MaxWordsPerMinute = 250;
    }

    public static class PositionPicker
    {
        public const double ScreenWidth = 300;
        public const double ScreenHeight = 186;
        public const double ScreenCornerRadius = 12;
        public const double CellSpacing = 6;
        public const double PillWidth = 46;
        public const double PillHeight = 16;
        public const double DotSize = 6;
        public const double IdleOpacity = 0.25;
    }

    public static class Icons
    {
        /// <summary>Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on Windows 10 (same code points).</summary>
        public static readonly FontFamily Font = new("Segoe Fluent Icons, Segoe MDL2 Assets");
        public const string Lock = "\uE72E";
        public const string Warning = "\uE7BA";
        public const string Home = "\uE80F";
        public const string History = "\uE81C";
        public const string Vocabulary = "\uE82D";
        public const string Cleanup = "\uE70F";
        public const string Microphone = "\uE720";
        public const string Keyboard = "\uE765";
        public const string Keys = "\uE8D7";
        public const string Copy = "\uE8C8";
        public const string Remove = "\uE738";
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
