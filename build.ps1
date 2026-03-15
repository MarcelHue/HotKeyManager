#Requires -Version 5.1
<#
.SYNOPSIS
    Build-Script für HotKeyManager

.DESCRIPTION
    Erstellt einen optimierten Release-Build der HotKeyManager Anwendung.
    Optional kann auch ein Installer mit Inno Setup erstellt werden.

.PARAMETER CreateInstaller
    Wenn angegeben, wird nach dem Build auch der Installer erstellt.

.PARAMETER Clean
    Wenn angegeben, werden vor dem Build alle vorherigen Build-Artefakte gelöscht.

.PARAMETER Configuration
    Build-Konfiguration (Standard: Release)

.EXAMPLE
    .\build.ps1
    Erstellt nur den Release-Build.

.EXAMPLE
    .\build.ps1 -CreateInstaller
    Erstellt den Release-Build und den Installer.

.EXAMPLE
    .\build.ps1 -Clean -CreateInstaller
    Bereinigt, erstellt Build und Installer.
#>

param(
    [switch]$CreateInstaller,
    [switch]$Clean,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

# Konfiguration
$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$PublishDir = Join-Path $ProjectRoot "bin\publish\win-x64"
$InstallerDir = Join-Path $ProjectRoot "installer"
$InstallerScript = Join-Path $InstallerDir "setup.iss"
$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

# Farben für Ausgabe
function Write-Step {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Green
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Red
}

# MSBuild finden
function Find-MSBuild {
    # Versuche vswhere zu finden (Visual Studio Locator)
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    
    if (Test-Path $vswhere) {
        $vsPath = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($vsPath -and (Test-Path $vsPath)) {
            return $vsPath
        }
    }
    
    # Fallback: Bekannte Pfade prüfen
    $knownPaths = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    )
    
    foreach ($path in $knownPaths) {
        if (Test-Path $path) {
            return $path
        }
    }
    
    return $null
}

# Header
Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "   HotKeyManager Build Script" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

# Prüfen ob MSBuild verfügbar ist (benötigt für WinUI 3)
Write-Step "Prüfe Voraussetzungen"

$msbuildPath = Find-MSBuild
if (-not $msbuildPath) {
    Write-ErrorMsg "FEHLER: MSBuild wurde nicht gefunden."
    Write-Host "WinUI 3 Apps benötigen Visual Studio oder die Visual Studio Build Tools." -ForegroundColor Yellow
    Write-Host "Bitte installiere Visual Studio 2022 mit der '.NET Desktop-Entwicklung' Workload." -ForegroundColor Yellow
    exit 1
}
Write-Success "✓ MSBuild gefunden: $msbuildPath"

# Bereinigen wenn gewünscht
if ($Clean) {
    Write-Step "Bereinige Build-Verzeichnisse"
    
    $dirsToClean = @(
        (Join-Path $ProjectRoot "bin"),
        (Join-Path $ProjectRoot "obj")
    )
    
    foreach ($dir in $dirsToClean) {
        if (Test-Path $dir) {
            Write-Host "  Lösche $dir..."
            Remove-Item -Path $dir -Recurse -Force
        }
    }
    Write-Success "✓ Bereinigung abgeschlossen"
}

# Build ausführen
Write-Step "Erstelle Release-Build"
Write-Host "  Konfiguration: $Configuration"
Write-Host "  Platform: x64"
Write-Host "  Runtime: win-x64"
Write-Host "  Ausgabe: $PublishDir"
Write-Host ""

# MSBuild Parameter für WinUI 3 Publish
$projectFile = Join-Path $ProjectRoot "HotKeyManager.csproj"

# Erst Restore ausführen
Write-Host "  Stelle NuGet-Pakete wieder her..." -ForegroundColor Gray
& $msbuildPath $projectFile /t:Restore /p:Configuration=$Configuration /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /v:minimal
if ($LASTEXITCODE -ne 0) {
    Write-ErrorMsg "FEHLER: NuGet Restore fehlgeschlagen mit Exit-Code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# Dann Publish
Write-Host "  Erstelle Build..." -ForegroundColor Gray
$msbuildArgs = @(
    $projectFile
    "/t:Publish"
    "/p:Configuration=$Configuration"
    "/p:Platform=x64"
    "/p:RuntimeIdentifier=win-x64"
    "/p:SelfContained=true"
    "/p:PublishReadyToRun=false"
    "/p:PublishSingleFile=true"
    "/p:PublishDir=$PublishDir\"
    "/v:minimal"
)

& $msbuildPath @msbuildArgs
if ($LASTEXITCODE -ne 0) {
    Write-ErrorMsg "FEHLER: Build fehlgeschlagen mit Exit-Code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Success "✓ Build erfolgreich abgeschlossen"

# Installer erstellen wenn gewünscht
if ($CreateInstaller) {
    Write-Step "Erstelle Installer"
    
    # Prüfen ob Inno Setup installiert ist
    if (-not (Test-Path $InnoSetupPath)) {
        Write-ErrorMsg "FEHLER: Inno Setup wurde nicht gefunden unter: $InnoSetupPath"
        Write-Host "Bitte installiere Inno Setup von: https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
        exit 1
    }
    Write-Success "✓ Inno Setup gefunden"
    
    # Prüfen ob Installer-Script existiert
    if (-not (Test-Path $InstallerScript)) {
        Write-ErrorMsg "FEHLER: Installer-Script nicht gefunden: $InstallerScript"
        exit 1
    }
    
    Write-Host "  Kompiliere Installer..."
    & $InnoSetupPath $InstallerScript
    
    if ($LASTEXITCODE -ne 0) {
        Write-ErrorMsg "FEHLER: Installer-Erstellung fehlgeschlagen mit Exit-Code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    
    $installerOutput = Join-Path $InstallerDir "Output\HotKeyManager_Setup.exe"
    if (Test-Path $installerOutput) {
        Write-Success "✓ Installer erfolgreich erstellt: $installerOutput"
    }
}

# Zusammenfassung
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "   Build abgeschlossen!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Build-Ausgabe: $PublishDir" -ForegroundColor White

if ($CreateInstaller) {
    $installerOutput = Join-Path $InstallerDir "Output\HotKeyManager_Setup.exe"
    Write-Host "Installer: $installerOutput" -ForegroundColor White
}

Write-Host ""
