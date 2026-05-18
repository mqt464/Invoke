param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = ".\artifacts\portable"
)

$ErrorActionPreference = "Stop"

$project = ".\src\Invoke.App\Invoke.App.csproj"
$publishDir = Join-Path $OutputRoot "$Runtime-publish"
$zipPath = Join-Path $OutputRoot "Invoke-$Runtime-portable.zip"

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue
Remove-Item -Force $zipPath -ErrorAction SilentlyContinue

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    /p:PublishSingleFile=false `
    /p:IncludeNativeLibrariesForSelfExtract=false `
    -o $publishDir

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host "Portable publish ready:"
Write-Host "  Publish dir: $publishDir"
Write-Host "  Zip:         $zipPath"
