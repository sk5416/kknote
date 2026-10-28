$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "dist\framework-dependent"
dotnet publish (Join-Path $root "src\KkNote\KkNote.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $out
Write-Host "已输出到 $out"
Write-Host "目标机器需要安装 .NET 8 Desktop Runtime。"
