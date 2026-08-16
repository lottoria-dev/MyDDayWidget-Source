# 🗓️ My D-Day Widget
바탕화면에서 간편하게 중요한 일정을 관리할 수 있는 데스크톱 D-Day 위젯입니다.

현재 배포 버전은 **v2.6.0**입니다. 프로그램은 무료로 사용할 수 있으며, 소스 코드는 검토와 개인적 개선을 위해 공개하는 **source-available freeware**입니다.

- 공식 안내: https://mathtime.kr/?page=dday
- GitHub 저장소: https://github.com/lottoria-dev/MyDDayWidget-Source
- 최신 배포본: https://github.com/lottoria-dev/MyDDayWidget-Source/releases/latest

## ✨ 주요 기능 (Features)
- 직관적인 D-Day 관리: 여러 개의 D-Day를 등록하고 가까운/먼 날짜순으로 정렬 가능
- D-Day 계산: 남은 날짜(D-x), 당일(D-Day), 지난 날짜(D+x)와 목표 날짜 표시
- 높은 자유도의 커스터마이징:
   - 위젯 전체 투명도 조절
   - 시간 및 날짜 글자 크기 조절
   - 시스템 내장 폰트(글꼴) 변경
   - 글자 색상 및 D-Day 숫자 색상 변경
   - 항상 위에 고정(Topmost) 기능
- 편의성
   - 프레임 없는(Frameless) 창으로 자유로운 드래그 이동 및 크기 조절
   - 시스템 트레이(System Tray) 아이콘을 통한 백그라운드 제어
   - INI 설정 파일 불러오기·내보내기 및 최근 백업 복원
   - 실행 중 보조 모니터가 분리되면 위젯을 주 모니터 중앙으로 자동 이동
   - 포터블 EXE의 Windows 시작 시 자동 실행 및 이동 후 경로 자동 복구
- 완성도 높은 설정 화면(v2.6.0)
   - D-Day 관리를 첫 번째 탭에 배치
   - D-Day 관리·표시 설정·글꼴·색상의 명확한 정보 구조
   - 기능별로 묶인 간결한 글꼴 설정과 실제 글꼴·크기·색상 실시간 미리보기
   - 반투명 팔레트 카드로 구성한 기본·고대비·바다·노을·모노 빠른 테마
   - 시간·날짜 입력과 분리되어 겹치지 않는 원형 글래스 불투명도 다이얼
   - `−`·`+` 버튼, 마우스 드래그·휠, 방향키를 지원하는 세밀한 조절
   - 항상 보이는 고대비 스크롤바로 명확한 스크롤 가능 영역 제공
   - 일정 개수 표시, 카드형 입력 행과 일관된 버튼 디자인
- 최적화와 유지보수성(v2.6.0)
   - D-Day 목표 날짜를 한 번만 해석하고 날짜 또는 설정이 바뀔 때만 남은 일수 재계산
   - 반복 생성되던 테마 스타일·다이얼 경로·시작 프로그램 명령 캐시
   - 설정 검증 단일화와 대용량 파일 복사의 스트리밍 처리
   - 공통 다이얼로그·콤보박스 렌더링 구조로 중복 코드 축소
   - 종료 시 설정 로그 핸들러를 명시적으로 정리
- 설정 보호(v2.2.2)
   - 사용자 설정 폴더의 고정 경로 사용
   - 저장 중 중단되어도 기존 파일을 보호하는 원자적 저장
   - 최근 정상 설정 3세대 자동 백업
   - 손상 파일 보존 및 최근 백업 자동 복구
   - 중복 실행에 의한 설정 덮어쓰기 방지

## 🚀 설치 및 실행 방법 (Installation & Run)
이 프로젝트는 **Python 3.9+** 환경과 **PySide6**를 사용합니다.

일반 사용자는 GitHub Releases에서 `MyDDayWidget.exe`를 내려받으면 됩니다. 별도 설치 과정이 없는 포터블 단일 실행 파일입니다.

