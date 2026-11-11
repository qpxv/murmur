using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Murmur.Core;

namespace Murmur.Services;

/// <summary>
/// The text "Copy diagnostics" puts on the clipboard: version, Windows build, what's set up and
/// the newest log lines. Ready to paste into chat; never contains keys or dictated text.
/// </summary>
public static class DiagnosticsReport
{
    /// <param name="facts">Live state only the app knows (hook running, microphone, ...), as label and value.</param>
    public static string Build(IEnumerable<KeyValuePair<string, string>> facts)
    {
        var report = new StringBuilder();
        report.AppendLine("Murmur diagnostics");
        Append(report, "version", Version);
        Append(report, "windows", $"{Environment.OSVersion.Version} ({RuntimeInformation.OSArchitecture})");
        Append(report, "runtime", RuntimeInformation.FrameworkDescription);
        Append(report, "exe", Environment.ProcessPath ?? "unknown");
        Append(report, "deepgram key", IsSet(AppConstants.Credentials.DeepgramTarget));
        Append(report, "claude key", IsSet(AppConstants.Credentials.AnthropicTarget));
        Append(report, "neon url", IsSet(AppConstants.Credentials.NeonTarget));
        Append(report, "start with windows", StartWithWindows.IsEnabled ? "on" : "off");
        foreach (var fact in facts)
        {
            Append(report, fact.Key, fact.Value);
        }
        report.AppendLine();
        report.AppendLine($"last {AppConstants.DiagnosticsReport.LogLines} log lines:");
        foreach (var line in Log.LastLines(AppConstants.DiagnosticsReport.LogLines))
        {
            report.AppendLine(line);
        }
        return report.ToString();
    }

    /// <summary>The app version (from the build, e.g. "0.1.0+abc1234").</summary>
    public static string Version { get; } =
        typeof(DiagnosticsReport).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    private static string IsSet(string target) => CredentialStore.Contains(target) ? "set" : "missing";

    private static void Append(StringBuilder report, string label, string value) => report.AppendLine($"{label}: {value}");
}
