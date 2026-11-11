using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Murmur.Services;

namespace Murmur.App.Panes;

/// <summary>
/// One secret stored in the Credential Manager. A saved key is never shown in an editable field,
/// so a stray keystroke can't wipe it; replacing it is an explicit step.
/// </summary>
public static class ApiKeyField
{
    private const string Category = "keys";

    /// <param name="title">What the key is for, e.g. "Deepgram".</param>
    /// <param name="target">The Credential Manager entry.</param>
    /// <param name="saved">Called after a key was saved (to refresh what depends on it).</param>
    public static ContentControl Build(string title, string target, Action? saved = null)
    {
        var host = new ContentControl();
        void ShowSaved() => host.Content = Ui.Row(
            title,
            Ui.Horizontal(Theme.Window.RowSpacing, Ui.Success("Saved in Credential Manager"), Ui.Button("Replace…", () => ShowEditor(canCancel: true))));

        void ShowEditor(bool canCancel)
        {
            var field = new PasswordBox { Width = Theme.Window.FieldWidth };
            void Save()
            {
                if (field.Password.Trim().Length == 0)
                {
                    return;
                }
                if (CredentialStore.Write(target, field.Password))
                {
                    Log.Info(Category, $"{title} saved");
                    ShowSaved();
                    saved?.Invoke();
                }
            }
            field.KeyDown += (_, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    Save();
                }
            };
            UIElement?[] controls = [field, canCancel ? Ui.Button("Cancel", ShowSaved) : null, Ui.Button("Save", Save, isAccent: true)];
            host.Content = Ui.Row(title, Ui.Horizontal(Theme.Window.RowSpacing, [.. controls.OfType<UIElement>()]));
            field.Focus();
        }

        if (CredentialStore.Contains(target))
        {
            ShowSaved();
        }
        else
        {
            ShowEditor(canCancel: false);
        }
        return host;
    }
}
