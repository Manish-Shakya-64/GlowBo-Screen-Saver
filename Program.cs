using System.Windows.Forms;
namespace Glowbo;
internal static class Program
{
    // Windows calls a .scr with: /s (run full screen), /c[:hwnd] (settings) or /p <hwnd> (dialog preview).
    // Settings → Apps calls the installed exe with /uninstall [/quiet].
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        // A screensaver copy left behind after the Store app was uninstalled removes itself and shows nothing.
        if (ScreensaverInstaller.CleanupIfAppRemoved()) return;
        string first = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "";
        string mode = first.Length >= 2 ? first[..2].Replace('-', '/') : first;
        switch (mode)
        {
            case "/s":
                Application.Run(new ScreensaverForm());
                break;
            case "/p":
                string handle = first.Length > 3 ? first[3..] : args.ElementAtOrDefault(1) ?? "";
                if (long.TryParse(handle, out long hwnd)) Application.Run(new PreviewForm(new IntPtr(hwnd)));
                break;
            case "/u": // Settings → Apps → Glowbo → Uninstall
                AppInstaller.RunUninstall(quiet: args.Any(a => a.Equals("/quiet", StringComparison.OrdinalIgnoreCase)));
                break;
            default:
                if (AppInstaller.OfferInstall()) return; // the installed copy was started instead
                Application.Run(new StudioForm());
                break;
        }
    }
}
