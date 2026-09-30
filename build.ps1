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

# gzip helper: resources are embedded compressed to keep both exes small
function Gzip($src, $dst) {
    $in = [IO.File]::OpenRead($src); $out = [IO.File]::Create($dst)
    $gz = New-Object IO.Compression.GZipStream($out, [IO.Compression.CompressionMode]::Compress)
    $in.CopyTo($gz); $gz.Dispose(); $out.Dispose(); $in.Dispose()
}
$obj = Join-Path $root "obj"; New-Item -ItemType Directory -Force $obj | Out-Null
Gzip "$root\assets\adhkar.json" "$obj\adhkar.json.gz"

& $csc /nologo /noconfig /nostdlib+ /target:winexe /platform:anycpu /optimize+ /debug- /langversion:7.3 `
    /nowarn:8012 /warnaserror- `
    "/out:$outDir\Dhikr.exe" "/win32icon:$root\assets\dhikr.ico" "/win32manifest:$root\assets\app.manifest" `
    "/resource:$root\assets\dhikr.ico,Dhikr.dhikr.ico" "/resource:$obj\adhkar.json.gz,Dhikr.adhkar.json.gz" @refs @sources
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }

# Installer: one file with Dhikr.exe embedded (gzipped); per-user, no admin rights needed.
Gzip "$outDir\Dhikr.exe" "$obj\Dhikr.exe.gz"
& $csc /nologo /noconfig /nostdlib+ /target:winexe /platform:anycpu /optimize+ /debug- /langversion:7.3 `
    /nowarn:8012 /warnaserror- `
    "/out:$outDir\DhikrSetup.exe" "/win32icon:$root\assets\dhikr.ico" "/win32manifest:$root\assets\app.manifest" `
    "/resource:$obj\Dhikr.exe.gz,Dhikr.exe.gz" @refs "$root\installer\Setup.cs"
if ($LASTEXITCODE -ne 0) { throw "Installer build failed ($LASTEXITCODE)" }

Get-Item "$outDir\Dhikr.exe", "$outDir\DhikrSetup.exe" | Select-Object FullName, Length