- 최신 Release: https://github.com/lottoria-dev/MyDDayWidget-Source/releases/latest
- 최신 EXE 직접 다운로드: https://github.com/lottoria-dev/MyDDayWidget-Source/releases/latest/download/MyDDayWidget.exe

소스 실행과 개발은 가상환경 사용을 권장합니다.

```
# 1. 가상환경 생성 및 활성화
python -m venv .venv
.venv\Scripts\activate

# 2. v2.6.0 검증 버전 설치
python -m pip install PySide6==6.10.2 PyInstaller==6.17.0 Pillow

# 3. 아이콘 생성 (최초 1회)
python icongen.py

# 4. 프로그램 실행
python main.py
```

Windows EXE는 `build.bat`으로 생성합니다. 프로젝트 경로에 공백, 한글, 괄호가 포함되어 있어도 사용할 수 있습니다. 정식 배포 빌드는 위의 검증 버전과 실제 빌드에 사용한 Python 세부 버전을 Release 설명에 기록해야 합니다.

## 🖱 사용 방법 (How to Use)
- 위젯 이동: 위젯의 빈 공간을 마우스 왼쪽 버튼으로 클릭한 채 드래그합니다.
- 크기 조절: 위젯 우측 하단의 ◢ 아이콘을 클릭하여 드래그합니다.
- 설정 열기: 위젯을 더블 클릭하거나, 우클릭 메뉴에서 설정 편집을 선택합니다.
- 설정 파일 관리: 환경 설정 하단에서 INI 불러오기·내보내기·최근 백업 복원을 사용할 수 있습니다.
- 숨기기/종료: 작업 표시줄 우측 하단의 시스템 트레이 아이콘을 우클릭하여 제어할 수 있습니다.
- 자동 실행: 배포 EXE에서 `표시 설정 > Windows 시작 시 자동 실행`을 선택합니다.

## 🧳 포터블 자동 실행

- 별도의 설치 과정 없이 `MyDDayWidget.exe` 하나로 사용할 수 있습니다.
- 자동 실행은 현재 사용자의 Windows 시작 프로그램에 현재 EXE 경로를 등록합니다.
- EXE를 이동한 직후에는 기존 위치를 찾지 못하므로 자동 실행되지 않을 수 있습니다.
- 이동한 EXE를 한 번 직접 실행하면 등록 경로가 새 위치로 자동 갱신됩니다.
- EXE를 삭제하기 전 자동 실행 옵션을 끄면 등록 정보도 함께 제거됩니다.
- Python 소스 실행에서는 잘못된 Python 경로 등록을 막기 위해 이 옵션이 비활성화됩니다.

## 💾 설정 파일과 백업

- Windows에서는 Qt 사용자 설정 폴더(AppConfigLocation)에 `dday_config.ini`를 저장합니다.
- 기존 버전에서 EXE 또는 소스 옆에 생성된 `dday_config.ini`는 최초 실행 때 새 위치로 복사합니다.
- 정상 저장 때 `dday_config.bak1.ini`부터 `dday_config.bak3.ini`까지 순환 백업합니다.
- 기본 INI가 손상되면 손상 파일을 `dday_config.corrupt-날짜시간.ini`로 보존하고 최근 정상 백업을 복구합니다.
- 설정 폴더에는 문제 확인용 `dday_widget.log`도 생성됩니다.
- 부분 복구 상태에서는 종료 시 기본값으로 자동 덮어쓰지 않습니다. 설정 화면에서 내용을 확인하고 저장해야 정상 파일로 확정됩니다.

설정창 하단의 `설정 내보내기`를 이용하면 현재 구성을 별도 INI 파일로 보관하거나 다른 PC로 옮길 수 있습니다.

## 🔐 다운로드 무결성 확인

GitHub Release에는 `MyDDayWidget.exe`와 함께 `MyDDayWidget_SHA256.txt`를 게시합니다. Windows 명령 프롬프트에서 다음 명령으로 내려받은 EXE의 해시를 확인할 수 있습니다.

```
certutil -hashfile MyDDayWidget.exe SHA256
```

