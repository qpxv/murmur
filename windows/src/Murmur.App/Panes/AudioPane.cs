using System.Windows;
using System.Windows.Controls;
using Murmur.Services;

namespace Murmur.App.Panes;

/// <summary>Microphone choice and the whisper-friendly volume boost.</summary>
public static class AudioPane
{
    private const string SystemDefault = "Windows default";

    public static UIElement Build(AppServices services)
    {
        var settings = services.Settings;
        // read when the pane opens, so a newly plugged-in mic shows up
        IReadOnlyList<InputDevice?> choices = [null, .. AudioDevices.Inputs()];
        var picker = new ComboBox { Width = Theme.Window.FieldWidth };
        foreach (var device in choices)
        {
            picker.Items.Add(new ComboBoxItem { Content = device?.Name ?? SystemDefault, Tag = device?.Id });
        }
        var selected = choices.ToList().FindIndex(device => device?.Id == settings.Current.InputDeviceId);
        // a chosen mic that's unplugged shows as the default, which is what recording falls back to
        picker.SelectedIndex = Math.Max(0, selected);
        picker.SelectionChanged += (_, _) =>
        {
            var id = picker.SelectedItem is ComboBoxItem { Tag: string deviceId } ? deviceId : null;
            settings.Update(current => current with { InputDeviceId = id });
        };

        return Ui.Page(
            Pane.Audio.Title(),
            Ui.Section(header: null, footer: "If the chosen microphone is unplugged, the Windows default is used.", Ui.Row("Microphone", picker)),
            Ui.Section(
                header: null,
                footer: "Turns input volume to 100% while you dictate, so even whispers come through, then puts it back.",
                Ui.Toggle("Boost mic while recording", settings.Current.IsMicBoostEnabled, isOn =>
                    settings.Update(current => current with { IsMicBoostEnabled = isOn }))));
    }
}
