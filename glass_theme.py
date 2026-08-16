from functools import lru_cache

from PySide6.QtGui import QColor, QBrush

# 모던하고 세련된 테마 (밝은 계열의 블러 글래스 느낌)
GLASS_COLOR_RGB = (250, 250, 252)
DIALOG_ALPHA = 245
MENU_ALPHA = 240

ACCENT_COLOR = "#006cd9" # 세련된 블루 포인트
TEXT_COLOR = "#202124"
BORDER_COLOR = "#e0e4e8"

def get_glass_background_brush():
    return QBrush(QColor(*GLASS_COLOR_RGB, DIALOG_ALPHA))

@lru_cache(maxsize=1)
def get_glass_menu_style():
    return f"""
        QMenu {{
            background-color: rgba({GLASS_COLOR_RGB[0]}, {GLASS_COLOR_RGB[1]}, {GLASS_COLOR_RGB[2]}, {MENU_ALPHA});
            border: 1px solid rgba(0, 0, 0, 0.08);
            border-radius: 10px;
            padding: 6px;
        }}
        QMenu::item {{
            color: {TEXT_COLOR};
            background-color: transparent;
            padding: 8px 16px;
            margin: 2px 4px;
            border-radius: 6px;
            font-family: 'Segoe UI', sans-serif;
            font-size: 10pt;
        }}
        QMenu::item:selected {{
            background-color: {ACCENT_COLOR};
            color: #ffffff;
        }}
        QMenu::separator {{
            height: 1px;
            background: rgba(0, 0, 0, 0.06);
            margin: 4px 8px;
        }}
    """

