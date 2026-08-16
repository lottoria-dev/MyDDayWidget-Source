import copy
import math
from PySide6.QtWidgets import (QDialog, QVBoxLayout, QHBoxLayout, QLabel,
                             QDial, QCheckBox, QPushButton, QFrame,
                             QScrollArea, QWidget, QLineEdit, QDateEdit,
                             QColorDialog, QFontComboBox, QSizePolicy, QGridLayout,
                             QComboBox, QTabWidget, QSpinBox, QFileDialog,
                             QMessageBox)
from PySide6.QtCore import Qt, QDate, QRectF, QSignalBlocker
from PySide6.QtGui import (QFont, QColor, QPainter, QPainterPath,
                           QLinearGradient, QPen, QBrush)

import glass_theme
from config_manager import DEFAULT_DATA


STYLE_PREVIEW_TEXTS = {
    "time": "12:34:56",
    "date": "2026-08-15 (토)",
    "dday_title": "수학 평가",
    "dday_count": "D-30",
    "dday_date": "2026-09-14 (월)",
    "calendar": "일  월  화  수  목  금  토",
}
TIME_FORMAT_VALUES = ("24h", "12h")
DATE_FORMAT_VALUES = ("yyyy-mm-dd", "mm/dd/yyyy", "dd/mm/yyyy")
DAY_FORMAT_VALUES = ("kor", "eng")

STYLE_THEMES = {
    "default": {
        "label": "기본",
        "font": "Segoe UI",
        "swatch": "#20252b",
        "colors": {
            "time": "#ffffff", "date": "#ffffff",
            "dday_title": "#ffffff", "dday_count": "#ff6b6b",
            "dday_date": "#aaaaaa", "calendar": "#ffffff",
        },
    },
    "contrast": {
        "label": "고대비",
        "font": "Malgun Gothic",
        "swatch": "#111111",
        "colors": {
            "time": "#ffffff", "date": "#f2f2f2",
            "dday_title": "#ffffff", "dday_count": "#ffd54f",
            "dday_date": "#e0e0e0", "calendar": "#ffffff",
        },
    },
    "ocean": {
        "label": "바다",
        "font": "Segoe UI",
        "swatch": "#14364a",
        "colors": {
            "time": "#e8f7ff", "date": "#9bd8ff",
            "dday_title": "#dff8ff", "dday_count": "#5ee7f7",
            "dday_date": "#91bcd5", "calendar": "#c9efff",
        },
    },
    "sunset": {
        "label": "노을",
        "font": "Malgun Gothic",
        "swatch": "#4a2a24",
        "colors": {
            "time": "#fff4dc", "date": "#ffcf99",
            "dday_title": "#ffe2ba", "dday_count": "#ff8a65",
            "dday_date": "#d9b28c", "calendar": "#ffe0b2",
        },
    },
    "mono": {
        "label": "모노",
        "font": "Segoe UI",
        "swatch": "#303030",
        "colors": {
            "time": "#ffffff", "date": "#c8c8c8",
            "dday_title": "#ededed", "dday_count": "#ffffff",
            "dday_date": "#999999", "calendar": "#d7d7d7",
        },
    },
}

COMBOBOX_STYLE = """
    QComboBox { padding-right: 20px; }
    QComboBox::down-arrow { image: none; }
    QComboBox::drop-down { border: none; background: transparent; width: 24px; }
    QComboBox QAbstractItemView {
        background-color: #ffffff;
        color: #202020;
        selection-background-color: #006cd9;
        selection-color: white;
        border: 1px solid #e0e4e8;
        border-radius: 6px;
        outline: 0px;
    }
"""

DATE_EDIT_STYLE = """
    QDateEdit { padding-right: 20px; }
    QDateEdit::down-arrow { image: none; }
    QDateEdit::drop-down { border: none; background: transparent; width: 24px; }
"""


def _paint_dropdown_arrow(widget):
    painter = QPainter(widget)
    painter.setRenderHint(QPainter.Antialiasing)
    painter.setPen(QColor("#808080"))
    font = widget.font()
    font.setPointSize(8)
    painter.setFont(font)
    painter.drawText(
        widget.rect().adjusted(0, 0, -8, 0),
        Qt.AlignRight | Qt.AlignVCenter,
        "▼",
    )


def _select_combo_value(combo, values, value):
    combo.setCurrentIndex(values.index(value) if value in values else 0)


class GlassThemeButton(QPushButton):
    """글래스 카드와 팔레트 점으로 테마를 보여 주는 버튼입니다."""

    def __init__(self, theme, parent=None):
        super().__init__(theme["label"], parent)
        self.theme = theme
        self.setCursor(Qt.PointingHandCursor)
        self.setMinimumSize(88, 40)
        self.setToolTip("{} 글꼴과 색상 적용".format(theme["label"]))
        self.setStyleSheet("background: transparent; border: none; padding: 0px;")

    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        rect = self.rect().adjusted(1, 1, -1, -1)

        tint = QColor(self.theme["swatch"])
        tint.setAlpha(70 if self.isDown() else (48 if self.underMouse() else 32))
        gradient = QLinearGradient(rect.left(), rect.top(), rect.right(), rect.bottom())
        gradient.setColorAt(0.0, QColor(255, 255, 255, 242))
        gradient.setColorAt(0.55, QColor(248, 251, 255, 222))
        gradient.setColorAt(1.0, tint)

        border = QColor("#78b7f0") if self.underMouse() else QColor(205, 216, 227, 210)
        painter.setPen(QPen(border, 1.2))
        painter.setBrush(QBrush(gradient))
        painter.drawRoundedRect(rect, 10, 10)

        dot_colors = (
            self.theme["colors"]["time"],
            self.theme["colors"]["dday_count"],
            self.theme["colors"]["dday_date"],
        )
        for index, color in enumerate(dot_colors):
            painter.setPen(QPen(QColor(90, 105, 120, 90), 0.7))
            painter.setBrush(QColor(color))
            painter.drawEllipse(10 + index * 8, 16, 7, 7)

        label_font = QFont("Segoe UI", 9, QFont.DemiBold)
        painter.setFont(label_font)
        painter.setPen(QColor("#27313b"))
        painter.drawText(rect.adjusted(39, 0, -7, 0), Qt.AlignVCenter | Qt.AlignLeft, self.text())


