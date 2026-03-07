# 🗓️ My D-Day Widget
바탕화면에서 간편하게 중요한 일정을 관리할 수 있는 데스크톱 D-Day 위젯입니다.

## ✨ 주요 기능 (Features)
- 직관적인 D-Day 관리: 여러 개의 D-Day를 등록하고 가까운/먼 날짜순으로 정렬 가능
- 상세한 날짜 계산: 남은 날짜(D-x) 및 지난 날짜(D+x) 표시뿐만 아니라, 연/월/일 단위의 상세 기간 표시
- 높은 자유도의 커스터마이징:
   - 위젯 전체 투명도 조절
   - 시간 및 날짜 글자 크기 조절
   - 시스템 내장 폰트(글꼴) 변경
   - 글자 색상 및 D-Day 숫자 색상 변경
   - 항상 위에 고정(Topmost) 기능
- 편의성
   - 프레임 없는(Frameless) 창으로 자유로운 드래그 이동 및 크기 조절
   - 시스템 트레이(System Tray) 아이콘을 통한 백그라운드 제어

## 🚀 설치 및 실행 방법 (Installation & Run)
먼저 Python 3.x가 설치되어 있어야 합니다. 레포지토리를 클론한 후, 필수 라이브러리를 설치하고 실행합니다.
```
# 1. 필수 라이브러리 설치 (PySide6 기준)
pip install PySide6 pillow

# 3. 아이콘 생성 (최초 1회)
python icongen.py

# 4. 프로그램 실행
python main.py
```

## 🖱 사용 방법 (How to Use)
- 위젯 이동: 위젯의 빈 공간을 마우스 왼쪽 버튼으로 클릭한 채 드래그합니다.
- 크기 조절: 위젯 우측 하단의 ◢ 아이콘을 클릭하여 드래그합니다.
- 설정 열기: 위젯을 더블 클릭하거나, 우클릭 메뉴에서 설정 편집을 선택합니다.
- 숨기기/종료: 작업 표시줄 우측 하단의 시스템 트레이 아이콘을 우클릭하여 제어할 수 있습니다.

## 📁 프로젝트 구조 (Project Structure).
```
├── main.py              # 메인 실행 파일
├── ui_main.py           # D-Day 위젯 메인 UI 및 로직
├── ui_settings.py       # 설정 다이얼로그 및 정보창 UI
├── glass_theme.py       # 글래스 테마 스타일시트(CSS) 및 렌더링 설정
├── config_manager.py    # dday_config.ini 파일 저장 및 로드 관리
├── utils.py             # 날짜 계산 및 리소스 경로 관리 유틸리티
├── icongen.py           # 앱 아이콘(.ico, .png) 생성 스크립트
└── build.bat            # PyInstaller 기반 Windows EXE 자동 빌드 스크립트
```

## 📄 라이선스 및 배포 (License)
- 개발일: 2026.01.21
- 공식 배포 페이지: https://mathtime.kr/dday.html
- Copyright 2026 lottoria-dev. All rights reserved.
- ⚠️ 정식 배포 페이지를 제외한 곳에서 임의의 수정 및 재배포를 금지합니다.
