@echo off
chcp 65001 > nul
setlocal

pushd "%~dp0"
if errorlevel 1 goto :path_failed

echo ========================================================
echo  D-Day 위젯 EXE 빌드 도구 (v2.6.1)
echo ========================================================
echo.

echo [1/5] 필수 라이브러리 확인 중...
python -m pip install pyinstaller pillow PySide6
if errorlevel 1 goto :dependency_failed

echo.
echo [2/5] 아이콘 파일 확인 중...
if not exist "icon.ico" goto :icon_missing

echo.
echo [3/5] 실행 파일(EXE) 빌드 시작...
taskkill /IM "MyDDayWidget.exe" /F > nul 2>&1
if not exist "dist\MyDDayWidget.exe" goto :run_build
del /Q "dist\MyDDayWidget.exe" > nul 2>&1
if exist "dist\MyDDayWidget.exe" goto :exe_locked

:run_build
python -m PyInstaller -w -F --clean --icon="icon.ico" --add-data "icon.png;." --name="MyDDayWidget" "main.py"
if errorlevel 1 goto :build_failed
if not exist "dist\MyDDayWidget.exe" goto :build_failed

echo.
echo [4/5] 마무리 작업 중...
echo.
echo [5/5] SHA-256 체크섬 생성 중...
certutil -hashfile "dist\MyDDayWidget.exe" SHA256 > "dist\MyDDayWidget_SHA256.txt"
if errorlevel 1 goto :hash_failed

echo SHA-256 해시값이 dist\MyDDayWidget_SHA256.txt 파일에 저장되었습니다.
echo.
echo ========================================================
echo  [성공] 빌드가 완료되었습니다!
echo ========================================================
echo.
echo  생성된 파일 위치: "%~dp0dist"
echo  체크섬 파일 위치: "%~dp0dist\MyDDayWidget_SHA256.txt"
echo.
echo  [배포 시 주의사항]
echo  dist 폴더 안의 MyDDayWidget.exe 파일만 있어도 실행됩니다.
echo  시작 프로그램 등록은 EXE의 설정 창에서 선택할 수 있습니다.
echo.
explorer "%~dp0dist"
popd
pause
exit /b 0

:icon_missing
echo [오류] icon.ico 파일을 찾을 수 없습니다.
echo icongen.py를 먼저 실행하여 아이콘을 생성해 주세요.
goto :failed

:exe_locked
echo [오류] dist\MyDDayWidget.exe 파일을 사용할 수 없습니다.
echo 프로그램과 파일 탐색기 미리보기를 닫은 뒤 다시 실행해 주세요.
goto :failed

:dependency_failed
echo.
echo [오류] 필수 라이브러리를 준비하지 못했습니다.
goto :failed

:build_failed
echo.
echo [오류] PyInstaller 빌드에 실패했습니다.
goto :failed

:hash_failed
echo.
echo [오류] SHA-256 체크섬을 생성하지 못했습니다.
goto :failed

:failed
popd
pause
exit /b 1

:path_failed
echo [오류] 프로젝트 폴더로 이동하지 못했습니다.
pause
exit /b 1