class OpacityJogDial(QDial):
    """좁은 설정 창에서도 겹치지 않는 원형 불투명도 조절기입니다."""

    START_ANGLE = 135.0
    SWEEP_ANGLE = 270.0

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setRange(20, 100)
        self.setSingleStep(1)
        self.setPageStep(5)
        self.setWrapping(False)
        self.setNotchesVisible(False)
        self.setFixedSize(68, 68)
        self.setCursor(Qt.PointingHandCursor)
        self.setFocusPolicy(Qt.StrongFocus)
        self.setAccessibleName("창 불투명도 조절")
        self.setToolTip("다이얼을 돌리거나 마우스 휠·방향키로 조절합니다.")
        self._track_path = self._arc_path(
            self.width() / 2.0,
            self.height() / 2.0,
            min(self.width(), self.height()) / 2.0 - 7.0,
            self.START_ANGLE,
            self.START_ANGLE + self.SWEEP_ANGLE,
        )

    @staticmethod
    def _arc_path(center_x, center_y, radius, start_angle, end_angle):
        path = QPainterPath()
        steps = max(2, int(abs(end_angle - start_angle) / 3.0))
        for index in range(steps + 1):
            ratio = index / steps
            angle = math.radians(start_angle + (end_angle - start_angle) * ratio)
            x = center_x + math.cos(angle) * radius
            y = center_y + math.sin(angle) * radius
            if index == 0:
                path.moveTo(x, y)
            else:
                path.lineTo(x, y)
        return path

    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)

        center_x = self.width() / 2.0
        center_y = self.height() / 2.0
        radius = min(self.width(), self.height()) / 2.0 - 7.0
        ratio = (self.value() - self.minimum()) / (self.maximum() - self.minimum())
        value_angle = self.START_ANGLE + self.SWEEP_ANGLE * ratio

        track_pen = QPen(QColor("#d4e0ea"), 7)
        track_pen.setCapStyle(Qt.RoundCap)
        painter.setPen(track_pen)
        painter.drawPath(self._track_path)

        active_gradient = QLinearGradient(7, self.height(), self.width() - 7, 0)
        active_gradient.setColorAt(0.0, QColor("#57adff"))
        active_gradient.setColorAt(1.0, QColor("#0878e4"))
        active_pen = QPen(QBrush(active_gradient), 7)
        active_pen.setCapStyle(Qt.RoundCap)
        painter.setPen(active_pen)
        painter.drawPath(self._arc_path(
            center_x, center_y, radius, self.START_ANGLE, value_angle,
        ))

        angle = math.radians(value_angle)
        handle_x = center_x + math.cos(angle) * radius
        handle_y = center_y + math.sin(angle) * radius
        painter.setPen(QPen(QColor("#0878e4"), 2))
        painter.setBrush(QColor("#ffffff"))
        painter.drawEllipse(QRectF(handle_x - 4, handle_y - 4, 8, 8))

        inner_rect = QRectF(center_x - 19, center_y - 19, 38, 38)
        inner_gradient = QLinearGradient(0, inner_rect.top(), 0, inner_rect.bottom())
        inner_gradient.setColorAt(0.0, QColor(255, 255, 255, 248))
        inner_gradient.setColorAt(1.0, QColor(235, 244, 252, 238))
        painter.setPen(QPen(QColor(158, 185, 209, 190), 1))
        painter.setBrush(QBrush(inner_gradient))
        painter.drawEllipse(inner_rect)

        value_font = QFont("Segoe UI", 9, QFont.DemiBold)
        painter.setFont(value_font)
        painter.setPen(QColor("#075fae"))
        painter.drawText(self.rect(), Qt.AlignCenter, "{}%".format(self.value()))

        if self.hasFocus():
            focus_pen = QPen(QColor(8, 120, 228, 110), 1)
            focus_pen.setStyle(Qt.DotLine)
            painter.setPen(focus_pen)
            painter.setBrush(Qt.NoBrush)
            painter.drawEllipse(self.rect().adjusted(1, 1, -1, -1))


class DraggableGlassDialog(QDialog):
    """공통 글래스 배경과 프레임리스 창 드래그 동작을 제공합니다."""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.drag_position = None

    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        path = QPainterPath()
        path.addRoundedRect(0, 0, self.width(), self.height(), 16, 16)
        painter.fillPath(path, glass_theme.get_glass_background_brush())
        painter.setPen(QColor(200, 200, 200, 150))
        painter.setBrush(Qt.NoBrush)
        painter.drawPath(path)

    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self.drag_position = (
                event.globalPosition().toPoint() - self.frameGeometry().topLeft()
            )
            event.accept()

    def mouseMoveEvent(self, event):
        if event.buttons() == Qt.LeftButton and self.drag_position:
            self.move(event.globalPosition().toPoint() - self.drag_position)
            event.accept()


class GlassColorDialog(QColorDialog):
    def __init__(self, color, parent=None):
        super().__init__(color, parent)
        self.setOption(QColorDialog.DontUseNativeDialog, True)
        self.setAttribute(Qt.WA_TranslucentBackground)
        self.setStyleSheet(glass_theme.get_glass_dialog_style())
        self.setFont(QFont("Segoe UI"))

    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        path = QPainterPath()
        path.addRoundedRect(0, 0, self.width(), self.height(), 15, 15)
        painter.fillPath(path, glass_theme.get_glass_background_brush())
        painter.setPen(QColor(200, 200, 200, 100))
        painter.drawPath(path)

