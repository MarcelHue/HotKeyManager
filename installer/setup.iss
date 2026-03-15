; HotKeyManager Installer Script
; Erstellt mit Inno Setup 6
; https://jrsoftware.org/isinfo.php

#define MyAppName "HotKeyManager"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "HotKeyManager"
#define MyAppURL "https://github.com/yourusername/HotKeyManager"
#define MyAppExeName "HotKeyManager.exe"
#define MyAppSourceDir "..\bin\publish\win-x64"

[Setup]
; Eindeutige App-ID - NICHT ÄNDERN nach Veröffentlichung!
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installationsverzeichnis
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

; Ausgabe-Einstellungen
OutputDir=Output
OutputBaseFilename=HotKeyManager_Setup
SetupIconFile=

; Komprimierung
Compression=lzma2
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Erscheinungsbild
WizardStyle=modern

; Berechtigungen
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Deinstallation
Uninstallable=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

; Windows-Version
MinVersion=10.0.17763

; Sonstiges
DisableProgramGroupPage=yes
AllowNoIcons=yes

; Update-Unterstützung: Alte Version wird automatisch deinstalliert
UsePreviousAppDir=yes

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Mit Windows starten"; GroupDescription: "Zusätzliche Optionen:"; Flags: unchecked

[Files]
; Alle Dateien aus dem Publish-Verzeichnis
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Hinweis: "ignoreversion" bedeutet, dass Dateien immer überschrieben werden (gut für Updates)

[Icons]
; Startmenü
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName} deinstallieren"; Filename: "{uninstallexe}"

; Desktop (optional)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Autostart-Eintrag (optional)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
; Nach Installation starten (optional)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Vor Deinstallation: App beenden falls sie läuft
Filename: "taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
; Zusätzliche Dateien/Ordner löschen bei Deinstallation
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}"
Type: filesandordirs; Name: "{userappdata}\{#MyAppName}"

[Code]
// Prüfen ob die App bereits läuft
function IsAppRunning(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('tasklist.exe', '/FI "IMAGENAME eq {#MyAppExeName}" /NH', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := (ResultCode = 0);
end;

// Vor Installation: Prüfen ob App läuft
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  // Versuche die App zu beenden falls sie läuft
  Exec('taskkill.exe', '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500); // Kurz warten
end;

// Nach erfolgreicher Deinstallation aufräumen
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Hier können zusätzliche Aufräumarbeiten durchgeführt werden
  end;
end;
