; SpecScope installer – build with:  makensis /DVERSION=1.2.0 installer\SpecScope.nsi
Unicode True
!ifndef VERSION
  !define VERSION "1.2.0"
!endif
!define APP        "SpecScope"
!define PUBLISHER  "Tasmon Islam"
!define EMAIL      "tasmon@outlook.com"
!define UNINSTKEY  "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP}"

Name "${APP} ${VERSION}"
OutFile "..\dist\${APP}-Setup-${VERSION}.exe"
InstallDir "$PROGRAMFILES64\${APP}"
InstallDirRegKey HKLM "Software\${APP}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "${APP} ${VERSION}  |  ${PUBLISHER}"

!include "MUI2.nsh"
!include "x64.nsh"

; ---- version info embedded in the installer itself
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName"     "${APP}"
VIAddVersionKey "CompanyName"     "${PUBLISHER}"
VIAddVersionKey "LegalCopyright"  "Copyright (c) 2026 ${PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP} Setup"
VIAddVersionKey "FileVersion"     "${VERSION}.0"
VIAddVersionKey "ProductVersion"  "${VERSION}"

!define MUI_ICON   "..\src\app.ico"
!define MUI_UNICON "..\src\app.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP}.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${APP}"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApp
!define MUI_FINISHPAGE_LINK "Contact the developer: ${EMAIL}"
!define MUI_FINISHPAGE_LINK_LOCATION "mailto:${EMAIL}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  SetRegView 64
FunctionEnd

Function un.onInit
  SetRegView 64
FunctionEnd

Function CleanLegacyShortcuts
  SetShellVarContext current
  Delete "$SMPROGRAMS\${APP}\${APP}.lnk"
  Delete "$SMPROGRAMS\${APP}\Uninstall ${APP}.lnk"
  RMDir  "$SMPROGRAMS\${APP}"
  Delete "$DESKTOP\${APP}.lnk"
  SetShellVarContext all
  Delete "$SMPROGRAMS\${APP}\${APP}.lnk"
  Delete "$SMPROGRAMS\${APP}\Uninstall ${APP}.lnk"
  RMDir  "$SMPROGRAMS\${APP}"
FunctionEnd

Function un.CleanShortcuts
  SetShellVarContext current
  Delete "$SMPROGRAMS\${APP}.lnk"
  Delete "$SMPROGRAMS\${APP}\${APP}.lnk"
  Delete "$SMPROGRAMS\${APP}\Uninstall ${APP}.lnk"
  RMDir  "$SMPROGRAMS\${APP}"
  Delete "$DESKTOP\${APP}.lnk"
  SetShellVarContext all
  Delete "$SMPROGRAMS\${APP}.lnk"
  Delete "$SMPROGRAMS\${APP}\${APP}.lnk"
  Delete "$SMPROGRAMS\${APP}\Uninstall ${APP}.lnk"
  RMDir  "$SMPROGRAMS\${APP}"
  Delete "$DESKTOP\${APP}.lnk"
FunctionEnd

; Start the app as the normal user (not elevated) after setup
Function LaunchApp
  Exec '"$WINDIR\explorer.exe" "$INSTDIR\${APP}.exe"'
FunctionEnd

Section "${APP} (required)" SecMain
  SectionIn RO
  nsExec::Exec 'taskkill /F /IM ${APP}.exe'
  Sleep 400
  SetOutPath "$INSTDIR"
  File "..\dist\${APP}.exe"
  File "..\LICENSE"
  File "..\README.md"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; Clean up shortcuts/folders from older versions (they used a "SpecScope" Start-menu subfolder)
  Call CleanLegacyShortcuts
  ; Shortcut directly in the Start menu list - no subfolder. Uninstall via Settings > Apps.
  SetShellVarContext all
  CreateShortcut "$SMPROGRAMS\${APP}.lnk" "$INSTDIR\${APP}.exe" "" "$INSTDIR\${APP}.exe" 0

  WriteRegStr HKLM "Software\${APP}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayName"          "${APP}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayVersion"       "${VERSION}"
  WriteRegStr HKLM "${UNINSTKEY}" "Publisher"            "${PUBLISHER}"
  WriteRegStr HKLM "${UNINSTKEY}" "Contact"              "${EMAIL}"
  WriteRegStr HKLM "${UNINSTKEY}" "HelpLink"             "mailto:${EMAIL}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayIcon"          "$INSTDIR\${APP}.exe"
  WriteRegStr HKLM "${UNINSTKEY}" "InstallLocation"      "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "UninstallString"      '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoRepair" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "EstimatedSize" 400
SectionEnd

Section "Desktop shortcut" SecDesktop
  SetShellVarContext all
  CreateShortcut "$DESKTOP\${APP}.lnk" "$INSTDIR\${APP}.exe"
SectionEnd

LangString DESC_Main    ${LANG_ENGLISH} "The ${APP} application and live tray monitor."
LangString DESC_Desktop ${LANG_ENGLISH} "Create a shortcut on the desktop."
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMain}    $(DESC_Main)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} $(DESC_Desktop)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  nsExec::Exec 'taskkill /F /IM ${APP}.exe'
  Sleep 400
  Delete "$INSTDIR\${APP}.exe"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\README.md"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir  "$INSTDIR"
  Call un.CleanShortcuts
  DeleteRegKey HKLM "${UNINSTKEY}"
  DeleteRegKey HKLM "Software\${APP}"
  ; per-user leftovers of the installing user (autostart + settings)
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP}"
  DeleteRegKey HKCU "Software\${APP}"
SectionEnd