출력값이 Release에 첨부된 SHA-256 값과 다르면 실행하지 마세요. 개인 개발자 배포본이라 디지털 서명이 없으며 Windows SmartScreen 경고가 나타날 수 있습니다.

## 🔧 Qt/PySide6 교체와 재빌드

이 프로젝트는 Qt/PySide6를 동적으로 로드하는 Python 애플리케이션을 PyInstaller로 묶습니다. 사용자는 LGPLv3가 허용하는 범위에서 PySide6/Qt를 수정하거나 다른 호환 버전으로 교체한 뒤 프로그램을 다시 빌드할 수 있습니다.

1. 저장소를 clone 또는 fork합니다.
2. 가상환경에 원하는 PySide6/Qt 호환 버전을 설치합니다.
3. 자동 테스트를 실행합니다.
4. `build.bat`으로 새 EXE를 만듭니다.

수정된 Qt/PySide6 구성요소의 이용과 재배포에는 해당 구성요소의 원 라이선스가 적용됩니다.

설정 저장과 복구 로직의 자동 테스트는 다음 명령으로 실행할 수 있습니다.

```
python -m unittest discover -s tests -v
```

## 📁 프로젝트 구조 (Project Structure).
```
├── main.py              # 메인 실행 파일
├── ui_main.py           # D-Day 위젯 메인 UI 및 로직
├── ui_settings.py       # 설정 다이얼로그 및 정보창 UI
├── glass_theme.py       # 글래스 테마 스타일시트(CSS) 및 렌더링 설정
├── config_manager.py    # dday_config.ini 파일 저장 및 로드 관리
├── startup_manager.py   # 포터블 EXE 시작 프로그램 등록 및 경로 복구
├── utils.py             # 날짜 계산 및 리소스 경로 관리 유틸리티
├── icongen.py           # 앱 아이콘(.ico, .png) 생성 스크립트
├── build.bat            # PyInstaller 기반 Windows EXE 자동 빌드 스크립트
└── CODE_REVIEW.md       # v2.6.0 정적 검토·최적화 결과
```

## 📄 라이선스 및 배포 (License)

My D-Day Widget 자체 코드는 `LICENSE.md`의 **Source-Available Freeware License**를 따릅니다.

- 개인·교육기관·공공기관·기업에서 무료로 사용할 수 있습니다.
- 공개 소스를 학습·검토하고 개인적으로 수정·재빌드할 수 있습니다.
- GitHub 서비스 안의 fork는 허용됩니다.
- 원본·수정본의 외부 재배포, 실행 파일 재판매와 공식판으로 오인할 수 있는 수정본 배포는 금지됩니다.
- 일반적인 아이디어나 알고리즘이 아니라 저작권으로 보호되는 구체적인 소스 코드 표현을 보호합니다.

PySide6, Qt, Python, PyInstaller와 Pillow에는 각각의 원 라이선스가 우선 적용됩니다. 특히 본 프로젝트의 자체 라이선스는 LGPLv3가 허용하는 Qt/PySide6의 분석·수정·교체·재링크 권리를 제한하지 않습니다. 제3자 라이선스와 상응하는 소스 제공 안내는 `LICENSE.md` 제7조에 정리되어 있습니다.

정식 배포 시에는 다음 자료를 함께 게시해야 합니다.

- `MyDDayWidget.exe`
- `MyDDayWidget_SHA256.txt`
- 현재 `LICENSE.md`
- 실제 배포본에 포함된 Qt/PySide6의 정확한 버전과 상응하는 소스 제공 안내
- LGPLv3·GPLv3·Python 등 제3자 라이선스 전문과 저작권 고지를 담은 별도 라이선스 자료

## 개발자 정보
- 개발일: 2026.01.21
- 최근 업데이트: 2026.08.16
- 공식 배포 페이지: https://mathtime.kr/?page=dday
- GitHub: https://github.com/lottoria-dev/MyDDayWidget-Source
- 문의: mathtime.ai@gmail.com
- Copyright 2026 lottoria-dev. All rights reserved.
- 공식 안내 페이지와 GitHub Releases 이외의 실행 파일은 배포자가 무결성을 보증하지 않습니다.
