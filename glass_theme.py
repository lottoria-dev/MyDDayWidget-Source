from PySide6.QtGui import QColor, QBrush

# ---------------------------------------------------------
# Prism Dock Style Identity
# ---------------------------------------------------------
# 배경색: 순수 화이트 (더 깨끗한 유리 느낌)
GLASS_COLOR_RGB = (255, 255, 255)
# 투명도: 이미지처럼 내용이 잘 보이도록 약간 불투명하게 (0~255)
DIALOG_ALPHA = 230 
MENU_ALPHA = 230

# 포인트 컬러 (이미지의 파란색)
ACCENT_COLOR = "#0078D7"
# 텍스트 컬러
TEXT_COLOR = "#202020"

def get_glass_background_brush():
    """다이얼로그 배경 페인팅을 위한 브러시 반환"""
    return QBrush(QColor(*GLASS_COLOR_RGB, DIALOG_ALPHA))

def get_glass_menu_style():
    """우클릭 메뉴용 스타일시트"""
    return f"""
        QMenu {{
            background-color: rgba({GLASS_COLOR_RGB[0]}, {GLASS_COLOR_RGB[1]}, {GLASS_COLOR_RGB[2]}, {MENU_ALPHA});
            border: 1px solid rgba(0, 0, 0, 0.1);
            border-radius: 12px;
            padding: 8px;
        }}
        QMenu::item {{
            color: {TEXT_COLOR};
            background-color: transparent;
            padding: 6px 12px;
            margin: 2px;
            border-radius: 6px;
            font-family: 'Segoe UI';
            font-size: 10pt;
        }}
        QMenu::item:selected {{
            background-color: {ACCENT_COLOR}; /* 선택 시 파란색 */
            color: #ffffff; /* 텍스트는 흰색 */
        }}
        QMenu::separator {{
            height: 1px;
            background: rgba(0, 0, 0, 0.1);
            margin: 4px 8px;
        }}
    """

