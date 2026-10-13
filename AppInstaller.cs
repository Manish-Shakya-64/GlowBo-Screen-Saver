using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
namespace Glowbo;

/// <summary>
/// Installs the plain Glowbo.exe for the current user like a normal app (no admin rights): a copy in
/// %LOCALAPPDATA%\Programs\Glowbo, a Start-menu shortcut and an entry in Settings → Apps whose Uninstall
/// runs <c>Glowbo.exe /uninstall</c>, which also cleans up the screensaver. Not used by the Store (MSIX) version.
/// </summary>
internal static class AppInstaller
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Glowbo";

    public static string InstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Glowbo");
    private static string InstalledExe => Path.Combine(InstallDir, "Glowbo.exe");
    private static string StartMenuShortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Glowbo.lnk");

    public static bool IsInstalled => File.Exists(InstalledExe) && Registry.CurrentUser.OpenSubKey(UninstallKey) is { } key && key.ValueCount > 0;
    public static bool RunningInstalledCopy => SameDir(AppContext.BaseDirectory, InstallDir);
    /// <summary>Install/update makes sense only for the plain exe started from somewhere else (Downloads, a USB stick…).</summary>
    public static bool CanInstall => ScreensaverInstaller.CurrentPackageFamily() == null && !RunningInstalledCopy
                                     && !SameDir(AppContext.BaseDirectory, ScreensaverInstaller.InstallDir);

    /// <summary>
    /// First-run offer to install (or update the installed copy). Returns true when the installed copy was
    /// started, so this process should exit.
    /// </summary>
    public static bool OfferInstall()
    {
        if (!CanInstall) return false;
        var settings = GlowboSettings.Load();
        bool installed = IsInstalled;
        if (!installed && settings.SkipInstallPrompt) return false;
        string question = installed
            ? "Glowbo is already installed on this PC.\n\nUpdate it with this version?"
            : "Install Glowbo on this PC?\n\nIt will be added to the Start menu and to Settings → Apps, so you can find, open and uninstall it like any other app.";
        if (MessageBox.Show(question, "Glowbo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            if (!installed) { settings.SkipInstallPrompt = true; settings.Save(); }
            return false;
        }
        return InstallAndLaunch();
    }

    /// <summary>Installs or updates, then starts the installed copy. Returns true on success.</summary>
    public static bool InstallAndLaunch()
    {
        try
        {
            string exe = Install();
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not install Glowbo:\n" + ex.Message + "\n\nIf Glowbo is already open, close it and try again.", "Glowbo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private static string Install()
    {
        Directory.CreateDirectory(InstallDir);
        foreach (string file in AppFiles()) File.Copy(file, Path.Combine(InstallDir, Path.GetFileName(file)), true);
        string exe = InstalledExe;
        if (!File.Exists(exe)) File.Copy(Environment.ProcessPath!, exe, true);

        CreateShortcut(StartMenuShortcut, exe);

        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        long sizeKb = Directory.GetFiles(InstallDir).Sum(f => new FileInfo(f).Length) / 1024;
        using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            key.SetValue("DisplayName", "Glowbo");
            key.SetValue("DisplayVersion", version.ToString(3));
            key.SetValue("Publisher", "Glowbo");
            key.SetValue("DisplayIcon", exe);
            key.SetValue("InstallLocation", InstallDir);
            key.SetValue("UninstallString", $"\"{exe}\" /uninstall");
            key.SetValue("QuietUninstallString", $"\"{exe}\" /uninstall /quiet");
            key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, sizeKb), RegistryValueKind.DWord);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }

        // Keep an existing Glowbo screensaver in step with the new version.
        if (ScreensaverInstaller.IsRegistered)
            try { ScreensaverInstaller.Install(GlowboSettings.Load().IdleMinutes); } catch { /* old copy in use; the next install refreshes it */ }
        return exe;
    }

    /// <summary>Runs from Settings → Apps → Glowbo → Uninstall (<c>Glowbo.exe /uninstall [/quiet]</c>).</summary>
    public static void RunUninstall(bool quiet)
    {
        bool deleteSettings = true;
        if (!quiet)
        {
            if (MessageBox.Show("Uninstall Glowbo from this PC?\n\nThis also stops Windows using Glowbo as your screen saver.", "Glowbo",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            deleteSettings = MessageBox.Show("Also delete your saved faces, colors and settings?", "Glowbo",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
        ScreensaverInstaller.Uninstall(deleteSettings);
        try { File.Delete(StartMenuShortcut); } catch { }
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
        ScreensaverInstaller.DeleteDir(InstallDir);
        if (!quiet) MessageBox.Show("Glowbo was uninstalled.", "Glowbo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// The files that make up this app: just the exe for the single-file build, or Glowbo.* (exe, dll, json)
    /// for a framework-dependent build. Never the rest of the folder it was started from.
    /// </summary>
    public static IEnumerable<string> AppFiles()
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the running executable.");
        if (string.IsNullOrEmpty(typeof(AppInstaller).Assembly.Location)) return new[] { exe }; // single-file build
        return Directory.GetFiles(Path.GetDirectoryName(exe)!, Path.GetFileNameWithoutExtension(exe) + ".*");
    }

    private static void CreateShortcut(string path, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.CreateShortcut(path);
        link.TargetPath = target;
        link.WorkingDirectory = Path.GetDirectoryName(target);
        link.IconLocation = target + ",0";
        link.Description = "Glowbo: cute face screensaver";
        link.Save();
    }

    private static bool SameDir(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
}
