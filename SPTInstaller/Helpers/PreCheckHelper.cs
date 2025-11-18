using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SPTInstaller.Models;

namespace SPTInstaller.Helpers;

public static class PreCheckHelper
{
    private const string registryInstall =
        @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov";
    private const string steamPathRegistryKey = @"Software\Wow6432Node\Valve\Steam";
    private const string tarkovAppId = "3932890";
    
    public static string? DetectOriginalGamePath()
    {
        // We can't detect the installed path on non-Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        // We prioritize the Steam version, as the Steam CDN is faster for updates, and if someone
        // owns it on both platforms, there's a better chance their Steam version is up to date
        var steamGamePath = DetectSteamGamePath();
        if (steamGamePath != null && 
            Path.Exists(Path.Combine(steamGamePath, "build")))
        {
            var steamBuildDir = Path.Combine(steamGamePath, "build");
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

    /**
     * Steam doesn't update it's libraryfolders.vdf on new game install, so we need
     * to chain the following steps to find where Tarkov is installed:
     *   - Load all steam library paths from libraryfolders.vdf
     *   - Loops through all libraries, and look for Tarkov's appmanifest
     *   - Extract the `installdir` from Tarkov's appmanifest
     *   - Build the final path as `library + steamapps + common + installdir`
     */
    private static string? DetectSteamGamePath()
    {
        // We can't detect the installed path on non-Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        var steamPath = GetSteamInstallPath();
        if (string.IsNullOrEmpty(steamPath))
        {
            return null;
        }

        var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        var steamLibraryPaths = ExtractVdfFieldsByName(libraryFoldersPath, "path");
        if (steamLibraryPaths.Count == 0)
        {
            return null;
        }

        return FindTarkovInSteamLibraries(steamLibraryPaths);
    }

    private static string? GetSteamInstallPath()
    {
        // We can't detect the installed path on non-Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        var steamPath = string.Empty;
        using (var steamReg = Registry.LocalMachine.OpenSubKey(steamPathRegistryKey, false))
        {
            if (steamReg != null)
            {
                steamPath = steamReg.GetValue("InstallPath")?.ToString();
            }
        }

        return steamPath;
    }

    private static string? FindTarkovInSteamLibraries(List<string> steamLibraryPaths)
    {
        var appManifestFilename = $"appmanifest_{tarkovAppId}.acf";
        foreach (var steamLibraryPath in steamLibraryPaths)
        {
            var appManifestPath = Path.Combine(steamLibraryPath, "steamapps", appManifestFilename);
            if (!File.Exists(appManifestPath))
            {
                continue;
            }

            var installDirFields = ExtractVdfFieldsByName(appManifestPath, "installdir");
            if (installDirFields.Count > 0)
            {
                return Path.Combine(steamLibraryPath, "steamapps", "common", installDirFields[0]);
            }
        }

        return null;
    }

    private static List<string> ExtractVdfFieldsByName(string vdfPath, string fieldName)
    {
        var fieldValues = new List<string>();
        if (!File.Exists(vdfPath)) return fieldValues;
        var libraryPathPattern = $@"""{fieldName}""\s+""(.*)""";
        foreach (var line in File.ReadLines(vdfPath))
        {
            var match = Regex.Match(line, libraryPathPattern);
            if (match.Success)
            {
                fieldValues.Add(Regex.Unescape(match.Groups[1].Value));
            }
        }

        return fieldValues;
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