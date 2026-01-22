@echo off
chcp 65001 > nul
setlocal

:: ------------------------------------------------------------------
:: 설정: 빌드된 실행 파일 이름
:: ------------------------------------------------------------------
set "EXE_NAME=MyDDayWidget.exe"
set "LINK_NAME=MyDDayWidget.lnk"

:: ------------------------------------------------------------------
:: 경로 설정
:: ------------------------------------------------------------------
:: 현재 폴더 경로
set "CURRENT_DIR=%~dp0"
:: 실행 파일 전체 경로
set "TARGET_EXE=%CURRENT_DIR%%EXE_NAME%"
:: 윈도우 시작프로그램 폴더 경로
set "STARTUP_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
:: 생성될 바로가기 전체 경로
set "SHORTCUT_PATH=%STARTUP_DIR%\%LINK_NAME%"

echo.
echo ========================================================
echo  윈도우 시작프로그램 자동 등록 도구
echo ========================================================
echo.

:: 1. 실행 파일 존재 확인
if not exist "%TARGET_EXE%" (
    echo [오류] '%EXE_NAME%' 파일을 찾을 수 없습니다.
    echo.
    echo * 중요: 이 파일(install_startup.bat)을 
    echo         '%EXE_NAME%'가 있는 폴더(dist 폴더)에 넣고 실행해야 합니다.
    echo.
    pause
    exit /b
)

echo [1/3] 실행 파일 확인 완료...
echo     위치: %TARGET_EXE%

:: 2. 기존 등록된 바로가기가 있다면 삭제 (갱신)
if exist "%SHORTCUT_PATH%" (
    echo [2/3] 기존 설정을 업데이트합니다...
    del "%SHORTCUT_PATH%"
) else (
    echo [2/3] 시작프로그램 폴더를 확인했습니다...
)

:: 3. PowerShell을 이용해 바로가기 생성 (작업 경로 설정 포함)
echo [3/3] 바로가기 생성 중...

powershell -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%SHORTCUT_PATH%'); $s.TargetPath = '%TARGET_EXE%'; $s.WorkingDirectory = '%CURRENT_DIR%'; $s.Save()"

if exist "%SHORTCUT_PATH%" (
    echo.
    echo ========================================================
    echo  [성공] 등록이 완료되었습니다!
    echo ========================================================
    echo  이제 윈도우를 다시 시작하면 위젯이 자동으로 뜹니다.
    echo.
) else (
    echo.
    echo [실패] 바로가기 생성 중 오류가 발생했습니다.
    echo.
)

pause