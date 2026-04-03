import copy
from PySide6.QtWidgets import (QDialog, QVBoxLayout, QHBoxLayout, QLabel, 
                             QSlider, QCheckBox, QPushButton, QFrame, 
                             QScrollArea, QWidget, QLineEdit, QDateEdit, 
                             QColorDialog, QFontComboBox, QSizePolicy, QGridLayout, 
                             QComboBox, QTabWidget, QSpinBox)
from PySide6.QtCore import Qt, QDate
from PySide6.QtGui import QFont, QColor, QPainter, QPainterPath

import glass_theme
from config_manager import DEFAULT_DATA

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

class GlassInfoDialog(QDialog):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowFlags(Qt.FramelessWindowHint | Qt.Dialog)
        self.setAttribute(Qt.WA_TranslucentBackground)
        self.setStyleSheet(glass_theme.get_glass_dialog_style())
        self.setFont(QFont("Segoe UI"))
        self.resize(340, 360)
        
        if parent and hasattr(parent, 'app_icon'):
            self.setWindowIcon(parent.app_icon)
            
        self.drag_position = None
        self.init_ui()

    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        rect_path = QPainterPath()
        rect_path.addRoundedRect(0, 0, self.width(), self.height(), 16, 16)
        painter.fillPath(rect_path, glass_theme.get_glass_background_brush())
        painter.setPen(QColor(200, 200, 200, 150))
        painter.setBrush(Qt.NoBrush)
        painter.drawPath(rect_path)

    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self.drag_position = event.globalPosition().toPoint() - self.frameGeometry().topLeft()
            event.accept()

    def mouseMoveEvent(self, event):
        if event.buttons() == Qt.LeftButton and self.drag_position:
            self.move(event.globalPosition().toPoint() - self.drag_position)
            event.accept()

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
            <b>■ 버전 정보:</b> v2.2.1<br><br>
            <b>■ 공식 배포 페이지</b><br>
            <a href="https://mathtime.kr/?page=dday" style="color: #1a73e8; text-decoration: none;">https://mathtime.kr/?page=dday</a><br><br>
            <b>■ 개발자 정보</b><br>
            - 최근 업데이트: 2026.04.03<br>
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
        self.setStyleSheet("""
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
        """)
        
    def paintEvent(self, event):
        super().paintEvent(event)
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        painter.setPen(QColor("#808080"))
        f = self.font()
        f.setPointSize(8) 
        painter.setFont(f)
        painter.drawText(self.rect().adjusted(0, 0, -8, 0), Qt.AlignRight | Qt.AlignVCenter, "▼")

class GlassComboBox(QComboBox):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setStyleSheet("""
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
        """)
        
    def paintEvent(self, event):
        super().paintEvent(event)
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        painter.setPen(QColor("#808080"))
        f = self.font()
        f.setPointSize(8)
        painter.setFont(f)
        painter.drawText(self.rect().adjusted(0, 0, -8, 0), Qt.AlignRight | Qt.AlignVCenter, "▼")

class GlassDateEdit(QDateEdit):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setStyleSheet("""
            QDateEdit { padding-right: 20px; } 
            QDateEdit::down-arrow { image: none; } 
            QDateEdit::drop-down { border: none; background: transparent; width: 24px; }
        """)
        
    def paintEvent(self, event):
        super().paintEvent(event)
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        painter.setPen(QColor("#808080"))
        f = self.font()
        f.setPointSize(8)
        painter.setFont(f)
        painter.drawText(self.rect().adjusted(0, 0, -8, 0), Qt.AlignRight | Qt.AlignVCenter, "▼")

class GlassSpinBox(QSpinBox):
    def __init__(self, parent=None):
        super().__init__(parent)
        
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

