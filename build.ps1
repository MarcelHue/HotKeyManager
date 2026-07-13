#Requires -Version 5.1
<#
.SYNOPSIS
    Lokales Build-Script für HotKeyManager (dotnet CLI, kein Visual Studio nötig).

.DESCRIPTION
    Erstellt einen Release-Publish der HotKeyManager Anwendung.
    Offizielle Releases werden über GitHub Actions gebaut (.github/workflows/release.yml):
    Tag "vX.Y.Z" pushen -> Workflow baut, packt mit Velopack und veröffentlicht das Release.

.PARAMETER Configuration
    Build-Konfiguration (Standard: Release)

.PARAMETER Clean
    Löscht vor dem Build alle Build-Artefakte.

.PARAMETER Pack
    Erstellt zusätzlich lokal ein Velopack-Paket (benötigt: dotnet tool install -g vpk).

.EXAMPLE
    .\build.ps1
    Erstellt den Release-Publish nach bin\publish\win-x64.

.EXAMPLE
    .\build.ps1 -Pack
    Publish + lokales Velopack-Paket unter .\Releases (zum Testen des Installers).
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Clean,
    [switch]$Pack
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ProjectFile = Join-Path $ProjectRoot "HotKeyManager.csproj"
$PublishDir = Join-Path $ProjectRoot "bin\publish\win-x64"

Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "   HotKeyManager Build Script" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

if ($Clean) {
    Write-Host "=== Bereinige Build-Verzeichnisse ===" -ForegroundColor Cyan
    foreach ($dir in @((Join-Path $ProjectRoot "bin"), (Join-Path $ProjectRoot "obj"))) {
        if (Test-Path $dir) {
            Write-Host "  Lösche $dir..."
            Remove-Item -Path $dir -Recurse -Force
        }
    }
}

Write-Host "=== Erstelle $Configuration-Publish ===" -ForegroundColor Cyan
Write-Host "  Ausgabe: $PublishDir"
Write-Host ""

& dotnet publish $ProjectFile `
    -c $Configuration `
    -p:Platform=x64 `
    -r win-x64 `
    --self-contained `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "FEHLER: Build fehlgeschlagen mit Exit-Code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Build erfolgreich: $PublishDir" -ForegroundColor Green

if ($Pack) {
    Write-Host "=== Erstelle Velopack-Paket ===" -ForegroundColor Cyan

    $vpk = Get-Command vpk -ErrorAction SilentlyContinue
    if (-not $vpk) {
        Write-Host "FEHLER: vpk nicht gefunden. Installieren mit: dotnet tool install -g vpk" -ForegroundColor Red
        exit 1
    }

    # Version aus dem Projekt lesen
    [xml]$csproj = Get-Content $ProjectFile
    $version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1
    if (-not $version) { $version = "0.0.0" }

    & vpk pack `
        --packId HotKeyManager `
        --packVersion $version `
        --packDir $PublishDir `
        --mainExe HotKeyManager.exe `
        --packTitle "HotKey Manager" `
        --packAuthors MarcelHue `
        --icon (Join-Path $ProjectRoot "icon-512.ico")

    if ($LASTEXITCODE -ne 0) {
        Write-Host "FEHLER: vpk pack fehlgeschlagen mit Exit-Code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "Velopack-Paket erstellt unter: $(Join-Path $ProjectRoot 'Releases')" -ForegroundColor Green
}

Write-Host ""
Write-Host "Fertig." -ForegroundColor Green