class GlassInfoDialog(DraggableGlassDialog):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowFlags(Qt.FramelessWindowHint | Qt.Dialog)
        self.setAttribute(Qt.WA_TranslucentBackground)
        self.setStyleSheet(glass_theme.get_glass_dialog_style())
        self.setFont(QFont("Segoe UI"))
        self.resize(340, 360)

        if parent and hasattr(parent, 'app_icon'):
            self.setWindowIcon(parent.app_icon)

        self.init_ui()

    def init_ui(self):
        layout = QVBoxLayout()
        layout.setContentsMargins(25, 25, 25, 25)
        self.setLayout(layout)

        header_layout = QHBoxLayout()
        lbl_header = QLabel("정보 (About)")
        lbl_header.setStyleSheet("font-size: 15pt; font-weight: bold; color: #101010;")
        header_layout.addWidget(lbl_header)
        header_layout.addStretch()

        btn_close = QPushButton("✕")
        btn_close.setFixedSize(30, 30)
        btn_close.setStyleSheet("QPushButton { background: transparent; border: none; font-size: 14pt; color: #707070; } QPushButton:hover { color: #d32f2f; background: #ffebee; border-radius: 15px; }")
        btn_close.clicked.connect(self.accept)
        header_layout.addWidget(btn_close)

        layout.addLayout(header_layout)
        layout.addSpacing(10)

        info_label = QLabel()
        info_label.setOpenExternalLinks(True)
        info_html = """
        <div style="font-family: 'Segoe UI', 'Malgun Gothic', sans-serif; font-size: 13px; color: #404040;">
            <h3 style="margin-bottom: 10px; color: #1a73e8;">■ My D-Day Widget</h3>
            <p><b>[사용 방법]</b></p>
            <ul style="margin-top: 5px; padding-left: 20px;">
                <li><b>이동 :</b> 마우스 드래그</li>
                <li><b>설정 :</b> 위젯 더블 클릭</li>
                <li><b>크기 :</b> 우측 하단(◢) 드래그</li>
            </ul>
            <hr style="background-color: rgba(0,0,0,0.05); border: 0; height: 1px; margin: 15px 0;">
            <b>■ 버전 정보:</b> v2.6.0<br><br>
            <b>■ 공식 배포 페이지</b><br>
            <a href="https://mathtime.kr/?page=dday" style="color: #1a73e8; text-decoration: none;">https://mathtime.kr/?page=dday</a><br><br>
            <b>■ 개발자 정보</b><br>
            - 최근 업데이트: 2026.08.16<br>
            - ✉: mathtime.ai@gmail.com<br>
            Copyright 2026 lottoria-dev. All rights reserved.<br>
        </div>
        """
        info_label.setText(info_html)
        layout.addWidget(info_label)
        layout.addStretch()

class GlassFontComboBox(QFontComboBox):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setStyleSheet(COMBOBOX_STYLE)

    def paintEvent(self, event):
        super().paintEvent(event)
        _paint_dropdown_arrow(self)

class GlassComboBox(QComboBox):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setStyleSheet(COMBOBOX_STYLE)

    def paintEvent(self, event):
        super().paintEvent(event)
        _paint_dropdown_arrow(self)

class GlassDateEdit(QDateEdit):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setStyleSheet(DATE_EDIT_STYLE)

    def paintEvent(self, event):
        super().paintEvent(event)
        _paint_dropdown_arrow(self)

