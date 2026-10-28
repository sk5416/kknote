$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root "src\KkNote\KkNote.csproj"
$out = Join-Path $root "dist\self-contained"
$ver = ([regex]::Match((Get-Content $csproj -Raw), "<Version>([^<]+)</Version>")).Groups[1].Value
if ([string]::IsNullOrWhiteSpace($ver)) { $ver = "0.0.0" }
dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $out
Get-ChildItem $out -Filter "KkNote-*.exe" | Remove-Item -Force
$named = Join-Path $out "KkNote-$ver.exe"
Move-Item (Join-Path $out "KkNote.exe") $named -Force
Remove-Item (Join-Path $out "KkNote.pdb") -ErrorAction SilentlyContinue
Write-Host "Published $named"
