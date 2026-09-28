# Builds release\Dhikr.exe (a single small .NET Framework 4.8 WPF exe, preinstalled on Windows 10/11).
# Needs a modern C# compiler (Roslyn), e.g. from Visual Studio / VS Build Tools 2019+.
param([string]$OutDir = 'release')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$csc = $env:DHIKR_CSC
if (-not $csc) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $csc = & $vswhere -latest -products * -find 'MSBuild\**\Bin\Roslyn\csc.exe' | Select-Object -First 1
    }
}
if (-not $csc -or -not (Test-Path $csc)) { throw 'Roslyn csc.exe not found. Install "Visual Studio Build Tools" or set DHIKR_CSC.' }

$fw  = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319"
$wpf = "$fw\WPF"
$refs = @(
    "$fw\mscorlib.dll", "$fw\System.dll", "$fw\System.Core.dll", "$fw\System.Xml.dll",
    "$fw\System.Runtime.Serialization.dll", "$fw\System.Drawing.dll", "$fw\System.Windows.Forms.dll",
    "$fw\System.Xaml.dll", "$wpf\WindowsBase.dll", "$wpf\PresentationCore.dll", "$wpf\PresentationFramework.dll"
) | ForEach-Object { "/r:$_" }

$outDir = Join-Path $root $OutDir
New-Item -ItemType Directory -Force $outDir | Out-Null
$sources = Get-ChildItem "$root\src\*.cs" | ForEach-Object { $_.FullName }

& $csc /nologo /noconfig /nostdlib+ /target:winexe /platform:anycpu /optimize+ /debug- /langversion:7.3 `
    /nowarn:8012 /warnaserror- `
    "/out:$outDir\Dhikr.exe" "/win32icon:$root\assets\dhikr.ico" "/win32manifest:$root\assets\app.manifest" `
    "/resource:$root\assets\dhikr.ico,Dhikr.dhikr.ico" @refs @sources
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }

# Installer: one file with Dhikr.exe embedded; per-user, no admin rights needed.
& $csc /nologo /noconfig /nostdlib+ /target:winexe /platform:anycpu /optimize+ /debug- /langversion:7.3 `
    /nowarn:8012 /warnaserror- `
    "/out:$outDir\DhikrSetup.exe" "/win32icon:$root\assets\dhikr.ico" "/win32manifest:$root\assets\app.manifest" `
    "/resource:$outDir\Dhikr.exe,Dhikr.exe" @refs "$root\installer\Setup.cs"
if ($LASTEXITCODE -ne 0) { throw "Installer build failed ($LASTEXITCODE)" }

Get-Item "$outDir\Dhikr.exe", "$outDir\DhikrSetup.exe" | Select-Object FullName, Length
