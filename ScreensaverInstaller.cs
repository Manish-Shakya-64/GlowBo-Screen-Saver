using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
namespace Glowbo;

/// <summary>Registers this app as the current user's Windows screen saver (no admin rights needed), and removes it again.</summary>
internal static class ScreensaverInstaller
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SystemParametersInfo(int action, int param, IntPtr vparam, int winIni);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetShortPathName(string longPath, StringBuilder shortPath, int size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentPackageFamilyName(ref int length, StringBuilder? name);
    private const int SPI_SETSCREENSAVETIMEOUT = 15, SPI_SETSCREENSAVEACTIVE = 17, SPI_SETSCREENSAVESECURE = 0x77, SPIF_UPDATEINIFILE = 1, SPIF_SENDCHANGE = 2;
    private const string DesktopKey = @"Control Panel\Desktop";

    /// <summary>Sets Windows' "On resume, display logon screen" option for the current user.</summary>
    public static void ApplySecureLock(bool secure) =>
        SystemParametersInfo(SPI_SETSCREENSAVESECURE, secure ? 1 : 0, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

    public static string InstallDir => Path.Combine(GlowboSettings.DataDir, "Screensaver");
    private static string LegacyDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DasaiMochi");
    // Screensaver copy made by versions released before the rename to Glowbo.
    private static string LegacyInstallDir => Path.Combine(LegacyDataDir, "Screensaver");
    // Written next to the .scr when it was installed from the Store (MSIX) app: holds the package family name.
    private static string PackageMarker => Path.Combine(InstallDir, "package.txt");

    /// <summary>Copies the app to a stable folder as Glowbo.scr, selects it and sets the idle timeout. Returns the .scr path.</summary>
    public static string Install(int idleMinutes)
    {
        Directory.CreateDirectory(InstallDir);
        string source = Path.GetFullPath(AppContext.BaseDirectory);
        // Copy the app's own files only (dll/json too for a framework-dependent build), so the .scr can start.
        if (!IsSameDir(source, InstallDir))
            foreach (string file in AppInstaller.AppFiles())
                File.Copy(file, Path.Combine(InstallDir, Path.GetFileName(file)), true);

        string scr = Path.Combine(InstallDir, "Glowbo.scr");
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the running executable.");
        if (!string.Equals(exe, scr, StringComparison.OrdinalIgnoreCase)) File.Copy(exe, scr, true);

        // Remember which Store package installed us, so the copy can clean itself up after that app is uninstalled.
        string? family = CurrentPackageFamily();
        if (family != null) File.WriteAllText(PackageMarker, family);
        else if (!IsSameDir(source, InstallDir) && File.Exists(PackageMarker)) File.Delete(PackageMarker);

        using (var key = Registry.CurrentUser.OpenSubKey(DesktopKey, writable: true)!)
            key.SetValue("SCRNSAVE.EXE", ShortPath(scr));
        SystemParametersInfo(SPI_SETSCREENSAVETIMEOUT, Math.Max(1, idleMinutes) * 60, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 1, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        try { if (Directory.Exists(LegacyInstallDir)) Directory.Delete(LegacyInstallDir, true); } catch { /* old copy in use; harmless to leave */ }
        return scr;
    }

    /// <summary>True when Windows' current screen saver is a Glowbo (or older Dasai Mochi) copy.</summary>
    public static bool IsRegistered
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(DesktopKey);
            string current = key?.GetValue("SCRNSAVE.EXE") as string ?? "";
            return current.Contains("GLOWBO", StringComparison.OrdinalIgnoreCase) || current.Contains("DASAIM", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Unsets Glowbo as the Windows screen saver and deletes the installed copy; with <paramref name="deleteSettings"/>
    /// also deletes saved settings. Folders that hold the running exe are deleted right after this process exits.
    /// </summary>
    public static void Uninstall(bool deleteSettings)
    {
        if (IsRegistered)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(DesktopKey, writable: true))
                key?.DeleteValue("SCRNSAVE.EXE", throwOnMissingValue: false);
            SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 0, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }
        var dirs = deleteSettings ? new[] { GlowboSettings.DataDir, LegacyDataDir } : new[] { InstallDir, LegacyInstallDir };
        foreach (string dir in dirs) DeleteDir(dir);
    }

    /// <summary>
    /// Store apps cannot run code when Windows uninstalls them. Instead, the screensaver copy checks on every start
    /// whether the Store app that installed it still exists; if not, it removes itself. Returns true when it did.
    /// </summary>
    public static bool CleanupIfAppRemoved()
    {
        try
        {
            if (!IsSameDir(AppContext.BaseDirectory, InstallDir) || !File.Exists(PackageMarker)) return false;
            string family = File.ReadAllText(PackageMarker).Trim();
            // Windows deletes this per-package folder when the package is uninstalled for the user.
            string packageData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", family);
            if (family.Length == 0 || Directory.Exists(packageData)) return false;
            Uninstall(deleteSettings: true);
            return true;
        }
        catch { return false; }
    }

    public static void OpenWindowsSettings() => Process.Start(new ProcessStartInfo("control.exe", "desk.cpl,,@screensaver") { UseShellExecute = true });
    public static void OpenInstalledApps() => Process.Start(new ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true });

    /// <summary>Package family name when running as a Store/MSIX app, otherwise null.</summary>
    public static string? CurrentPackageFamily()
    {
        int length = 0;
        if (GetCurrentPackageFamilyName(ref length, null) != 122 /* ERROR_INSUFFICIENT_BUFFER */) return null;
        var sb = new StringBuilder(length);
        return GetCurrentPackageFamilyName(ref length, sb) == 0 ? sb.ToString() : null;
    }

    public static void DeleteDir(string dir)
    {
        if (!Directory.Exists(dir)) return;
        try { Directory.Delete(dir, true); }
        catch
        {
            // Still in use (for example this exe runs from it): delete once this process has exited.
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping -n 3 127.0.0.1 >nul & rd /s /q \"{dir}\"") { CreateNoWindow = true, UseShellExecute = false });
        }
    }

    private static bool IsSameDir(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    // Windows' own dialog stores 8.3 paths; this avoids problems with spaces in the user folder.
    private static string ShortPath(string path)
    {
        var sb = new StringBuilder(260);
        return GetShortPathName(path, sb, sb.Capacity) > 0 ? sb.ToString() : path;
    }
}
