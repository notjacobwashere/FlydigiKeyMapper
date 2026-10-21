Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!include "WinVer.nsh"
!ifndef VERSION
 !define VERSION "0.1.0"
!endif
Name "Flydigi Key Mapper ${VERSION}"
OutFile "..\artifacts\FlydigiKeyMapper-Setup-${VERSION}-x64.exe"
InstallDir "$PROGRAMFILES64\Flydigi Key Mapper"
InstallDirRegKey HKLM "Software\FlydigiKeyMapper" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "Flydigi Key Mapper"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "FileDescription" "Flydigi Key Mapper Setup"
VIAddVersionKey "LegalCopyright" "Copyright notjacobwashere"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TEXT "Map the Vader 5S extra buttons to keyboard and numpad keys.\r\n\r\nRequires Flydigi Space Station for Xbox from Microsoft Store. Its proprietary files are not bundled.\r\n\r\nSetup can install USBPcap's official signed USB filter. Restart Windows afterward. Only one Vader 5S is supported at a time."
!define MUI_FINISHPAGE_NOREBOOTSUPPORT
!define MUI_FINISHPAGE_TEXT "Setup is complete. If USBPcap was installed, save your work and restart Windows before using the mapper. Setup will not restart your PC automatically.\r\n\r\nInstall Flydigi Space Station for Xbox from Microsoft Store if needed, then close it while using the mapper."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
Section "Flydigi Key Mapper" App
 SectionIn RO
 SetRegView 64
 SetShellVarContext all
 SetOutPath "$INSTDIR"
 File /r /x "*.pdb" /x "*.lib" /x "*.exp" /x "settings.json" /x "*-preview.png" /x "VendorRuntimeCache" "..\artifacts\app\*"
 File "..\LICENSE"
 File "..\THIRD-PARTY-NOTICES.md"
 SetOutPath "$INSTDIR\third-party"
 File "..\artifacts\dependencies\usbpcap-src-incl-pdb-1.5.4.0.7z"
 File "..\artifacts\dependencies\USBPcap-LICENSES.txt"
 WriteUninstaller "$INSTDIR\Uninstall.exe"
 WriteRegStr HKLM "Software\FlydigiKeyMapper" "InstallDir" "$INSTDIR"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FlydigiKeyMapper" "DisplayName" "Flydigi Key Mapper"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FlydigiKeyMapper" "DisplayVersion" "${VERSION}"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FlydigiKeyMapper" "Publisher" "notjacobwashere"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FlydigiKeyMapper" "UninstallString" '"$INSTDIR\Uninstall.exe"'
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FlydigiKeyMapper" "DisplayIcon" "$INSTDIR\FlydigiKeyMapper.exe"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FlydigiKeyMapper" "URLInfoAbout" "https://github.com/notjacobwashere/FlydigiKeyMapper"
 WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FlydigiKeyMapper" "NoModify" 1
 WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FlydigiKeyMapper" "NoRepair" 1
 CreateDirectory "$SMPROGRAMS\Flydigi Key Mapper"
 CreateShortcut "$SMPROGRAMS\Flydigi Key Mapper\Flydigi Key Mapper.lnk" "$INSTDIR\FlydigiKeyMapper.exe"
 CreateShortcut "$SMPROGRAMS\Flydigi Key Mapper\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
 WriteINIStr "$SMPROGRAMS\Flydigi Key Mapper\Flydigi Store app.url" "InternetShortcut" "URL" "https://apps.microsoft.com/detail/9NB9DQG1BN6P"
 ExecWait '"$INSTDIR\FlydigiKeyMapper.exe" --refresh-startup' $0
SectionEnd
Section "USBPcap driver (restart required if missing)" Driver
 SetRegView 64
 ReadRegStr $0 HKLM "SYSTEM\CurrentControlSet\Services\USBPcap" "ImagePath"
 ${If} $0 == ""
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File "..\artifacts\dependencies\USBPcapSetup-1.5.4.0.exe"
  ExecWait '"$PLUGINSDIR\USBPcapSetup-1.5.4.0.exe" /S' $0
  ReadRegStr $1 HKLM "SYSTEM\CurrentControlSet\Services\USBPcap" "ImagePath"
  ${If} $0 != 0
  ${OrIf} $1 == ""
   MessageBox MB_ICONEXCLAMATION "USBPcap installation could not be confirmed. Install it from https://desowin.org/usbpcap/ before using the mapper."
   SetErrorLevel 1
  ${Else}
   SetRebootFlag true
  ${EndIf}
 ${Else}
  DetailPrint "Existing USBPcap installation kept."
 ${EndIf}
SectionEnd
Function CheckClosed
 retry:
 System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\FlydigiKeyMapper") p.r0'
 ${If} $0 != 0
  System::Call 'kernel32::CloseHandle(p r0)'
  MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "Exit Flydigi Key Mapper using its tray menu, then choose Retry." IDRETRY retry
  Abort
 ${EndIf}
FunctionEnd
Function .onInit
 ${IfNot} ${AtLeastWin10}
  MessageBox MB_ICONSTOP "Windows 10 or newer is required."
  Abort
 ${EndIf}
 ${IfNot} ${RunningX64}
  MessageBox MB_ICONSTOP "This release requires 64-bit Windows."
  Abort
 ${EndIf}
 Call CheckClosed
FunctionEnd
Function un.onInit
 System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\FlydigiKeyMapper") p.r0'
 ${If} $0 != 0
  System::Call 'kernel32::CloseHandle(p r0)'
  MessageBox MB_ICONSTOP "Exit Flydigi Key Mapper using its tray menu before uninstalling."
  Abort
 ${EndIf}
FunctionEnd
Section "Uninstall"
 SetRegView 64
 SetShellVarContext all
 ExecWait '"$INSTDIR\FlydigiKeyMapper.exe" --remove-startup' $0
 Delete "$SMPROGRAMS\Flydigi Key Mapper\Flydigi Key Mapper.lnk"
 Delete "$SMPROGRAMS\Flydigi Key Mapper\Uninstall.lnk"
 Delete "$SMPROGRAMS\Flydigi Key Mapper\Flydigi Store app.url"
 RMDir "$SMPROGRAMS\Flydigi Key Mapper"
 ; The dedicated installation directory contains app/runtime files only.
 ; Preserve per-user AppData mappings and the shared USBPcap installation.
 !include "uninstall-files.nsh"
 Delete "$INSTDIR\VendorRuntimeCache\D4XGaming.Devices.dll"
 Delete "$INSTDIR\VendorRuntimeCache\MSVCP140_APP.dll"
 Delete "$INSTDIR\VendorRuntimeCache\VCRUNTIME140_APP.dll"
 Delete "$INSTDIR\VendorRuntimeCache\VCRUNTIME140_1_APP.dll"
 RMDir "$INSTDIR\VendorRuntimeCache"
 Delete "$INSTDIR\LICENSE"
 Delete "$INSTDIR\THIRD-PARTY-NOTICES.md"
 Delete "$INSTDIR\third-party\usbpcap-src-incl-pdb-1.5.4.0.7z"
 Delete "$INSTDIR\third-party\USBPcap-LICENSES.txt"
 RMDir "$INSTDIR\third-party"
 Delete "$INSTDIR\Uninstall.exe"
 RMDir "$INSTDIR"
 DeleteRegKey HKLM "Software\FlydigiKeyMapper"
 DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FlydigiKeyMapper"
SectionEnd





