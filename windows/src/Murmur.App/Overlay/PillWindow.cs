using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Win32;
using Murmur.Core;
using Murmur.Services;

namespace Murmur.App.Overlay;

/// <summary>
/// The black pill (dynamic island style) in a borderless, transparent, topmost window that never
/// takes focus or clicks: a waveform glyph when idle, a live voice bar while recording, a spinner
/// while finishing, and a short message on errors. Nothing animates while idle.
/// </summary>
public sealed class PillWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly Border _pill;
    private readonly FrameworkElement _idleGlyph;
    private readonly StackPanel _recording;
    private readonly Rectangle[] _bars;
    private readonly TextBlock _lockIcon;
    private readonly FrameworkElement _spinner;
    private readonly Storyboard _spin;
    private readonly StackPanel _failure;
    private readonly TextBlock _failureText;

    public PillWindow(SettingsStore settings)
    {
        _settings = settings;
        // an empty style keeps the fluent theme's window style (backdrop, chrome) off the transparent overlay
        Style = new Style(typeof(Window));
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        ResizeMode = ResizeMode.NoResize;
        Width = Theme.Pill.CanvasWidth;
        Height = Theme.Pill.CanvasHeight;
        Title = AppConstants.AppName;

        _idleGlyph = IdleGlyph();
        _bars = [.. Enumerable.Range(0, AppConstants.Overlay.VoiceBarCount).Select(_ => Bar(Theme.VoiceBar.MinBarHeight))];
        var voiceBar = Row(Theme.VoiceBar.BarSpacing, _bars);
        _lockIcon = GlyphIcon(Theme.Icons.Lock, Theme.Pill.Secondary);
        _recording = Row(Theme.Pill.ContentSpacing, voiceBar, _lockIcon);
        (_spinner, _spin) = Spinner();
        _failureText = new TextBlock
        {
            Foreground = Theme.Pill.Foreground,
            FontSize = Theme.Pill.MessageFontSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        _failure = Row(Theme.Pill.ContentSpacing, GlyphIcon(Theme.Icons.Warning, Theme.Pill.Foreground), _failureText);

        var content = new Grid { VerticalAlignment = VerticalAlignment.Center };
        foreach (var part in new UIElement[] { _idleGlyph, _recording, _spinner, _failure })
        {
            content.Children.Add(part);
        }
        _pill = new Border
        {
            Background = Theme.Pill.Background,
            BorderBrush = Theme.Pill.Edge,
            BorderThickness = new Thickness(Theme.Pill.EdgeWidth),
            Child = content,
        };
        Content = _pill;

        Render(DictationState.Idle);
        settings.Changed += (_, _) => Reposition();
        SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(Reposition);
        Reposition();
    }

    /// <summary>Shows the pill for a dictation state. Call on the UI thread.</summary>
    public void Render(DictationState state)
    {
        var isIdle = state == DictationState.Idle;
        _pill.Height = isIdle ? Theme.Pill.IdleHeight : Theme.Pill.PillHeight;
        _pill.CornerRadius = new CornerRadius(_pill.Height / 2);
        _pill.Width = isIdle ? Theme.Pill.IdleWidth : double.NaN;
        _pill.Padding = new Thickness(isIdle ? 0 : Theme.Pill.HorizontalPadding, 0, isIdle ? 0 : Theme.Pill.HorizontalPadding, 0);

        _idleGlyph.Visibility = Visible(isIdle);
        _recording.Visibility = Visible(state is DictationState.RecordingState);
        _lockIcon.Visibility = Visible(state is DictationState.RecordingState { Mode: RecordingMode.Locked });
        _failure.Visibility = Visible(state is DictationState.FailedState);
        if (state is DictationState.FailedState failed)
        {
            _failureText.Text = failed.Error.Message();
        }

        // the spinner is the only animation, and it only runs while finishing
        var isFinishing = state == DictationState.Finishing;
        _spinner.Visibility = Visible(isFinishing);
        if (isFinishing)
        {
            _spin.Begin(_spinner, isControllable: true);
        }
        else
        {
            _spin.Stop(_spinner);
        }
        if (state.IsRecording)
        {
            ShowLevels([]);
        }
        // another topmost window (the taskbar, a video call) may have come up in the meantime
        Topmost = false;
        Topmost = true;
    }

    /// <summary>Most recent mic levels (0...1), oldest first; missing ones show as silence.</summary>
    public void ShowLevels(IReadOnlyList<float> levels)
    {
        var offset = _bars.Length - levels.Count;
        for (var index = 0; index < _bars.Length; index++)
        {
            var level = index >= offset ? levels[index - offset] : 0;
            _bars[index].Height = Theme.VoiceBar.MinBarHeight + ((Theme.VoiceBar.MaxBarHeight - Theme.VoiceBar.MinBarHeight) * level);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // never activates, lets every click through, and stays out of alt-tab
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeWindow.GetExtendedStyle(handle);
        NativeWindow.SetExtendedStyle(
            handle,
            style | NativeWindow.NoActivate | NativeWindow.Transparent | NativeWindow.ToolWindow);
    }

    private void Reposition()
    {
        var position = _settings.Current.OverlayPosition;
        var area = SystemParameters.WorkArea;
        var origin = position.Origin(
            Theme.Pill.CanvasWidth,
            Theme.Pill.CanvasHeight,
            new ScreenRect(area.Left, area.Top, area.Width, area.Height),
            Theme.Pill.ScreenMargin);
        Left = origin.X;
        Top = origin.Y;
        _pill.HorizontalAlignment = position.Horizontal() switch
        {
            HorizontalEdge.Leading => HorizontalAlignment.Left,
            HorizontalEdge.Center => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Right,
        };
        _pill.VerticalAlignment = position.Vertical() switch
        {
            VerticalEdge.Top => VerticalAlignment.Top,
            VerticalEdge.Middle => VerticalAlignment.Center,
            _ => VerticalAlignment.Bottom,
        };
    }

    private static Visibility Visible(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Collapsed;

    private static Rectangle Bar(double height) => new()
    {
        Width = Theme.VoiceBar.BarWidth,
        Height = height,
        RadiusX = Theme.VoiceBar.BarWidth / 2,
        RadiusY = Theme.VoiceBar.BarWidth / 2,
        Fill = Theme.Pill.Foreground,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static StackPanel Row(double spacing, params UIElement[] children)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        foreach (var child in children)
        {
            // wpf's StackPanel has no spacing: every child after the first gets a left margin
            if (row.Children.Count > 0 && child is FrameworkElement element)
            {
                element.Margin = new Thickness(spacing, 0, 0, 0);
            }
            row.Children.Add(child);
        }
        return row;
    }

    private static StackPanel IdleGlyph() =>
        Row(Theme.VoiceBar.BarSpacing, [.. Theme.Pill.IdleGlyphBars.Select(Bar)]);

    private static TextBlock GlyphIcon(string glyph, Brush foreground) => new()
    {
        Text = glyph,
        FontFamily = Theme.Icons.Font,
        FontSize = Theme.Pill.IconFontSize,
        Foreground = foreground,
        VerticalAlignment = VerticalAlignment.Center,
    };

    // a three-quarter ring turning once per SpinnerTurn
    private static (FrameworkElement Spinner, Storyboard Spin) Spinner()
    {
        var radius = (Theme.Pill.SpinnerSize - Theme.Pill.SpinnerStroke) / 2;
        var center = Theme.Pill.SpinnerSize / 2;
        var arc = new PathFigure { StartPoint = new Point(center, center - radius) };
        arc.Segments.Add(new ArcSegment(
            new Point(center - radius, center),
            new Size(radius, radius),
            rotationAngle: 0,
            isLargeArc: true,
            SweepDirection.Clockwise,
            isStroked: true));
        var rotation = new RotateTransform(0, center, center);
        var spinner = new System.Windows.Shapes.Path
        {
            Data = new PathGeometry([arc]),
            Stroke = Theme.Pill.Foreground,
            StrokeThickness = Theme.Pill.SpinnerStroke,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Width = Theme.Pill.SpinnerSize,
            Height = Theme.Pill.SpinnerSize,
            RenderTransform = rotation,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var turn = new DoubleAnimation(0, FullTurn, Theme.Pill.SpinnerTurn) { RepeatBehavior = RepeatBehavior.Forever };
        Storyboard.SetTargetProperty(turn, new PropertyPath("RenderTransform.Angle"));
        var storyboard = new Storyboard();
        storyboard.Children.Add(turn);
        return (spinner, storyboard);
    }

    private const double FullTurn = 360;
}
