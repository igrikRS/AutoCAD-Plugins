param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("x64")]
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

$solution = Join-Path $PSScriptRoot "LayoutsFromModel.sln"
$msbuild = $null

$msbuildCommand = Get-Command "MSBuild.exe" -ErrorAction SilentlyContinue
if ($msbuildCommand) {
    $msbuild = $msbuildCommand.Source
}

if (-not $msbuild) {
    $candidates = @(
        "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    )

    $msbuild = $candidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}

if (-not $msbuild) {
    throw "MSBuild из Visual Studio не найден. Установите компонент разработки классических приложений .NET."
}

Write-Host "Building LayoutsFromModel.sln: $Configuration|$Platform"

& $msbuild $solution `
    "-t:Rebuild" `
    "-p:Configuration=$Configuration" `
    "-p:Platform=$Platform" `
    "-v:minimal"

if ($LASTEXITCODE -ne 0) {
    throw "Сборка завершилась с кодом $LASTEXITCODE."
}

Write-Host
Write-Host "Press any key to continue ..."
Read-Host
Clear-Host
