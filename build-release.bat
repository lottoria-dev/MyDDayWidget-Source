@echo off
setlocal
cd /d "%~dp0"

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "MSBUILD_EXE="
if exist "%VSWHERE%" for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD_EXE=%%i"
if not defined MSBUILD_EXE set "MSBUILD_EXE=msbuild"

echo [D-Day 3] Release x64 build - diagnostics excluded
"%MSBUILD_EXE%" DDay3.sln /m /t:Restore,Rebuild /p:Configuration=Release /p:Platform=x64
if errorlevel 1 (
  echo [ERROR] Release build failed.
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File tools\package-release.ps1
if errorlevel 1 exit /b 1
echo [OK] artifacts\v3.5.4 - EXE, portable ZIP and SHA-256
endlocal
