param(
    [string]$Version = "5.0.0",
    [switch]$SaltarPruebas
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

Write-Host "=== Compilación Integral: Xynex Code Editor & Cibao v$Version ===" -ForegroundColor Cyan

Push-Location $RepoRoot
try {
    dotnet restore AlbaEcosistema.sln
    dotnet build AlbaEcosistema.sln -c Release --no-restore

    if (-not $SaltarPruebas) {
        dotnet test AlbaEcosistema.sln -c Release --no-build --verbosity normal
    }

    & (Join-Path $RepoRoot "packaging\windows\build-windows-installer.ps1") -Version $Version
    Write-Host "=== Empaquetado Windows completado en '$RepoRoot\dist' ===" -ForegroundColor Green
}
finally {
    Pop-Location
}
