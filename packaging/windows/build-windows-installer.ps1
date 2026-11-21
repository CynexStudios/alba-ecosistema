param(
    [string]$Version = "9.0.0",
    [string]$Configuration = "Release",
    [string]$Rid = "win-x64"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$StageDir = Join-Path $RepoRoot "dist\stage-$Rid"
$DistDir = Join-Path $RepoRoot "dist"

Write-Host "==> Preparando staging para Xynex Code Editor & Cibao v$Version ($Rid)..." -ForegroundColor Cyan

if (Test-Path $StageDir) {
    Remove-Item -Recurse -Force $StageDir
}
New-Item -ItemType Directory -Force -Path $StageDir, $DistDir | Out-Null

# 1. Publicar CLI e IDE autocontenidos (.NET 8)
dotnet publish (Join-Path $RepoRoot "src\Alba.CLI\Alba.CLI.csproj") `
    -c $Configuration -r $Rid --self-contained true `
    -o (Join-Path $StageDir "cli") --nologo

dotnet publish (Join-Path $RepoRoot "src\Alba.IDE\Alba.IDE.csproj") `
    -c $Configuration -r $Rid --self-contained true `
    -o (Join-Path $StageDir "ide") --nologo

# Alias de línea de comandos 'cibao' junto al binario 'alba.exe'
$CibaoWrapper = Join-Path $StageDir "cli\cibao.cmd"
@"
@echo off
"%~dp0alba.exe" %*
"@ | Set-Content -Path $CibaoWrapper -Encoding Ascii

# 2. Compilar alba-host C++20 si CMake está disponible (opcional si ya existe)
$HostExe = Join-Path $RepoRoot "build\windows-auto\bin\$Configuration\alba-host.exe"
if (-not (Test-Path $HostExe) -and (Get-Command cmake -ErrorAction SilentlyContinue)) {
    cmake -S $RepoRoot -B (Join-Path $RepoRoot "build\windows-auto") -DCMAKE_BUILD_TYPE=$Configuration
    cmake --build (Join-Path $RepoRoot "build\windows-auto") --config $Configuration --target alba-host
}
if (Test-Path $HostExe) {
    Copy-Item $HostExe (Join-Path $StageDir "alba-host.exe") -Force
}

# 3. Copiar recursos integrados de solo lectura
Copy-Item (Join-Path $RepoRoot "addons") (Join-Path $StageDir "addons") -Recurse -Force
Copy-Item (Join-Path $RepoRoot "docs") (Join-Path $StageDir "docs") -Recurse -Force
Copy-Item (Join-Path $RepoRoot "examples") (Join-Path $StageDir "examples") -Recurse -Force

# 4. Generar paquete ZIP portable
$ZipPath = Join-Path $DistDir "XynexCodeEditor-$Version-$Rid-portable.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path "$StageDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
Write-Host "ZIP portable generado: $ZipPath" -ForegroundColor Green

# 5. Generar instalador gráfico .exe con Inno Setup 6 si está instalado
$IsccCandidatos = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "iscc"
)
$Iscc = $IsccCandidatos | Where-Object { Get-Command $_ -ErrorAction SilentlyContinue } | Select-Object -First 1

if ($Iscc) {
    & $Iscc "/DMyAppVersion=$Version" "/DSourceStageDir=$StageDir" "/DOutputDir=$DistDir" (Join-Path $PSScriptRoot "installer.iss")
    Write-Host "Instalador Windows generado en: $DistDir" -ForegroundColor Green
} else {
    Write-Warning "Inno Setup 6 (ISCC.exe) no encontrado. Se generó el ZIP portable; instala Inno Setup 6 para generar el .exe."
}
