<#
  Builds the Microsoft Store package (MSIX) for Glowbo.

  Upload to Partner Center (identity values from Partner Center → Product management → Product identity):
    .\build-msix.ps1 -IdentityName "12345YourName.Glowbo" -Publisher "CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX" -PublisherDisplayName "Your Name"

  Test on this PC first (needs Windows Developer Mode; uses placeholder identity):
    .\build-msix.ps1 -Register

  Output: bin\Msix\Glowbo_<version>_x64.msix   (Partner Center signs it; you do not need a certificate)
#>
param(
    [string]$IdentityName = "Glowbo.Local",
    [string]$Publisher = "CN=GlowboDev",
    [string]$PublisherDisplayName = "Glowbo",
    [switch]$Register
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# Store versions are 4-part and the last part must be 0: <Version>1.2.3</Version> becomes 1.2.3.0.
$version = ([xml](Get-Content Glowbo.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$parts = @($version.Split('.')) + @('0', '0', '0')
$msixVersion = "$($parts[0]).$($parts[1]).$($parts[2]).0"

dotnet publish -p:PublishProfile=Release-win-x64
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$layout = "bin\MsixLayout"
if (Test-Path $layout) {
    if ($Register) { Get-AppxPackage -Name $IdentityName | Remove-AppxPackage }
    Remove-Item -Recurse -Force $layout
}
New-Item -ItemType Directory -Force "$layout\Assets" | Out-Null
Copy-Item bin\Publish\Glowbo.exe $layout
Copy-Item Packaging\Assets\* "$layout\Assets"

$manifest = [IO.File]::ReadAllText("$PSScriptRoot\Packaging\Package.appxmanifest")
$manifest = $manifest.Replace('$IdentityName$', $IdentityName).Replace('$Publisher$', $Publisher).Replace('$PublisherDisplayName$', $PublisherDisplayName).Replace('$Version$', $msixVersion)
[IO.File]::WriteAllText("$PSScriptRoot\$layout\AppxManifest.xml", $manifest, (New-Object Text.UTF8Encoding $false))

if ($Register) {
    # Runs the package straight from the layout folder, no signing needed (Developer Mode must be on).
    Add-AppxPackage -Register "$layout\AppxManifest.xml"
    "Registered. Start 'Glowbo' from the Start menu to test the packaged app."
    return
}

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $makeappx) { throw "makeappx.exe not found. Install the Windows SDK: winget install Microsoft.WindowsSDK.10.0.22621" }

New-Item -ItemType Directory -Force bin\Msix | Out-Null
$out = "bin\Msix\Glowbo_${msixVersion}_x64.msix"
& $makeappx pack /d $layout /p $out /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed" }
"Package: $(Resolve-Path $out)"
