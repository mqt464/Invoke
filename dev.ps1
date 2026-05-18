$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = Join-Path $root ".dotnet\dotnet.exe"

if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

$env:INVOKE_DEV_OPEN = "1"

& $dotnet run --project (Join-Path $root "src\Invoke.App\Invoke.App.csproj")