def get_glass_dialog_style():
    """설정창 및 공통 위젯용 스타일시트 (Prism Dock 스타일)"""
    return f"""
        QDialog, QWidget {{
            background: transparent;
            font-family: 'Segoe UI';
            color: {TEXT_COLOR};
        }}
        
        /* 라벨 & 체크박스 */
        QLabel, QCheckBox {{
            color: {TEXT_COLOR};
            font-size: 10pt;
            font-weight: 500;
        }}
        QCheckBox::indicator {{
            width: 18px;
            height: 18px;
            border-radius: 4px;
            border: 1px solid #bbb;
            background: white;
        }}
        QCheckBox::indicator:checked {{
            background-color: {ACCENT_COLOR};
            border: 1px solid {ACCENT_COLOR};
            image: url(none); /* 체크 아이콘 대신 색상으로 표현하거나 커스텀 가능 */
        }}

        /* 2. 슬라이더 (세로 두께 축소 및 입체감 조절) */
        QSlider::groove:horizontal {{
            border: 1px solid #b0b0b0;
            height: 4px; /* 기존 8px에서 4px로 축소 */
            background: qlineargradient(x1:0, y1:0, x2:0, y2:1, stop:0 #dcdcdc, stop:1 #f8f8f8);
            margin: 2px 0;
            border-radius: 2px;
        }}
        QSlider::handle:horizontal {{
            background: qlineargradient(x1:0, y1:0, x2:0, y2:1, stop:0 #ffffff, stop:1 #e0e0e0);
            border: 1px solid #888888;
            width: 14px;
            height: 14px;
            margin: -5px 0; /* 슬라이더 바 중앙에 위치하도록 여백 조정 */
            border-radius: 7px;
        }}
        QSlider::handle:horizontal:hover {{
            background: qlineargradient(x1:0, y1:0, x2:0, y2:1, stop:0 #ffffff, stop:1 #f0f0f0);
            border: 1px solid #555555;
        }}
        QSlider::sub-page:horizontal {{
            background: qlineargradient(x1:0, y1:0, x2:0, y2:1, stop:0 #4ba3e3, stop:1 {ACCENT_COLOR});
            border: 1px solid #005a9e;
            border-radius: 2px;
        }}

        /* 1 & 5. 버튼 스타일 (입체감 추가 및 X버튼 잘림 방지를 위한 패딩 최적화) */
        QPushButton {{
            background: qlineargradient(x1:0, y1:0, x2:0, y2:1, stop:0 #ffffff, stop:1 #f0f0f0);
            border: 1px solid #cccccc;
            border-bottom: 2px solid #b0b0b0; /* 입체감을 위한 하단 테두리 */
            border-radius: 6px;
            padding: 4px 8px; /* X버튼 잘림 현상 방지를 위해 좌우 패딩을 8px로 줄임 */
            color: {TEXT_COLOR};
            font-size: 10pt;
        }}
        QPushButton:hover {{
            background: qlineargradient(x1:0, y1:0, x2:0, y2:1, stop:0 #f8f9fa, stop:1 #e8e8e8);
            border-bottom: 2px solid #909090; /* 호버 시 약간 눌린 느낌 */
        }}
        QPushButton:pressed {{
            background: qlineargradient(x1:0, y1:0, x2:0, y2:1, stop:0 #e0e0e0, stop:1 #f0f0f0);
            border-top: 2px solid #b0b0b0; /* 눌렸을 때 음각 효과 */
            border-bottom: 1px solid #cccccc;
            padding-top: 6px; 
            padding-left: 10px;
        }}
        
        /* 입력창 & 콤보박스 (흰색 배경, 둥근 모서리) */
        QLineEdit, QComboBox, QSpinBox, QDateEdit {{
            background-color: #ffffff;
            border: 1px solid #dcdcdc;
            border-radius: 6px;
            padding: 4px 8px;
            color: {TEXT_COLOR};
            selection-background-color: {ACCENT_COLOR};
            selection-color: white;
            min-height: 22px;
        }}
        QDateEdit {{
            min-width: 105px; /* 날짜 잘림 현상 방지를 위해 여유 너비 확보 */
        }}
        
        /* 3. 콤보박스 및 날짜입력창 역삼각형 가시성 문제 해결 */
        QComboBox::drop-down, QDateEdit::drop-down {{
            subcontrol-origin: padding;
            subcontrol-position: top right;
            width: 22px;
            border-left: 1px solid #dcdcdc;
            background-color: transparent;
            border-top-right-radius: 5px;
            border-bottom-right-radius: 5px;
        }}
        QComboBox::down-arrow, QDateEdit::down-arrow {{
            /* 안전한 렌더링을 위해 Base64로 인코딩된 SVG 적용 */
            image: url("data:image/svg+xml;base64,PHN2ZyB4bWxucz0naHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmcnIHdpZHRoPScxMicgaGVpZ2h0PScxMicgdmlld0JveD0nMCAwIDI0IDI0JyBmaWxsPSdub25lJyBzdHJva2U9JyM1NTU1NTUnIHN0cm9rZS13aWR0aD0nMycgc3Ryb2tlLWxpbmVjYXA9J3JvdW5kJyBzdHJva2UtbGluZWpvaW49J3JvdW5kJz48cG9seWxpbmUgcG9pbnRzPSc2IDkgMTIgMTUgMTggOSc+PC9wb2x5bGluZT48L3N2Zz4=");
            width: 12px;
            height: 12px;
        }}

        /* 스크롤바 (미니멀) */
        QScrollBar:vertical {{
            border: none;
            background: transparent;
            width: 8px;
            margin: 0px;
        }}
        QScrollBar::handle:vertical {{
            background: #d0d0d0;
            min-height: 20px;
            border-radius: 4px;
        }}
        QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{
            height: 0px;
        }}
        
        /* 그룹박스 및 라인 */
        QFrame[frameShape="4"] {{ /* HLine */
            color: #e0e0e0; 
        }}
    """

def get_calendar_style():
    """4. 캘린더 위젯용 스타일시트 (날짜 가려짐 현상 및 다크모드 충돌 해결)"""
    return f"""
        QCalendarWidget QWidget#qt_calendar_navigationbar {{
            background-color: #f0f0f0;
            border-bottom: 1px solid #dcdcdc;
        }}
        QCalendarWidget QTableView {{
            background-color: #ffffff;
            selection-background-color: {ACCENT_COLOR};
            selection-color: white;
            alternate-background-color: #f9f9f9;
            gridline-color: #eeeeee;
        }}
        QCalendarWidget QToolButton {{
            color: {TEXT_COLOR};
            background-color: transparent;
            border: none;
            border-radius: 4px;
            padding: 4px;
        }}
        QCalendarWidget QToolButton:hover {{
            background-color: #e0e0e0;
        }}
        QCalendarWidget QMenu {{
            background-color: #ffffff;
            color: {TEXT_COLOR};
        }}
        QCalendarWidget QSpinBox {{
            background-color: #ffffff;
            color: {TEXT_COLOR};
        }}
        QCalendarWidget QAbstractItemView:enabled {{
            background-color: #ffffff;
            color: {TEXT_COLOR};
            selection-background-color: {ACCENT_COLOR};
            selection-color: white;
        }}
        QCalendarWidget QAbstractItemView:disabled {{
            color: #a0a0a0;
        }}
    """