# Glowbo — Cute Face Screensaver

A configurable Windows screensaver with a cute OLED-style face: glowing rounded eyes and mouth on a dark background.

## Features
- Studio UI with a live animated preview.
- 20 expressions: Happy, Angry, Shy, Blinking, Sleepy, Love, Surprised, Sad, Wink, Excited, Confused, Dizzy, Silly, Neutral, Cool, Starstruck, Thinking, Kiss, Scared and Proud.
- Glowbo's signature sparkle eyes.
- Optional date and time in the bottom-right corner of the screensaver.
- Configurable face and background colors.
- Select which faces participate in automatic rotation and set the rotation interval.
- Full-screen screensaver mode; exits on keyboard, click, wheel or significant mouse movement.
- Multi-monitor option and saved settings.
- Settings stored in `%LOCALAPPDATA%\Glowbo\settings.json`.

## Requirements
Windows 10/11, Visual Studio 2022 with the **.NET desktop development** workload, and .NET 8 SDK.

## Open and run
1. Extract the ZIP.
2. Open `Glowbo.sln` in Visual Studio.
3. Let NuGet restore complete.
4. Press F5 to open Glowbo.
5. Select a face, change colors, choose rotation faces, and click **Save settings**.
6. Click **Start screensaver now** to test the full-screen display.

## Publish (share with anyone)
In PowerShell from the project folder:
```powershell
dotnet publish -p:PublishProfile=Release-win-x64
```
Or in Visual Studio: right-click the project → **Publish** → choose the **Release-win-x64** profile → **Publish**.

Output: `bin\Publish\Glowbo.exe` — a single self-contained file (about 70 MB) that runs on any 64-bit Windows 10/11 PC without installing .NET. Share it as a ZIP (OneDrive, Google Drive, GitHub Releases, etc.).

On first run Glowbo offers to install itself for the current user (no admin): a copy in `%LOCALAPPDATA%\Programs\Glowbo`, a Start-menu shortcut and an entry in **Settings → Apps**. Uninstalling from Settings → Apps runs `Glowbo.exe /uninstall`, which also removes the screensaver and optionally the saved settings. Declined offers can be accepted later with **Install app on this PC**.

The recipient double-clicks `Glowbo.exe`, accepts the install, sets faces/colors and clicks **Install as Windows screensaver**. Because the file is not code-signed, Windows SmartScreen shows "Windows protected your PC" on first run; they click **More info → Run anyway**.

Bump `<Version>` in `Glowbo.csproj` for each release.

## Microsoft Store package (MSIX)
Files: `Packaging\Package.appxmanifest` (manifest with identity tokens), `Packaging\Assets\` (logos), `build-msix.ps1`.

Requires the Windows SDK (`winget install Microsoft.WindowsSDK.10.0.22621`).
```powershell
# Local test (Windows Developer Mode on): installs from bin\MsixLayout without signing
.\build-msix.ps1 -Register
# Store package, identity values from Partner Center → Product management → Product identity
.\build-msix.ps1 -IdentityName "12345YourName.Glowbo" -Publisher "CN=..." -PublisherDisplayName "Your Name"
```
Output: `bin\Msix\Glowbo_<version>_x64.msix` — upload it in Partner Center; the Store signs it.
The manifest declares the restricted capability `unvirtualizedResources` so **Install as Windows screensaver** can write the real `HKCU\Control Panel\Desktop` and `%LOCALAPPDATA%\Glowbo`; justify it in the submission notes.

## Set as the Windows screen saver
Click **Install as Windows screensaver** in Studio. It copies the app to `%LOCALAPPDATA%\Glowbo\Screensaver\Glowbo.scr`, selects it as the current user's screen saver and applies the **Start after idle for (minutes)** value. No administrator rights are needed. Click **Windows screen saver settings…** to change the wait time or preview it.

Manual alternative: copy the published EXE to `Glowbo.scr`, then right-click it and choose **Install**.

A screen saver does not reliably start over the Windows lock screen on every configuration; that depends on Windows policy/session settings.

## Command line
Windows calls the `.scr` with these arguments:
- `/s` — run the full-screen screensaver.
- `/p <hwnd>` — draw the preview inside the small monitor of the Screen Saver dialog.
- `/c` or no argument — open Studio (the dialog's **Settings** button).