class GlassSpinBox(QSpinBox):
    def paintEvent(self, event):
        super().paintEvent(event)
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        painter.setPen(QColor("#606060"))
        f = self.font()
        f.setPointSize(7)
        painter.setFont(f)

        # 우측 버튼 영역(약 22px 폭)의 상/하단 중앙에 텍스트로 화살표 직접 그리기
        up_rect = self.rect().adjusted(self.width() - 22, 0, 0, -self.height() // 2)
        painter.drawText(up_rect, Qt.AlignCenter, "▲")

        down_rect = self.rect().adjusted(self.width() - 22, self.height() // 2, 0, 0)
        painter.drawText(down_rect, Qt.AlignCenter, "▼")

class SettingsDialog(DraggableGlassDialog):
    def __init__(self, data, parent=None):
        super().__init__(parent)
        self.data = copy.deepcopy(data)
        self.imported_data = None
        self.original_opacity = parent.windowOpacity() if parent else None

        self.setWindowFlags(Qt.FramelessWindowHint | Qt.Dialog)
        self.setFont(QFont("Segoe UI"))

        if parent and hasattr(parent, 'app_icon'):
            self.setWindowIcon(parent.app_icon)

        # 미리보기와 일정 편집 영역을 충분히 확보합니다.
        self.resize(720, 680)
        self.setMinimumSize(680, 620)

        self.setAttribute(Qt.WA_TranslucentBackground)
        self.setStyleSheet(glass_theme.get_glass_dialog_style())

        self.style_controls = {}
        self.init_ui()

    def init_ui(self):
        layout = QVBoxLayout()
        layout.setContentsMargins(30, 26, 30, 26)
        layout.setSpacing(14)
        self.setLayout(layout)

        header_layout = QHBoxLayout()
        header_text_layout = QVBoxLayout()
        header_text_layout.setSpacing(3)
        lbl_header = QLabel("My D-Day 설정")
        lbl_header.setObjectName("settingsHeader")
        lbl_subtitle = QLabel("일정을 관리하고 위젯의 표시 방식을 원하는 모습으로 조정하세요.")
        lbl_subtitle.setObjectName("settingsSubtitle")
        header_text_layout.addWidget(lbl_header)
        header_text_layout.addWidget(lbl_subtitle)
        header_layout.addLayout(header_text_layout)
        header_layout.addStretch()

        btn_close = QPushButton("✕")
        btn_close.setFixedSize(32, 32)
        btn_close.setStyleSheet("""
            QPushButton { background-color: transparent; color: #888; border: none; font-size: 14pt; }
            QPushButton:hover { color: #d32f2f; background: #ffebee; border-radius: 16px; }
        """)
        btn_close.clicked.connect(self.reject)
        header_layout.addWidget(btn_close)

        layout.addLayout(header_layout)
        layout.addSpacing(4)

        # --- 탭 구성 시작 ---
        self.tabs = QTabWidget()
        # 스타일은 glass_theme.py에서 전역으로 가져오므로 별도 지정 불필요

        tab_items = QWidget()
        tab_general = QWidget()
        tab_style = QWidget()

        self._init_items_tab(tab_items)
        self._init_general_tab(tab_general)
        self._init_style_tab(tab_style)

        self.tabs.addTab(tab_items, "D-Day 관리")
        self.tabs.addTab(tab_general, "표시 설정")
        self.tabs.addTab(tab_style, "글꼴 · 색상")

        layout.addWidget(self.tabs)
        layout.addSpacing(2)
        # --- 탭 구성 끝 ---

        # 설정 파일 관리
        file_card = QFrame()
        file_card.setObjectName("fileCard")
        file_layout = QHBoxLayout(file_card)
        file_layout.setContentsMargins(14, 10, 14, 10)
        file_layout.setSpacing(8)
        file_text_layout = QVBoxLayout()
        file_text_layout.setSpacing(1)
        file_title = QLabel("설정 파일")
        file_title.setObjectName("fileCardTitle")
        file_description = QLabel("백업을 복원하거나 현재 구성을 다른 PC로 옮길 수 있습니다.")
        file_description.setObjectName("fileCardDescription")
        file_text_layout.addWidget(file_title)
        file_text_layout.addWidget(file_description)
        file_layout.addLayout(file_text_layout)
        file_layout.addStretch()
        btn_import = QPushButton("설정 불러오기")
        btn_export = QPushButton("설정 내보내기")
        btn_restore = QPushButton("최근 백업 복원")
        for button in (btn_import, btn_export, btn_restore):
            button.setObjectName("compactButton")
        btn_import.setToolTip("INI 또는 BAK 설정 파일을 검증한 뒤 적용합니다.")
        btn_export.setToolTip("현재 화면의 설정을 별도 INI 파일로 저장합니다.")
        btn_restore.setToolTip("가장 최근의 정상 백업을 불러옵니다.")
        btn_import.clicked.connect(self.import_settings_file)
        btn_export.clicked.connect(self.export_settings_file)
        btn_restore.clicked.connect(self.restore_latest_backup)
        file_layout.addWidget(btn_import)
        file_layout.addWidget(btn_export)
        file_layout.addWidget(btn_restore)
        layout.addWidget(file_card)

        # 하단 버튼 영역
        bottom_layout = QHBoxLayout()

        btn_reset = QPushButton("표시 설정 초기화")
        btn_reset.setObjectName("resetButton")
        btn_reset.setMinimumWidth(100)
        btn_reset.clicked.connect(self.reset_to_defaults)

        btn_save = QPushButton("저장 및 적용")
        btn_save.setObjectName("primaryButton")
        btn_save.setMinimumWidth(120)
        btn_save.clicked.connect(self.accept)

        btn_cancel = QPushButton("취소")
        btn_cancel.setObjectName("compactButton")
        btn_cancel.setMinimumWidth(80)
        btn_cancel.clicked.connect(self.reject)

        bottom_layout.addWidget(btn_reset)
        bottom_layout.addStretch()
        bottom_layout.addWidget(btn_cancel)
        bottom_layout.addWidget(btn_save)
        layout.addLayout(bottom_layout)

    def _init_general_tab(self, tab):
        layout = QVBoxLayout(tab)
        layout.setContentsMargins(18, 18, 18, 18)
        layout.setSpacing(12)

        title = QLabel("화면 표시 방식")
        title.setObjectName("sectionTitle")
        description = QLabel("시간과 날짜 표기, 위젯 동작을 설정합니다.")
        description.setObjectName("sectionDescription")
        layout.addWidget(title)
        layout.addWidget(description)

        format_card = QFrame()
        format_card.setObjectName("settingsCard")
        format_card.setMinimumHeight(174)
        format_layout = QVBoxLayout(format_card)
        format_layout.setContentsMargins(18, 14, 18, 14)
        format_layout.setSpacing(10)
        card_title = QLabel("시간 · 날짜")
        card_title.setObjectName("cardTitle")
        format_layout.addWidget(card_title)

        format_body = QHBoxLayout()
        format_body.setContentsMargins(0, 0, 0, 0)
        format_body.setSpacing(14)

        grid = QGridLayout()
        grid.setHorizontalSpacing(12)
        grid.setVerticalSpacing(8)
        grid.setColumnMinimumWidth(0, 72)
        grid.setColumnStretch(1, 1)

        lbl_time = QLabel("시간 표기:")
        lbl_time.setStyleSheet("font-weight: 600; color: #404040;")
        grid.addWidget(lbl_time, 0, 0)

        self.combo_time_format = GlassComboBox()
        self.combo_time_format.addItems(["24시간제", "12시간제 (AM/PM)"])
        _select_combo_value(
            self.combo_time_format,
            TIME_FORMAT_VALUES,
            self.data.get('time_format', '24h'),
        )
        grid.addWidget(self.combo_time_format, 0, 1)

        lbl_date = QLabel("날짜 표기:")
        lbl_date.setStyleSheet("font-weight: 600; color: #404040;")
        grid.addWidget(lbl_date, 1, 0)

        self.combo_date_format = GlassComboBox()
        self.combo_date_format.addItems(["YYYY-MM-DD", "MM/DD/YYYY", "DD/MM/YYYY"])
        _select_combo_value(
            self.combo_date_format,
            DATE_FORMAT_VALUES,
            self.data.get('date_format', 'yyyy-mm-dd'),
        )
        grid.addWidget(self.combo_date_format, 1, 1)

        lbl_day = QLabel("요일 표기:")
        lbl_day.setStyleSheet("font-weight: 600; color: #404040;")
        grid.addWidget(lbl_day, 2, 0)

        self.combo_day_format = GlassComboBox()
        self.combo_day_format.addItems(["한국어 (월, 화...)", "영어 (Mon, Tue...)"])
        _select_combo_value(
            self.combo_day_format,
            DAY_FORMAT_VALUES,
            self.data.get('day_format', 'kor'),
        )
        grid.addWidget(self.combo_day_format, 2, 1)

        format_body.addLayout(grid, 1)

        opacity_card = QFrame()
        opacity_card.setObjectName("opacityDialCard")
        opacity_card.setFixedWidth(178)
        opacity_card.setStyleSheet("""
            QFrame#opacityDialCard {
                background-color: rgba(241, 247, 252, 178);
                border: 1px solid rgba(160, 185, 208, 150);
                border-radius: 10px;
            }
            QLabel#opacityDialTitle {
                color: #30465a;
                font-size: 9pt;
                font-weight: 600;
                border: none;
                background: transparent;
            }
            QLabel#opacityValue {
                color: #0878e4;
                font-size: 9pt;
                font-weight: 700;
                border: none;
                background: transparent;
            }
            QLabel#opacityEndpoint {
                color: #6f8294;
                font-size: 8pt;
                border: none;
                background: transparent;
            }
            QPushButton#opacityStepButton {
                min-width: 24px;
                max-width: 24px;
                min-height: 24px;
                max-height: 24px;
                padding: 0px;
                color: #176ebc;
                font-size: 13pt;
                font-weight: 600;
                background-color: rgba(255, 255, 255, 205);
                border: 1px solid rgba(125, 169, 207, 175);
                border-radius: 12px;
            }
            QPushButton#opacityStepButton:hover {
                color: #ffffff;
                background-color: #0878e4;
                border-color: #0878e4;
            }
        """)
        opacity_layout = QVBoxLayout(opacity_card)
        opacity_layout.setContentsMargins(9, 6, 9, 6)
        opacity_layout.setSpacing(0)

        opacity_header = QHBoxLayout()
        opacity_header.setSpacing(4)
        opacity_title = QLabel("창 불투명도")
        opacity_title.setObjectName("opacityDialTitle")
        self.lbl_alpha_value = QLabel()
        self.lbl_alpha_value.setObjectName("opacityValue")
        self.lbl_alpha_value.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
        opacity_header.addWidget(opacity_title)
        opacity_header.addStretch()
        opacity_header.addWidget(self.lbl_alpha_value)
        opacity_layout.addLayout(opacity_header)

        dial_row = QHBoxLayout()
        dial_row.setSpacing(5)
        btn_alpha_down = QPushButton("−")
        btn_alpha_down.setObjectName("opacityStepButton")
        btn_alpha_down.setToolTip("불투명도 5% 낮추기")
        self.dial_alpha = OpacityJogDial()
        self.dial_alpha.setValue(int(self.data['alpha'] * 100))
        btn_alpha_up = QPushButton("+")
        btn_alpha_up.setObjectName("opacityStepButton")
        btn_alpha_up.setToolTip("불투명도 5% 높이기")
        btn_alpha_down.clicked.connect(
            lambda: self.dial_alpha.setValue(self.dial_alpha.value() - 5)
        )
        btn_alpha_up.clicked.connect(
            lambda: self.dial_alpha.setValue(self.dial_alpha.value() + 5)
        )
        self.dial_alpha.valueChanged.connect(self.update_opacity_preview)
        dial_row.addWidget(btn_alpha_down)
        dial_row.addWidget(self.dial_alpha)
        dial_row.addWidget(btn_alpha_up)
        opacity_layout.addLayout(dial_row)

        endpoint_row = QHBoxLayout()
        lbl_transparent = QLabel("투명 20%")
        lbl_transparent.setObjectName("opacityEndpoint")
        lbl_opaque = QLabel("불투명 100%")
        lbl_opaque.setObjectName("opacityEndpoint")
        endpoint_row.addWidget(lbl_transparent)
        endpoint_row.addStretch()
        endpoint_row.addWidget(lbl_opaque)
        opacity_layout.addLayout(endpoint_row)

        format_body.addWidget(opacity_card)
        format_layout.addLayout(format_body)
        self.update_opacity_preview(self.dial_alpha.value())

        layout.addWidget(format_card)

        option_card = QFrame()
        option_card.setObjectName("settingsCard")
        option_layout = QVBoxLayout(option_card)
        option_layout.setContentsMargins(18, 16, 18, 16)
        option_layout.setSpacing(12)
        option_title = QLabel("위젯 동작")
        option_title.setObjectName("cardTitle")
        option_layout.addWidget(option_title)

        opt_layout = QHBoxLayout()
        self.chk_top = QCheckBox("항상 위에 고정")
        self.chk_top.setChecked(self.data['topmost'])
        opt_layout.addWidget(self.chk_top)

        self.chk_glass_bg = QCheckBox("유리 배경 사용")
        self.chk_glass_bg.setChecked(self.data.get('use_glass_background', False))
        opt_layout.addWidget(self.chk_glass_bg)

        self.chk_calendar = QCheckBox("투명 달력 표시")
        self.chk_calendar.setChecked(self.data.get('show_calendar', False))
        opt_layout.addWidget(self.chk_calendar)

        option_layout.addLayout(opt_layout)

        startup_layout = QHBoxLayout()
        startup_layout.setSpacing(8)
        self.chk_auto_start = QCheckBox("Windows 시작 시 자동 실행")
        self.chk_auto_start.setChecked(self.data.get('auto_start', False))
        startup_supported = bool(
            self.parent()
            and hasattr(self.parent(), 'startup_manager')
            and self.parent().startup_manager.is_supported
        )
        self.chk_auto_start.setEnabled(startup_supported)
        startup_layout.addWidget(self.chk_auto_start)
        startup_description = QLabel(
            "EXE를 옮긴 뒤 한 번 직접 실행하면 새 위치로 자동 복구됩니다."
            if startup_supported
            else "Windows용 배포 EXE에서만 설정할 수 있습니다."
        )
        startup_description.setObjectName("sectionDescription")
        startup_layout.addWidget(startup_description)
        startup_layout.addStretch()
        option_layout.addLayout(startup_layout)

        layout.addWidget(option_card)
        layout.addStretch()

    def _init_style_tab(self, tab):
        layout = QVBoxLayout(tab)
        layout.setContentsMargins(18, 18, 18, 18)
        layout.setSpacing(10)

        title = QLabel("글꼴과 색상")
        title.setObjectName("sectionTitle")
        description = QLabel("항목별 표시 스타일을 기능 단위로 정리했습니다. 변경 결과는 오른쪽에서 바로 확인할 수 있습니다.")
        description.setObjectName("sectionDescription")
        layout.addWidget(title)
        layout.addWidget(description)

        theme_card = QFrame()
        theme_card.setObjectName("themePresetCard")
        theme_layout = QVBoxLayout(theme_card)
        theme_layout.setContentsMargins(12, 9, 12, 9)
        theme_layout.setSpacing(5)
        theme_header = QHBoxLayout()
        theme_title = QLabel("빠른 테마")
        theme_title.setObjectName("styleGroupTitle")
        theme_hint = QLabel("팔레트를 선택하면 글꼴과 색상만 바뀌며 크기는 유지됩니다.")
        theme_hint.setObjectName("sectionDescription")
        theme_header.addWidget(theme_title)
        theme_header.addSpacing(6)
        theme_header.addWidget(theme_hint)
        theme_header.addStretch()
        theme_layout.addLayout(theme_header)

        theme_row = QHBoxLayout()
        theme_row.setSpacing(7)
        for theme_key, theme in STYLE_THEMES.items():
            button = GlassThemeButton(theme)
            button.clicked.connect(
                lambda _checked=False, key=theme_key: self.apply_style_theme(key)
            )
            theme_row.addWidget(button, 1)
        theme_layout.addLayout(theme_row)
        layout.addWidget(theme_card)

        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.NoFrame)
        scroll.setVerticalScrollBarPolicy(Qt.ScrollBarAlwaysOn)
        scroll.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        scroll.setStyleSheet("background: transparent;")

        content = QWidget()
        content.setStyleSheet("background: transparent;")
        v_layout = QVBoxLayout(content)
        v_layout.setSpacing(12)
        v_layout.setContentsMargins(0, 6, 8, 4)

        self.add_style_group(
            "시계",
            (("현재 시간", "time"), ("현재 날짜", "date")),
            v_layout,
        )
        self.add_style_group(
            "D-Day",
            (
                ("제목", "dday_title"),
                ("남은 일수", "dday_count"),
                ("목표 날짜", "dday_date"),
            ),
            v_layout,
        )
        self.add_style_group("달력", (("요일 머리글", "calendar"),), v_layout)

        v_layout.addStretch()
        scroll.setWidget(content)
        layout.addWidget(scroll)

    def add_style_group(self, title, rows, layout):
        group = QFrame()
        group.setObjectName("styleGroupCard")
        group_layout = QVBoxLayout(group)
        group_layout.setContentsMargins(12, 10, 12, 12)
        group_layout.setSpacing(7)

        group_title = QLabel(title)
        group_title.setObjectName("styleGroupTitle")
        group_layout.addWidget(group_title)

        for name, key_prefix in rows:
            self.add_style_row(name, key_prefix, group_layout)

        layout.addWidget(group)

    def add_style_row(self, name, key_prefix, layout):
        row = QFrame()
        row.setObjectName("styleCompactRow")
        row_layout = QHBoxLayout(row)
        row_layout.setContentsMargins(10, 7, 10, 7)
        row_layout.setSpacing(7)

        lbl = QLabel(name)
        lbl.setObjectName("styleItemTitle")
        lbl.setFixedWidth(74)

        cmb_font = GlassFontComboBox()
        cmb_font.setCurrentFont(QFont(self.data.get(f'font_{key_prefix}', 'Segoe UI')))
        cmb_font.setMinimumWidth(145)
        cmb_font.setMaximumWidth(180)

        lbl_size = QLabel("크기:")
        lbl_size.setObjectName("controlLabel")
        spin_size = GlassSpinBox()
        spin_size.setRange(5, 150)
        spin_size.setValue(self.data.get(f'size_{key_prefix}', 12))

        lbl_color = QLabel("색상:")
        lbl_color.setObjectName("controlLabel")
        btn_color = QPushButton()
        btn_color.setFixedSize(26, 26)
        current_color = self.data.get(f'color_{key_prefix}', '#ffffff')
        btn_color.setToolTip("색상 선택")
        self._set_color_button_style(btn_color, current_color)
        btn_color.clicked.connect(lambda _, k=f'color_{key_prefix}', b=btn_color: self._pick_color(k, b))

        preview = QLabel(STYLE_PREVIEW_TEXTS[key_prefix])
        preview.setObjectName("fontPreview")
        preview.setAlignment(Qt.AlignCenter)
        preview.setMinimumSize(145, 42)
        preview.setSizePolicy(QSizePolicy.Expanding, QSizePolicy.Minimum)
        preview.setTextFormat(Qt.PlainText)

        row_layout.addWidget(lbl)
        row_layout.addWidget(cmb_font)
        row_layout.addWidget(lbl_size)
        row_layout.addWidget(spin_size)
        row_layout.addWidget(lbl_color)
        row_layout.addWidget(btn_color)
        row_layout.addWidget(preview, 1)

        layout.addWidget(row)
        self.style_controls[key_prefix] = {
            'font': cmb_font,
            'size': spin_size,
            'btn': btn_color,
            'preview': preview,
        }
        cmb_font.currentFontChanged.connect(
            lambda _font, key=key_prefix: self.update_style_preview(key)
        )
        spin_size.valueChanged.connect(
            lambda _value, key=key_prefix: self.update_style_preview(key)
        )
        self.update_style_preview(key_prefix)

    def update_style_preview(self, key_prefix):
        controls = self.style_controls.get(key_prefix)
        if not controls:
            return
        selected_size = controls['size'].value()
        preview_font = controls['font'].currentFont()
        # 24pt를 넘는 값은 150pt까지 54pt 미리보기 범위에 연속 대응합니다.
        preview_size = (
            float(selected_size)
            if selected_size <= 24
            else 24.0 + (selected_size - 24) * (30.0 / 126.0)
        )
        is_bold = key_prefix in ('time', 'date', 'dday_title', 'dday_count')
        font_family = preview_font.family().replace('"', '')
        color = self.data.get(f'color_{key_prefix}', '#ffffff')
        controls['preview'].setStyleSheet(
            'font-family: "{}"; font-size: {:.1f}pt; font-weight: {}; color: {}; '
            'background-color: #20252b; border: 1px solid #343b44; '
            'border-radius: 7px; padding: 4px 7px;'.format(
                font_family,
                preview_size,
                700 if is_bold else 400,
                color,
            )
        )
        controls['preview'].setMinimumHeight(max(42, int(preview_size * 1.55)))
        controls['preview'].setToolTip(
            "{} · 실제 {}pt · 미리보기 {:.1f}pt".format(
                preview_font.family(), selected_size, preview_size
            )
        )

    def update_opacity_preview(self, value):
        self.lbl_alpha_value.setText("{}%".format(value))
        self.dial_alpha.setAccessibleDescription("현재 창 불투명도 {}%".format(value))
        if self.parent():
            self.parent().setWindowOpacity(value / 100)

    @staticmethod
    def _set_color_button_style(button, color):
        button.setStyleSheet(
            "background-color: {}; border: 1px solid #d0d0d0; "
            "border-radius: 13px;".format(color)
        )

    def apply_style_theme(self, theme_key):
        theme = STYLE_THEMES.get(theme_key)
        if not theme:
            return
        for key_prefix, controls in self.style_controls.items():
            with QSignalBlocker(controls['font']):
                controls['font'].setCurrentFont(QFont(theme["font"]))
            color = theme["colors"][key_prefix]
            self.data[f'color_{key_prefix}'] = color
            self._set_color_button_style(controls['btn'], color)
            self.update_style_preview(key_prefix)

    def _init_items_tab(self, tab):
        layout = QVBoxLayout(tab)
        layout.setContentsMargins(18, 18, 18, 18)
        layout.setSpacing(12)

        intro_layout = QHBoxLayout()
        intro_text_layout = QVBoxLayout()
        intro_text_layout.setSpacing(2)
        title = QLabel("중요한 날짜")
        title.setObjectName("sectionTitle")
        description = QLabel("가장 자주 사용하는 일정 관리 기능입니다. 제목과 날짜를 입력하고 순서를 조정하세요.")
        description.setObjectName("sectionDescription")
        intro_text_layout.addWidget(title)
        intro_text_layout.addWidget(description)
        intro_layout.addLayout(intro_text_layout)
        intro_layout.addStretch()
        self.lbl_item_count = QLabel("0개")
        self.lbl_item_count.setObjectName("countBadge")
        intro_layout.addWidget(self.lbl_item_count, alignment=Qt.AlignTop)
        layout.addLayout(intro_layout)

        h_sort_layout = QHBoxLayout()
        sort_label = QLabel("정렬")
        sort_label.setObjectName("controlLabel")
        btn_sort_near = QPushButton("가까운 날짜 순")
        btn_sort_near.setObjectName("compactButton")
        btn_sort_near.clicked.connect(lambda: self.sort_items(reverse=False))
        btn_sort_far = QPushButton("먼 날짜 순")
        btn_sort_far.setObjectName("compactButton")
        btn_sort_far.clicked.connect(lambda: self.sort_items(reverse=True))
        h_sort_layout.addWidget(sort_label)
        h_sort_layout.addWidget(btn_sort_near)
        h_sort_layout.addWidget(btn_sort_far)
        h_sort_layout.addStretch()
        layout.addLayout(h_sort_layout)

        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.NoFrame)
        scroll.setStyleSheet("background: transparent;")

        self.items_widget = QWidget()
        self.items_widget.setStyleSheet("background: transparent;")
        self.items_layout = QVBoxLayout(self.items_widget)
        self.items_layout.setContentsMargins(0, 0, 0, 0)
        self.items_layout.setSpacing(8)
        self.items_layout.addStretch()
        scroll.setWidget(self.items_widget)
        layout.addWidget(scroll)

        self.entries = []
        for item in self.data['items']:
            self.add_item_row(item['title'], item['date'])

        btn_add = QPushButton("+ D-Day 추가")
        btn_add.setObjectName("outlineButton")
        btn_add.clicked.connect(lambda checked: self.add_item_row("D-Day", QDate.currentDate().toString("yyyy-MM-dd")))
        layout.addWidget(btn_add)

    def reset_to_defaults(self):
        # 1. 일반 설정 초기화
        self.dial_alpha.setValue(int(DEFAULT_DATA['alpha'] * 100))
        _select_combo_value(
            self.combo_time_format, TIME_FORMAT_VALUES, DEFAULT_DATA['time_format']
        )
        _select_combo_value(
            self.combo_date_format, DATE_FORMAT_VALUES, DEFAULT_DATA['date_format']
        )
        _select_combo_value(
            self.combo_day_format, DAY_FORMAT_VALUES, DEFAULT_DATA['day_format']
        )
        self.chk_top.setChecked(DEFAULT_DATA['topmost'])
        self.chk_glass_bg.setChecked(DEFAULT_DATA['use_glass_background'])
        self.chk_calendar.setChecked(DEFAULT_DATA.get('show_calendar', False))
        self.chk_auto_start.setChecked(DEFAULT_DATA.get('auto_start', False))

        # 2. 개별 세부 디자인 요소 초기화
        for key_prefix, controls in self.style_controls.items():
            with QSignalBlocker(controls['font']), QSignalBlocker(controls['size']):
                controls['font'].setCurrentFont(
                    QFont(DEFAULT_DATA[f'font_{key_prefix}'])
                )
                controls['size'].setValue(DEFAULT_DATA[f'size_{key_prefix}'])

            default_color = DEFAULT_DATA[f'color_{key_prefix}']
            self.data[f'color_{key_prefix}'] = default_color
            self._set_color_button_style(controls['btn'], default_color)
            self.update_style_preview(key_prefix)

    def add_item_row(self, title, date):
        row = QFrame()
        row.setObjectName("ddayEditorRow")
        row_layout = QHBoxLayout(row)
        row_layout.setContentsMargins(12, 10, 12, 10)
        row_layout.setSpacing(8)

        edt_title = QLineEdit(title)
        edt_title.setPlaceholderText("D-Day 제목")
        edt_title.setClearButtonEnabled(True)

        edt_date = GlassDateEdit()
        edt_date.setCalendarPopup(True)
        edt_date.setDisplayFormat("yyyy-MM-dd")
        edt_date.setMinimumWidth(125)
        edt_date.calendarWidget().setStyleSheet(glass_theme.get_calendar_style())

        qdate = QDate.fromString(date, "yyyy-MM-dd")
        if qdate.isValid():
            edt_date.setDate(qdate)
        else:
            edt_date.setDate(QDate.currentDate())

        btn_up = QPushButton("▲")
        btn_up.setFixedSize(26, 26)
        btn_up.setToolTip("위로 이동")
        btn_up.setObjectName("iconButton")
        btn_up.clicked.connect(lambda checked, r=row: self.move_item(r, -1))

        btn_down = QPushButton("▼")
        btn_down.setFixedSize(26, 26)
        btn_down.setToolTip("아래로 이동")
        btn_down.setObjectName("iconButton")
        btn_down.clicked.connect(lambda checked, r=row: self.move_item(r, 1))

        btn_del = QPushButton("✕")
        btn_del.setFixedSize(26, 26)
        btn_del.setToolTip("일정 삭제")
        btn_del.setObjectName("dangerIconButton")
        btn_del.clicked.connect(lambda checked, r=row: self.delete_item_row(r))

        row_layout.addWidget(edt_title)
        row_layout.addWidget(edt_date)
        row_layout.addWidget(btn_up)
        row_layout.addWidget(btn_down)
        row_layout.addWidget(btn_del)

        self.items_layout.insertWidget(self.items_layout.count()-1, row)
        self.entries.append((row, edt_title, edt_date))
        self.update_item_count()

    def update_item_count(self):
        self.lbl_item_count.setText("{}개".format(len(self.entries)))

    def _entry_index(self, row_widget):
        return next(
            (index for index, entry in enumerate(self.entries) if entry[0] is row_widget),
            -1,
        )

    def move_item(self, row_widget, direction):
        idx = self._entry_index(row_widget)
        if idx == -1:
            return

        new_idx = idx + direction
        if 0 <= new_idx < len(self.entries):
            self.entries[idx], self.entries[new_idx] = self.entries[new_idx], self.entries[idx]
            self.items_layout.insertWidget(new_idx, row_widget)

    def sort_items(self, reverse=False):
        self.entries.sort(key=lambda entry: entry[2].date(), reverse=reverse)
        for index, entry in enumerate(self.entries):
            self.items_layout.insertWidget(index, entry[0])

    def delete_item_row(self, row_widget):
        index = self._entry_index(row_widget)
        if index == -1:
            return
        self.entries.pop(index)
        row_widget.deleteLater()
        self.update_item_count()

    def _pick_color(self, key, btn_widget):
        current_color = QColor(self.data.get(key, '#ffffff'))
        dlg = GlassColorDialog(current_color, self)
        dlg.setWindowTitle("색상 선택")

        if dlg.exec():
            c = dlg.selectedColor()
            if c.isValid():
                self.data[key] = c.name()
                self._set_color_button_style(btn_widget, c.name())
                self.update_style_preview(key.replace('color_', '', 1))

    def reject(self):
        """취소 시 투명도 미리보기를 원래 값으로 되돌립니다."""
        if self.parent() and self.original_opacity is not None:
            self.parent().setWindowOpacity(self.original_opacity)
        super().reject()

    def _config_manager(self):
        parent = self.parent()
        return getattr(parent, 'config_mgr', None) if parent else None

    def _confirm_loaded_settings(self, result, title):
        if result.status not in ('ok', 'partial'):
            QMessageBox.warning(
                self, title,
                "설정 파일을 읽을 수 없습니다.\n\n{}".format(
                    result.error or "파일 형식이 올바르지 않습니다."
                )
            )
            return False

        item_count = len(result.data.get('items', []))
        warning_text = ""
        if result.warnings:
            warning_text = "\n\n확인 사항:\n- " + "\n- ".join(result.warnings)
        message = (
            "다음 설정을 적용하시겠습니까?\n\n"
            "파일: {}\nD-Day 항목: {}개{}"
        ).format(result.source or "백업 파일", item_count, warning_text)
        return QMessageBox.question(
            self, title, message,
            QMessageBox.Yes | QMessageBox.No,
            QMessageBox.No
        ) == QMessageBox.Yes

    def import_settings_file(self):
        manager = self._config_manager()
        if not manager:
            QMessageBox.warning(self, "설정 불러오기", "설정 관리자를 찾을 수 없습니다.")
            return
        file_path, _ = QFileDialog.getOpenFileName(
            self,
            "설정 파일 선택",
            str(manager.config_dir),
            "설정 파일 (*.ini *.bak);;모든 파일 (*.*)"
        )
        if not file_path:
            return
        result = manager.load_external(file_path)
        if self._confirm_loaded_settings(result, "설정 불러오기"):
            self.imported_data = copy.deepcopy(result.data)
            self.accept()

    def export_settings_file(self):
        manager = self._config_manager()
        if not manager:
            QMessageBox.warning(self, "설정 내보내기", "설정 관리자를 찾을 수 없습니다.")
            return
        file_path, _ = QFileDialog.getSaveFileName(
            self,
            "설정 파일 저장",
            "dday_config_export.ini",
            "INI 설정 파일 (*.ini)"
        )
        if not file_path:
            return
        if not file_path.lower().endswith('.ini'):
            file_path += '.ini'
        result = manager.export_settings(self.get_data(), file_path)
        if result.success:
            QMessageBox.information(self, "설정 내보내기", result.message)
        else:
            QMessageBox.warning(self, "설정 내보내기", result.message)

    def restore_latest_backup(self):
        manager = self._config_manager()
        if not manager:
            QMessageBox.warning(self, "백업 복원", "설정 관리자를 찾을 수 없습니다.")
            return
        result = manager.load_latest_backup()
        if self._confirm_loaded_settings(result, "최근 백업 복원"):
            self.imported_data = copy.deepcopy(result.data)
            self.accept()

    def get_data(self):
        if self.imported_data is not None:
            return copy.deepcopy(self.imported_data)

        # 1. 일반 설정 데이터
        self.data['alpha'] = self.dial_alpha.value() / 100.0
        self.data['topmost'] = self.chk_top.isChecked()
        self.data['use_glass_background'] = self.chk_glass_bg.isChecked()
        self.data['show_calendar'] = self.chk_calendar.isChecked()
        self.data['auto_start'] = self.chk_auto_start.isChecked()

        self.data['time_format'] = TIME_FORMAT_VALUES[
            self.combo_time_format.currentIndex()
        ]
        self.data['date_format'] = DATE_FORMAT_VALUES[
            self.combo_date_format.currentIndex()
        ]
        self.data['day_format'] = DAY_FORMAT_VALUES[
            self.combo_day_format.currentIndex()
        ]

        # 2. 세부 디자인 데이터 추출
        for key_prefix, controls in self.style_controls.items():
            self.data[f'font_{key_prefix}'] = controls['font'].currentFont().family()
            self.data[f'size_{key_prefix}'] = controls['size'].value()
            # 색상값은 버튼 클릭 시 self.data 안에 직접 할당됨

        # 3. D-Day 목록
        new_items = []
        for row, t, d in self.entries:
            if t.text().strip(): # 빈 항목 방지
                new_items.append({'title': t.text(), 'date': d.date().toString("yyyy-MM-dd")})
        if not new_items: # 모두 지워졌을 때 기본값
            new_items.append({'title': "D-Day", 'date': QDate.currentDate().toString("yyyy-MM-dd")})
        self.data['items'] = new_items

        return self.data
