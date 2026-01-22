@echo off
chcp 65001 > nul
setlocal

echo ========================================================
echo  D-Day 위젯 EXE 빌드 도구 (Icon 포함)
echo ========================================================
echo.

:: 1. 필수 라이브러리 설치 확인 및 설치
echo [1/4] 필수 라이브러리 확인 중...
python -m pip install pyinstaller pillow PyQt6

:: 2. 아이콘 파일 확인 (생성 과정 생략)
echo.
echo [2/4] 아이콘 파일 확인 중...

if not exist "icon.ico" (
    echo [오류] icon.ico 파일을 찾을 수 없습니다.
    echo create_icon.py를 먼저 실행하여 아이콘을 생성해주세요.
    pause
    exit /b
)

:: 3. PyInstaller로 EXE 빌드
echo.
echo [3/4] 실행 파일(EXE) 빌드 시작...
:: --add-data "icon.png;.": icon.png 파일을 exe 내부(루트)로 포함시킴
python -m PyInstaller -w -F --clean --icon="icon.ico" --add-data "icon.png;." --name="MyDDayWidget" "dday.py"

:: 4. 실행에 필요한 리소스 복사
echo.
echo [4/4] 마무리 작업 중...
:: 빌드된 exe가 있는 dist 폴더로 설정 파일과 이미지 복사
if exist "dist\MyDDayWidget.exe" (
    
    :: 자동 실행 등록 배치 파일 복사 (배포 패키지에 포함)
    if exist "install_startup.bat" copy "install_startup.bat" "dist\" > nul
    
    echo.
    echo ========================================================
    echo  [성공] 빌드가 완료되었습니다!
    echo ========================================================
    echo.
    echo  생성된 파일 위치: %~dp0dist
    echo.
    echo  [배포 시 주의사항]
    echo  'dist' 폴더 안의 'MyDDayWidget.exe' 파일만 있어도 실행됩니다.
    echo  (자동실행이 필요하면 install_startup.bat 도 함께 배포)
    echo.
    
    :: 결과 폴더 열기
    explorer "dist"
) else (
    echo.
    echo [실패] 빌드 중 오류가 발생했습니다.
)

pause