using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SPTInstaller.Models;

namespace SPTInstaller.Helpers;

public static class PreCheckHelper
{
    private const string registryInstall =
        @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov";
    private const string steamRegistryInstall =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3932890";
    
    public static string? DetectOriginalGamePath()
    {
        // We can't detect the installed path on non-Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        // We prioritize the Steam version, as the Steam CDN is faster for updates, and if someone
        // owns it on both platforms, there's a better chance their Steam version is up to date
        var steamUninstallValue = Registry.LocalMachine.OpenSubKey(steamRegistryInstall, false)
            ?.GetValue("InstallLocation");
        if (steamUninstallValue != null && 
            steamUninstallValue is string steamUninstallStringValue &&
            Path.Exists(Path.Combine(steamUninstallStringValue, "build")))
        {
            var steamDirInfo = new DirectoryInfo(steamUninstallStringValue);
            var steamBuildDir = Path.Combine(steamDirInfo.FullName, "build");
            return Path.TrimEndingDirectorySeparator(steamBuildDir);
        }

        // Fall back to the BSG Launcher registry key if Steam isn't being used
        var uninstallStringValue = Registry.LocalMachine.OpenSubKey(registryInstall, false)
            ?.GetValue("InstallLocation");
        var info = (uninstallStringValue is string key) ? new DirectoryInfo(key) : null;
        
        if (info == null)
            return null;
        
        return Path.TrimEndingDirectorySeparator(info.FullName);
    }
    
    public static Result DetectOriginalGameVersion(string gamePath)
    {
        try
        {
            string version = FileVersionInfo.GetVersionInfo(Path.Join(gamePath, "/EscapeFromTarkov.exe")).ProductVersion
                .Replace('-', '.').Split('.')[^2];
            return Result.FromSuccess(version);
        }
        catch (Exception ex)
        {
            return Result.FromError($"File not found: {ex.Message}");
        }
    }
}