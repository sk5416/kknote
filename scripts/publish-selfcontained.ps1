$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "dist\self-contained"
dotnet publish (Join-Path $root "src\KkNote\KkNote.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $out
Write-Host "已输出到 $out"
Write-Host "这个包不依赖本机安装 .NET，体积会大很多。日常开发请用 publish-framework.ps1。"
