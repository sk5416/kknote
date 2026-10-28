$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) {
    $env:DOTNET_ROOT = Split-Path $dotnet.Source
}
dotnet run --project (Join-Path $root "src\KkNote\KkNote.csproj") -c Release -- @args
