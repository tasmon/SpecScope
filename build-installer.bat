@echo off
setlocal
set VERSION=1.2.0

call build.bat || exit /b 1

rem Optional code signing: set CERT_THUMBPRINT to a certificate in your store.
set SIGN=
if defined CERT_THUMBPRINT set SIGN=signtool sign /sha1 %CERT_THUMBPRINT% /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /d "SpecScope" /du "mailto:tasmon@outlook.com"
if defined SIGN %SIGN% dist\SpecScope.exe

set MAKENSIS=%ProgramFiles(x86)%\NSIS\makensis.exe
if not exist "%MAKENSIS%" set MAKENSIS=%ProgramFiles%\NSIS\makensis.exe
if not exist "%MAKENSIS%" (echo NSIS not found. Install it with: winget install NSIS.NSIS & exit /b 1)
"%MAKENSIS%" /V2 /DVERSION=%VERSION% installer\SpecScope.nsi || exit /b 1

if defined SIGN %SIGN% dist\SpecScope-Setup-%VERSION%.exe
echo.
certutil -hashfile dist\SpecScope-Setup-%VERSION%.exe SHA256