@lru_cache(maxsize=1)
def get_glass_dialog_style():
    return f"""
        QDialog, QWidget {{
            background: transparent;
            font-family: 'Segoe UI', 'Malgun Gothic', sans-serif;
            color: {TEXT_COLOR};
        }}

        QLabel, QCheckBox {{
            color: {TEXT_COLOR};
            font-size: 10pt;
        }}

        QLabel#settingsHeader {{
            color: #15191e;
            font-size: 19pt;
            font-weight: 800;
        }}
        QLabel#settingsSubtitle, QLabel#sectionDescription,
        QLabel#fileCardDescription {{
            color: #727982;
            font-size: 9pt;
        }}
        QLabel#sectionTitle {{
            color: #20252b;
            font-size: 14pt;
            font-weight: 750;
        }}
        QLabel#cardTitle, QLabel#fileCardTitle,
        QLabel#styleItemTitle {{
            color: #30363d;
            font-size: 10pt;
            font-weight: 700;
        }}
        QLabel#styleGroupTitle {{
            color: {ACCENT_COLOR};
            font-size: 10pt;
            font-weight: 750;
            padding: 1px 2px 3px 2px;
        }}
        QLabel#controlLabel {{
            color: #6b727c;
            font-size: 9pt;
            font-weight: 600;
        }}
        QLabel#countBadge {{
            color: {ACCENT_COLOR};
            background-color: #eaf4ff;
            border: 1px solid #c9e3ff;
            border-radius: 10px;
            padding: 4px 10px;
            font-weight: 700;
        }}

        QFrame#settingsCard, QFrame#styleGroupCard,
        QFrame#themePresetCard, QFrame#fileCard,
        QFrame#ddayEditorRow {{
            background-color: rgba(255, 255, 255, 0.92);
            border: 1px solid #e3e7eb;
            border-radius: 10px;
        }}
        QFrame#ddayEditorRow:hover {{
            border-color: #b9d7f5;
            background-color: #fbfdff;
        }}
        QFrame#styleCompactRow {{
            background-color: #f7f9fb;
            border: 1px solid #edf0f3;
            border-radius: 8px;
        }}
        QFrame#styleCompactRow:hover {{
            border-color: #c8def3;
            background-color: #fbfdff;
        }}
        QFrame#themePresetCard {{
            background-color: rgba(244, 249, 255, 0.72);
            border: 1px solid rgba(164, 190, 216, 0.55);
            border-radius: 12px;
        }}
        QCheckBox::indicator {{
            width: 18px;
            height: 18px;
            border-radius: 4px;
            border: 1px solid #c0c4c8;
            background: white;
        }}
        QCheckBox::indicator:checked {{
            background-color: {ACCENT_COLOR};
            border: 1px solid {ACCENT_COLOR};
            image: url(none);
        }}

        /* 버튼 스타일 세련되게 */
        QPushButton {{
            background-color: #ffffff;
            border: 1px solid {BORDER_COLOR};
            border-radius: 6px;
            padding: 5px 8px;
            color: {TEXT_COLOR};
            font-size: 10pt;
        }}
        QPushButton:hover {{
            background-color: #f4f6f8;
            border-color: #c0c4c8;
        }}
        QPushButton:pressed {{
            background-color: #eef1f4;
        }}
        QPushButton#primaryButton {{
            background-color: {ACCENT_COLOR};
            color: white;
            border: none;
            border-radius: 8px;
            padding: 10px 18px;
            font-weight: 700;
        }}
        QPushButton#primaryButton:hover {{ background-color: #005bb8; }}
        QPushButton#resetButton, QPushButton#compactButton {{
            background-color: #ffffff;
            color: #4f5660;
            border: 1px solid #dce1e6;
            border-radius: 7px;
            padding: 7px 11px;
            font-weight: 600;
        }}
        QPushButton#compactButton:hover {{
            color: {ACCENT_COLOR};
            border-color: #a9cff5;
            background-color: #f5faff;
        }}
        QPushButton#outlineButton {{
            color: {ACCENT_COLOR};
            background-color: #f5faff;
            border: 1px dashed #7eb7ee;
            border-radius: 8px;
            padding: 9px;
            font-weight: 700;
        }}
        QPushButton#outlineButton:hover {{
            background-color: #eaf4ff;
            border-style: solid;
        }}
        QPushButton#iconButton, QPushButton#dangerIconButton {{
            background-color: transparent;
            border: 1px solid #dfe4e8;
            border-radius: 6px;
            padding: 0px;
        }}
        QPushButton#iconButton:hover {{
            color: {ACCENT_COLOR};
            background-color: #eef6ff;
            border-color: #b8d7f5;
        }}
        QPushButton#dangerIconButton:hover {{
            color: #c62828;
            background-color: #fff1f1;
            border-color: #efb2b2;
        }}

        /* 입력창 & 콤보박스 (스핀박스 제외) */
        QLineEdit, QComboBox, QDateEdit {{
            background-color: #ffffff;
            border: 1px solid {BORDER_COLOR};
            border-radius: 6px;
            padding: 4px 8px;
            color: {TEXT_COLOR};
            selection-background-color: {ACCENT_COLOR};
            selection-color: white;
            min-height: 24px;
        }}

        /* 스핀박스 (텍스트 영역이 화살표를 침범하지 않도록 우측 패딩 추가 및 명시적 컨트롤 설정) */
        QSpinBox {{
            background-color: #ffffff;
            border: 1px solid {BORDER_COLOR};
            border-radius: 6px;
            padding: 4px 24px 4px 8px;
            color: {TEXT_COLOR};
            selection-background-color: {ACCENT_COLOR};
            selection-color: white;
            min-height: 24px;
            max-width: 70px;
        }}

        QSpinBox::up-button {{
            subcontrol-origin: border;
            subcontrol-position: top right;
            width: 22px;
            border-left: 1px solid {BORDER_COLOR};
            border-bottom: 1px solid {BORDER_COLOR};
            border-top-right-radius: 6px;
            background-color: transparent;
        }}
        QSpinBox::up-button:hover {{ background-color: #f4f6f8; }}

        QSpinBox::down-button {{
            subcontrol-origin: border;
            subcontrol-position: bottom right;
            width: 22px;
            border-left: 1px solid {BORDER_COLOR};
            border-bottom-right-radius: 6px;
            background-color: transparent;
        }}
        QSpinBox::down-button:hover {{ background-color: #f4f6f8; }}

        QSpinBox::up-arrow, QSpinBox::down-arrow {{
            image: none;
        }}

        /* 날짜 입력창이 잘리지 않도록 최소 너비 추가 */
        QDateEdit {{
            min-width: 110px;
        }}

        QComboBox::drop-down, QDateEdit::drop-down {{
            subcontrol-origin: padding;
            subcontrol-position: top right;
            width: 24px;
            border: none;
            background-color: transparent;
        }}

        /* 탭 모던 스타일 */
        QTabWidget::pane {{
            border: 1px solid {BORDER_COLOR};
            background: #ffffff;
            border-radius: 12px;
            border-top-left-radius: 0px;
            margin-top: -1px;
        }}
        QTabBar::tab {{
            background: #f2f4f6;
            border: 1px solid transparent;
            border-bottom: 1px solid {BORDER_COLOR};
            padding: 10px 24px;
            margin-right: 4px;
            border-top-left-radius: 8px;
            border-top-right-radius: 8px;
            color: #606770;
            font-weight: 600;
        }}
        QTabBar::tab:selected {{
            background: #ffffff;
            border: 1px solid {BORDER_COLOR};
            border-bottom: 1px solid #ffffff;
            color: {ACCENT_COLOR};
            font-weight: 750;
        }}
        QTabBar::tab:hover:!selected {{
            background: #f0f2f5;
            color: #30353a;
        }}

        QScrollBar:vertical {{
            border: none;
            background: #edf1f4;
            width: 12px;
            margin: 2px;
            border-radius: 6px;
        }}
        QScrollBar::handle:vertical {{
            background: #8793a0;
            min-height: 44px;
            border-radius: 4px;
            margin: 2px;
        }}
        QScrollBar::handle:vertical:hover {{ background: #5f6d7b; }}
        QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{ height: 0px; }}
        QScrollBar::add-page:vertical, QScrollBar::sub-page:vertical {{ background: transparent; }}

        QFrame[frameShape="4"] {{ color: #eaedf0; }}
    """

@lru_cache(maxsize=1)
def get_calendar_style():
    return f"""
        QCalendarWidget QWidget#qt_calendar_navigationbar {{
            background-color: #f8f9fa;
            border-bottom: 1px solid {BORDER_COLOR};
        }}
        QCalendarWidget QTableView {{
            background-color: #ffffff;
            selection-background-color: {ACCENT_COLOR};
            selection-color: white;
            alternate-background-color: #fafbfc;
            gridline-color: #f0f2f5;
        }}
        QCalendarWidget QToolButton {{
            color: {TEXT_COLOR};
            background-color: transparent;
            border: none;
            border-radius: 6px;
            padding: 4px;
        }}
        QCalendarWidget QToolButton:hover {{
            background-color: #eaedf0;
        }}
        QCalendarWidget QMenu, QCalendarWidget QSpinBox {{
            background-color: #ffffff;
            color: {TEXT_COLOR};
        }}
        QCalendarWidget QAbstractItemView:enabled {{
            background-color: #ffffff;
            color: {TEXT_COLOR};
            selection-background-color: {ACCENT_COLOR};
            selection-color: white;
        }}
    """