class SettingsDialog(QDialog):
    def __init__(self, data, parent=None):
        super().__init__(parent)
        self.data = copy.deepcopy(data)
        
        self.drag_position = None
        
        self.setWindowFlags(Qt.FramelessWindowHint | Qt.Dialog)
        self.setFont(QFont("Segoe UI"))
        
        if parent and hasattr(parent, 'app_icon'):
            self.setWindowIcon(parent.app_icon)
            
        # 쾌적한 인터페이스를 위해 사이즈 조정
        self.resize(600, 580)
        
        self.setAttribute(Qt.WA_TranslucentBackground)
        self.setStyleSheet(glass_theme.get_glass_dialog_style())

        self.style_controls = {} 
        self.init_ui()

    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        rect_path = QPainterPath()
        rect_path.addRoundedRect(0, 0, self.width(), self.height(), 16, 16)
        painter.fillPath(rect_path, glass_theme.get_glass_background_brush())
        # 외곽 테두리를 조금 더 은은하게 변경
        painter.setPen(QColor(200, 200, 200, 150))
        painter.setBrush(Qt.NoBrush)
        painter.drawPath(rect_path)

    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self.drag_position = event.globalPosition().toPoint() - self.frameGeometry().topLeft()
            event.accept()

    def mouseMoveEvent(self, event):
        if event.buttons() == Qt.LeftButton and self.drag_position:
            self.move(event.globalPosition().toPoint() - self.drag_position)
            event.accept()

    def init_ui(self):
        layout = QVBoxLayout()
        layout.setContentsMargins(30, 30, 30, 30) # 여백을 늘려 모던한 느낌 강조
        self.setLayout(layout)
        
        header_layout = QHBoxLayout()
        lbl_header = QLabel("환경 설정")
        lbl_header.setStyleSheet("font-size: 18pt; font-weight: 800; color: #1a1a1a;")
        header_layout.addWidget(lbl_header)
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
        layout.addSpacing(15)
        
        # --- 탭 구성 시작 ---
        self.tabs = QTabWidget()
        # 스타일은 glass_theme.py에서 전역으로 가져오므로 별도 지정 불필요
        
        tab_general = QWidget()
        tab_style = QWidget()
        tab_items = QWidget()
        
        self._init_general_tab(tab_general)
        self._init_style_tab(tab_style)
        self._init_items_tab(tab_items)
        
        self.tabs.addTab(tab_general, "일반 설정")
        self.tabs.addTab(tab_style, "디자인")
        self.tabs.addTab(tab_items, "D-Day 관리")
        
        layout.addWidget(self.tabs)
        layout.addSpacing(15)
        # --- 탭 구성 끝 ---
        
        # 하단 버튼 영역
        bottom_layout = QHBoxLayout()
        
        btn_reset = QPushButton("초기화")
        btn_reset.setMinimumWidth(100)
        btn_reset.setStyleSheet("""
            QPushButton { background-color: #ffffff; color: #505050; font-weight: 600; padding: 10px; border-radius: 8px; }
            QPushButton:hover { background-color: #f0f2f5; }
        """)
        btn_reset.clicked.connect(self.reset_to_defaults)
        
        btn_save = QPushButton("저장 및 적용")
        btn_save.setMinimumWidth(120)
        btn_save.setStyleSheet("""
            QPushButton { background-color: #006cd9; color: white; border: none; font-weight: 600; padding: 10px; border-radius: 8px; }
            QPushButton:hover { background-color: #0056b3; }
            QPushButton:pressed { background-color: #004494; }
        """)
        btn_save.clicked.connect(self.accept)
        
        bottom_layout.addWidget(btn_reset)
        bottom_layout.addStretch()
        bottom_layout.addWidget(btn_save)
        layout.addLayout(bottom_layout)

    def _init_general_tab(self, tab):
        # 탭 안쪽 여백 추가
        layout = QVBoxLayout(tab)
        layout.setContentsMargins(20, 25, 20, 20)
        layout.setSpacing(20)
        
        grid = QGridLayout()
        grid.setSpacing(15)
        
        lbl_alpha = QLabel("창 투명도:")
        lbl_alpha.setStyleSheet("font-weight: 600; color: #404040;")
        grid.addWidget(lbl_alpha, 0, 0)
        
        self.slider_alpha = QSlider(Qt.Horizontal)
        self.slider_alpha.setRange(20, 100)
        self.slider_alpha.setValue(int(self.data['alpha'] * 100))
        if self.parent():
            self.slider_alpha.valueChanged.connect(lambda v: self.parent().setWindowOpacity(v/100))
        grid.addWidget(self.slider_alpha, 0, 1)

        lbl_time = QLabel("시간 표기:")
        lbl_time.setStyleSheet("font-weight: 600; color: #404040;")
        grid.addWidget(lbl_time, 1, 0)
        
        self.combo_time_format = GlassComboBox()
        self.combo_time_format.addItems(["24시간제", "12시간제 (AM/PM)"])
        self.combo_time_format.setCurrentIndex(0 if self.data.get('time_format', '24h') == '24h' else 1)
        grid.addWidget(self.combo_time_format, 1, 1)

        lbl_date = QLabel("날짜 표기:")
        lbl_date.setStyleSheet("font-weight: 600; color: #404040;")
        grid.addWidget(lbl_date, 2, 0)
        
        self.combo_date_format = GlassComboBox()
        self.combo_date_format.addItems(["YYYY-MM-DD", "MM/DD/YYYY", "DD/MM/YYYY"])
        df_val = self.data.get('date_format', 'yyyy-mm-dd')
        if df_val == 'mm/dd/yyyy': self.combo_date_format.setCurrentIndex(1)
        elif df_val == 'dd/mm/yyyy': self.combo_date_format.setCurrentIndex(2)
        else: self.combo_date_format.setCurrentIndex(0)
        grid.addWidget(self.combo_date_format, 2, 1)

        lbl_day = QLabel("요일 표기:")
        lbl_day.setStyleSheet("font-weight: 600; color: #404040;")
        grid.addWidget(lbl_day, 3, 0)
        
        self.combo_day_format = GlassComboBox()
        self.combo_day_format.addItems(["한국어 (월, 화...)", "영어 (Mon, Tue...)"])
        self.combo_day_format.setCurrentIndex(0 if self.data.get('day_format', 'kor') == 'kor' else 1)
        grid.addWidget(self.combo_day_format, 3, 1)

        layout.addLayout(grid)
        
        # 선
        line = QFrame()
        line.setFrameShape(QFrame.HLine)
        line.setStyleSheet("background-color: #eaedf0; max-height: 1px; border: none;")
        layout.addWidget(line)
        
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
        
        layout.addLayout(opt_layout)
        layout.addStretch()

    def _init_style_tab(self, tab):
        layout = QVBoxLayout(tab)
        layout.setContentsMargins(10, 15, 10, 10)
        
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.NoFrame)
        scroll.setStyleSheet("background: transparent;")
        
        content = QWidget()
        content.setStyleSheet("background: transparent;")
        v_layout = QVBoxLayout(content)
        v_layout.setSpacing(15)
        v_layout.setContentsMargins(10, 10, 10, 10)
        
        # 세분화 생성 (달력 추가)
        self.add_style_row("현재 시간", "time", v_layout)
        self.add_style_row("현재 날짜", "date", v_layout)
        
        line1 = QFrame()
        line1.setFrameShape(QFrame.HLine)
        line1.setStyleSheet("background-color: #eaedf0; max-height: 1px; border: none;")
        v_layout.addWidget(line1)
        
        self.add_style_row("D-Day 제목", "dday_title", v_layout)
        self.add_style_row("남은 일수", "dday_count", v_layout)
        self.add_style_row("목표 날짜", "dday_date", v_layout)
        
        line2 = QFrame()
        line2.setFrameShape(QFrame.HLine)
        line2.setStyleSheet("background-color: #eaedf0; max-height: 1px; border: none;")
        v_layout.addWidget(line2)
        
        self.add_style_row("달력", "calendar", v_layout)
        
        v_layout.addStretch()
        scroll.setWidget(content)
        layout.addWidget(scroll)

    def add_style_row(self, name, key_prefix, layout):
        row = QWidget()
        row_layout = QHBoxLayout(row)
        row_layout.setContentsMargins(0, 0, 0, 0)
        row_layout.setSpacing(10)
        
        lbl = QLabel(name)
        lbl.setFixedWidth(75)
        lbl.setStyleSheet("font-weight: 600; color: #404040;")
        
        cmb_font = GlassFontComboBox()
        cmb_font.setCurrentFont(QFont(self.data.get(f'font_{key_prefix}', 'Segoe UI')))
        cmb_font.setFixedWidth(160)
        
        lbl_size = QLabel("크기:")
        lbl_size.setStyleSheet("color: #606060;")
        spin_size = GlassSpinBox() # <--- GlassSpinBox로 변경 적용된 부분
        spin_size.setRange(5, 150)
        spin_size.setValue(self.data.get(f'size_{key_prefix}', 12))
        
        lbl_color = QLabel("색상:")
        lbl_color.setStyleSheet("color: #606060;")
        btn_color = QPushButton()
        btn_color.setFixedSize(26, 26)
        current_color = self.data.get(f'color_{key_prefix}', '#ffffff')
        # 모던한 색상 버튼
        btn_color.setStyleSheet(f"""
            background-color: {current_color}; 
            border: 1px solid #d0d0d0; 
            border-radius: 13px; /* 원형 */
        """)
        btn_color.clicked.connect(lambda _, k=f'color_{key_prefix}', b=btn_color: self._pick_color(k, b))
        
        row_layout.addWidget(lbl)
        row_layout.addWidget(cmb_font)
        row_layout.addSpacing(5)
        row_layout.addWidget(lbl_size)
        row_layout.addWidget(spin_size)
        row_layout.addSpacing(5)
        row_layout.addWidget(lbl_color)
        row_layout.addWidget(btn_color)
        row_layout.addStretch()
        
        layout.addWidget(row)
        self.style_controls[key_prefix] = {'font': cmb_font, 'size': spin_size, 'btn': btn_color}

    def _init_items_tab(self, tab):
        layout = QVBoxLayout(tab)
        layout.setContentsMargins(15, 20, 15, 15)
        layout.setSpacing(15)
        
        h_sort_layout = QHBoxLayout()
        btn_sort_near = QPushButton("가까운 날짜 순 (▲)")
        btn_sort_near.clicked.connect(lambda: self.sort_items(reverse=False))
        btn_sort_far = QPushButton("먼 날짜 순 (▼)")
        btn_sort_far.clicked.connect(lambda: self.sort_items(reverse=True))
        h_sort_layout.addWidget(btn_sort_near)
        h_sort_layout.addWidget(btn_sort_far)
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
        btn_add.setStyleSheet("""
            QPushButton { background-color: #f0f7ff; color: #006cd9; border: 1px dashed #006cd9; font-weight: bold; padding: 8px; border-radius: 8px;}
            QPushButton:hover { background-color: #e0f0ff; border: 1px solid #006cd9; }
        """)
        btn_add.clicked.connect(lambda checked: self.add_item_row("D-Day", QDate.currentDate().toString("yyyy-MM-dd")))
        layout.addWidget(btn_add)

    def reset_to_defaults(self):
        # 1. 일반 설정 초기화
        self.slider_alpha.setValue(int(DEFAULT_DATA['alpha'] * 100))
        self.combo_time_format.setCurrentIndex(0 if DEFAULT_DATA['time_format'] == '24h' else 1)
        self.combo_date_format.setCurrentIndex(0)
        self.combo_day_format.setCurrentIndex(0)
        self.chk_top.setChecked(DEFAULT_DATA['topmost'])
        self.chk_glass_bg.setChecked(DEFAULT_DATA['use_glass_background'])
        self.chk_calendar.setChecked(DEFAULT_DATA.get('show_calendar', False))
        
        # 2. 개별 세부 디자인 요소 초기화
        for key_prefix, controls in self.style_controls.items():
            controls['font'].setCurrentFont(QFont(DEFAULT_DATA[f'font_{key_prefix}']))
            controls['size'].setValue(DEFAULT_DATA[f'size_{key_prefix}'])
            
            default_color = DEFAULT_DATA[f'color_{key_prefix}']
            self.data[f'color_{key_prefix}'] = default_color
            controls['btn'].setStyleSheet(f"background-color: {default_color}; border: 1px solid #d0d0d0; border-radius: 13px;")

    def add_item_row(self, title, date):
        row = QWidget()
        row_layout = QHBoxLayout(row)
        row_layout.setContentsMargins(0, 0, 0, 0)
        
        edt_title = QLineEdit(title)
        edt_title.setPlaceholderText("제목 입력")
        
        edt_date = GlassDateEdit()
        edt_date.setCalendarPopup(True)
        edt_date.setDisplayFormat("yyyy-MM-dd")
        edt_date.calendarWidget().setStyleSheet(glass_theme.get_calendar_style())
        
        qdate = QDate.fromString(date, "yyyy-MM-dd")
        if qdate.isValid():
            edt_date.setDate(qdate)
        else:
            edt_date.setDate(QDate.currentDate())
        
        arrow_style = """
            QPushButton { color: #555555; background-color: transparent; border: 1px solid #e0e4e8; border-radius: 4px; padding: 0px; }
            QPushButton:hover { background-color: #f0f2f5; color: #000; }
        """

        btn_up = QPushButton("▲"); btn_up.setFixedSize(26, 26)
        btn_up.setStyleSheet(arrow_style)
        btn_up.clicked.connect(lambda checked, r=row: self.move_item(r, -1))
        
        btn_down = QPushButton("▼"); btn_down.setFixedSize(26, 26)
        btn_down.setStyleSheet(arrow_style)
        btn_down.clicked.connect(lambda checked, r=row: self.move_item(r, 1))

        btn_del = QPushButton("✕"); btn_del.setFixedSize(26, 26)
        btn_del.setStyleSheet("""
            QPushButton { color: #888888; font-size: 13px; font-weight: bold; border: 1px solid #e0e4e8; background-color: white; border-radius: 4px; }
            QPushButton:hover { color: #d32f2f; background-color: #ffebee; border: 1px solid #ef9a9a; }
        """)
        btn_del.clicked.connect(lambda checked, r=row: self.delete_item_row(r))
        
        row_layout.addWidget(edt_title)
        row_layout.addWidget(edt_date)
        row_layout.addWidget(btn_up)
        row_layout.addWidget(btn_down)
        row_layout.addWidget(btn_del)
        
        self.items_layout.insertWidget(self.items_layout.count()-1, row)
        self.entries.append((row, edt_title, edt_date))

    def move_item(self, row_widget, direction):
        idx = -1
        for i, (w, t, d) in enumerate(self.entries):
            if w == row_widget:
                idx = i; break
        if idx == -1: return
        
        new_idx = idx + direction
        if 0 <= new_idx < len(self.entries):
            self.entries[idx], self.entries[new_idx] = self.entries[new_idx], self.entries[idx]
            self.items_layout.insertWidget(new_idx, row_widget)

    def sort_items(self, reverse=False):
        self.entries.sort(key=lambda entry: entry[2].date(), reverse=reverse)
        for i, (row, t, d) in enumerate(self.entries):
            self.items_layout.insertWidget(i, row)

    def delete_item_row(self, row_widget):
        for i, (w, t, d) in enumerate(self.entries):
            if w == row_widget:
                self.entries.pop(i); break
        row_widget.deleteLater()

    def _pick_color(self, key, btn_widget):
        current_color = QColor(self.data.get(key, '#ffffff'))
        dlg = GlassColorDialog(current_color, self)
        dlg.setWindowTitle("색상 선택")
        
        if dlg.exec():
            c = dlg.selectedColor()
            if c.isValid():
                self.data[key] = c.name()
                btn_widget.setStyleSheet(f"background-color: {c.name()}; border: 1px solid #d0d0d0; border-radius: 13px;")

    def get_data(self):
        # 1. 일반 설정 데이터
        self.data['alpha'] = self.slider_alpha.value() / 100.0
        self.data['topmost'] = self.chk_top.isChecked()
        self.data['use_glass_background'] = self.chk_glass_bg.isChecked()
        self.data['show_calendar'] = self.chk_calendar.isChecked()
        
        self.data['time_format'] = '24h' if self.combo_time_format.currentIndex() == 0 else '12h'
        df_idx = self.combo_date_format.currentIndex()
        if df_idx == 1: self.data['date_format'] = 'mm/dd/yyyy'
        elif df_idx == 2: self.data['date_format'] = 'dd/mm/yyyy'
        else: self.data['date_format'] = 'yyyy-mm-dd'
        
        self.data['day_format'] = 'kor' if self.combo_day_format.currentIndex() == 0 else 'eng'
        
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