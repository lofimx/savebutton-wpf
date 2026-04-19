using System.IO;
using System.Reflection;
using Kaya.Core.Services;
using Microsoft.Win32;

namespace Kaya.Wpf;

/// <summary>
/// Registers the savebutton:// URL scheme in HKCU. Called on every primary-instance startup
/// so that dev builds (`dotnet run`) and installer-less runs pick up the handler automatically.
/// The WiX installer writes the same registry entry for shipped installs.
/// </summary>
public static class UrlSchemeRegistrar
{
    private const string Scheme = "savebutton";
    private const string CommandSubKey = "shell\\open\\command";

    public static void EnsureRegistered()
    {
        try
        {
            var exePath = CurrentExePath();
            if (string.IsNullOrEmpty(exePath)) return;

            var desiredCommand = $"\"{exePath}\" \"%1\"";

            if (IsAlreadyRegistered(desiredCommand)) return;

            Write(exePath, desiredCommand);
            Logger.Instance.Log($"🔵 INFO UrlSchemeRegistrar registered savebutton:// -> {exePath}");
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR UrlSchemeRegistrar failed: {e.Message}");
        }
    }

    private static bool IsAlreadyRegistered(string desiredCommand)
    {
        try
        {
            using var command = Registry.CurrentUser.OpenSubKey(
                $"Software\\Classes\\{Scheme}\\{CommandSubKey}");
            var current = command?.GetValue(null) as string;
            return string.Equals(current, desiredCommand, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void Write(string exePath, string commandValue)
    {
        using var root = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{Scheme}");
        root.SetValue(null, "URL:Save Button Protocol");
        root.SetValue("URL Protocol", "");

        using var icon = root.CreateSubKey("DefaultIcon");
        icon.SetValue(null, $"\"{exePath}\"");

        using var command = root.CreateSubKey(CommandSubKey);
        command.SetValue(null, commandValue);
    }

    private static string? CurrentExePath()
    {
        var path = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(path)) return null;
        // Defensive: refuse to register if the process is dotnet.exe (shouldn't happen for WinExe).
        if (Path.GetFileName(path).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Instance.Log("🟠 WARN UrlSchemeRegistrar skipped: running under dotnet.exe");
            return null;
        }
        return path;
    }
}
