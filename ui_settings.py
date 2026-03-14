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
        painter.setPen(QColor(255, 255, 255, 100))
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
        rect_path.addRoundedRect(0, 0, self.width(), self.height(), 20, 20)
        painter.fillPath(rect_path, glass_theme.get_glass_background_brush())
        painter.setPen(QColor(255, 255, 255, 150))
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
        btn_close.setStyleSheet("QPushButton { background: transparent; border: none; font-size: 14pt; color: #505050; } QPushButton:hover { color: #d32f2f; }")
        btn_close.clicked.connect(self.accept)
        header_layout.addWidget(btn_close)
        
        layout.addLayout(header_layout)
        layout.addSpacing(10)
        
        info_label = QLabel()
        info_label.setOpenExternalLinks(True) 
        info_html = """
        <div style="font-family: 'Segoe UI', 'Malgun Gothic', sans-serif; font-size: 13px; color: #202020;">
            <h3 style="margin-bottom: 10px;">■ My D-Day Widget</h3>
            <p><b>[사용 방법]</b></p>
            <ul style="margin-top: 5px; padding-left: 20px;">
                <li><b>이동 :</b> 마우스 드래그</li>
                <li><b>설정 :</b> 위젯 더블 클릭</li>
                <li><b>크기 :</b> 우측 하단(◢) 드래그</li>
            </ul>
            <hr style="background-color: rgba(0,0,0,0.1); border: 0; height: 1px; margin: 10px 0;">
            <b>■ 버전 정보:</b> v2.1.0<br><br>
            <b>■ 공식 배포 페이지</b><br>
            <a href="https://mathtime.kr/dday.html" style="color: #0078D7; text-decoration: none;">https://mathtime.kr/dday.html</a><br><br>
            <b>■ 개발자 정보</b><br>
            - 최근 업데이트: 2026.03.14<br>
            - ✉: trsketch@gmail.com<br>
            Copyright 2026 lottoria-dev. All rights reserved.<br><br>
            <span style='color: #d9534f;'><b>정식 배포 페이지를 제외한 곳에서<br>
            임의의 수정 및 재배포를 금지합니다.</b></span>
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
                background-color: rgba(255, 255, 255, 240);
                color: #202020;
                selection-background-color: #0078D7;
                selection-color: white;
                border: 1px solid #dcdcdc;
                border-radius: 6px;
                outline: 0px;
            }
        """)
        
    def paintEvent(self, event):
        super().paintEvent(event)
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        painter.setPen(QColor("#555555"))
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
                background-color: rgba(255, 255, 255, 240);
                color: #202020;
                selection-background-color: #0078D7;
                selection-color: white;
                border: 1px solid #dcdcdc;
                border-radius: 6px;
                outline: 0px;
            }
        """)
        
    def paintEvent(self, event):
        super().paintEvent(event)
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        painter.setPen(QColor("#555555"))
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
        painter.setPen(QColor("#555555"))
        f = self.font()
        f.setPointSize(8)
        painter.setFont(f)
        painter.drawText(self.rect().adjusted(0, 0, -8, 0), Qt.AlignRight | Qt.AlignVCenter, "▼")

class SettingsDialog(QDialog):
    def __init__(self, data, parent=None):
        super().__init__(parent)
        self.data = copy.deepcopy(data)
        
        self.drag_position = None
        
        self.setWindowFlags(Qt.FramelessWindowHint | Qt.Dialog)
        self.setFont(QFont("Segoe UI"))
        
        if parent and hasattr(parent, 'app_icon'):
            self.setWindowIcon(parent.app_icon)
            
        # 폭을 넓혀 탭 메뉴가 쾌적하게 보이도록 설정
        self.resize(560, 480)
        
        self.setAttribute(Qt.WA_TranslucentBackground)
        self.setStyleSheet(glass_theme.get_glass_dialog_style())

        self.style_controls = {} # 글꼴/크기/색상 컨트롤 저장용
        self.init_ui()

    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        rect_path = QPainterPath()
        rect_path.addRoundedRect(0, 0, self.width(), self.height(), 20, 20)
        painter.fillPath(rect_path, glass_theme.get_glass_background_brush())
        painter.setPen(QColor(255, 255, 255, 150))
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
        lbl_header = QLabel("D-Day Settings")
        lbl_header.setStyleSheet("font-size: 18pt; font-weight: bold; color: #101010;")
        header_layout.addWidget(lbl_header)
        header_layout.addStretch()
        
        btn_close = QPushButton("✕")
        btn_close.setFixedSize(30, 30)
        btn_close.setStyleSheet("""
            QPushButton { background-color: transparent; color: #505050; border: none; font-size: 14pt; }
            QPushButton:hover { color: #d32f2f; }
        """)
        btn_close.clicked.connect(self.reject)
        header_layout.addWidget(btn_close)
        
        layout.addLayout(header_layout)
        layout.addSpacing(10)
        
        # --- 탭 구성 시작 ---
        self.tabs = QTabWidget()
        self.tabs.setStyleSheet("""
            QTabWidget::pane { border: none; background: transparent; }
            QTabBar::tab {
                background: rgba(255, 255, 255, 0.4);
                border: 1px solid rgba(0, 0, 0, 0.1);
                border-bottom: none;
                padding: 8px 15px;
                margin-right: 3px;
                border-top-left-radius: 6px;
                border-top-right-radius: 6px;
                color: #555;
                font-weight: bold;
            }
            QTabBar::tab:selected {
                background: rgba(255, 255, 255, 0.8);
                border: 1px solid #0078D7;
                border-bottom: 2px solid #0078D7;
                color: #0078D7;
            }
            QTabBar::tab:hover:!selected { background: rgba(255, 255, 255, 0.6); }
        """)
        
        tab_general = QWidget()
        tab_style = QWidget()
        tab_items = QWidget()
        
        self._init_general_tab(tab_general)
        self._init_style_tab(tab_style)
        self._init_items_tab(tab_items)
        
        self.tabs.addTab(tab_general, "일반 설정")
        self.tabs.addTab(tab_style, "세부 디자인")
        self.tabs.addTab(tab_items, "D-Day 관리")
        
        layout.addWidget(self.tabs)
        layout.addSpacing(10)
        # --- 탭 구성 끝 ---
        
        # 하단 버튼 영역
        bottom_layout = QHBoxLayout()
        
        btn_reset = QPushButton("초기화")
        btn_reset.setStyleSheet("""
            QPushButton { background-color: #f5f5f5; color: #555555; border: 1px solid #dcdcdc; font-weight: bold; padding: 10px; border-radius: 6px; }
            QPushButton:hover { background-color: #e8e8e8; }
        """)
        btn_reset.clicked.connect(self.reset_to_defaults)
        
        btn_save = QPushButton("저장 및 적용")
        btn_save.clicked.connect(self.accept)
        btn_save.setStyleSheet("""
            QPushButton { background-color: #0078d7; color: white; border: none; font-weight: bold; padding: 10px; border-radius: 6px; }
            QPushButton:hover { background-color: #0063b1; }
        """)
        
        bottom_layout.addWidget(btn_reset)
        bottom_layout.addWidget(btn_save)
        layout.addLayout(bottom_layout)

    def _init_general_tab(self, tab):
        layout = QVBoxLayout(tab)
        layout.setSpacing(15)
        
        grid = QGridLayout()
        grid.setSpacing(12)
        
        grid.addWidget(QLabel("창 투명도 (전체):"), 0, 0)
        self.slider_alpha = QSlider(Qt.Horizontal)
        self.slider_alpha.setRange(20, 100)
        self.slider_alpha.setValue(int(self.data['alpha'] * 100))
        if self.parent():
            self.slider_alpha.valueChanged.connect(lambda v: self.parent().setWindowOpacity(v/100))
        grid.addWidget(self.slider_alpha, 0, 1)

        grid.addWidget(QLabel("시간 표기:"), 1, 0)
        self.combo_time_format = GlassComboBox()
        self.combo_time_format.addItems(["24시간제", "12시간제 (AM/PM)"])
        self.combo_time_format.setCurrentIndex(0 if self.data.get('time_format', '24h') == '24h' else 1)
        grid.addWidget(self.combo_time_format, 1, 1)

        grid.addWidget(QLabel("날짜 표기:"), 2, 0)
        self.combo_date_format = GlassComboBox()
        self.combo_date_format.addItems(["YYYY-MM-DD", "MM/DD/YYYY", "DD/MM/YYYY"])
        df_val = self.data.get('date_format', 'yyyy-mm-dd')
        if df_val == 'mm/dd/yyyy': self.combo_date_format.setCurrentIndex(1)
        elif df_val == 'dd/mm/yyyy': self.combo_date_format.setCurrentIndex(2)
        else: self.combo_date_format.setCurrentIndex(0)
        grid.addWidget(self.combo_date_format, 2, 1)

        grid.addWidget(QLabel("요일 표기:"), 3, 0)
        self.combo_day_format = GlassComboBox()
        self.combo_day_format.addItems(["한국어 (월, 화...)", "영어 (Mon, Tue...)"])
        self.combo_day_format.setCurrentIndex(0 if self.data.get('day_format', 'kor') == 'kor' else 1)
        grid.addWidget(self.combo_day_format, 3, 1)

        layout.addLayout(grid)
        
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
        layout.setContentsMargins(0, 10, 0, 0)
        
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.NoFrame)
        scroll.setStyleSheet("background: transparent;")
        
        content = QWidget()
        content.setStyleSheet("background: transparent;")
        v_layout = QVBoxLayout(content)
        v_layout.setSpacing(15)
        
        # 디자인 요소들을 5개의 행으로 세분화 생성
        self.add_style_row("현재 시간", "time", v_layout)
        self.add_style_row("현재 날짜", "date", v_layout)
        
        line = QFrame()
        line.setFrameShape(QFrame.HLine)
        line.setStyleSheet("background-color: rgba(0,0,0,0.1); max-height: 1px;")
        v_layout.addWidget(line)
        
        self.add_style_row("D-Day 제목", "dday_title", v_layout)
        self.add_style_row("남은 일수", "dday_count", v_layout)
        self.add_style_row("목표 날짜", "dday_date", v_layout)
        
        v_layout.addStretch()
        scroll.setWidget(content)
        layout.addWidget(scroll)

    def add_style_row(self, name, key_prefix, layout):
        row = QWidget()
        row_layout = QHBoxLayout(row)
        row_layout.setContentsMargins(0, 0, 0, 0)
        
        lbl = QLabel(name)
        lbl.setFixedWidth(75)
        lbl.setStyleSheet("font-weight: bold;")
        
        cmb_font = GlassFontComboBox()
        cmb_font.setCurrentFont(QFont(self.data.get(f'font_{key_prefix}', 'Segoe UI')))
        cmb_font.setFixedWidth(180)
        
        spin_size = QSpinBox()
        spin_size.setRange(5, 150)
        spin_size.setValue(self.data.get(f'size_{key_prefix}', 12))
        
        btn_color = QPushButton()
        btn_color.setFixedSize(24, 24)
        current_color = self.data.get(f'color_{key_prefix}', '#ffffff')
        btn_color.setStyleSheet(f"background-color: {current_color}; border: 1px solid #888; border-radius: 4px;")
        btn_color.clicked.connect(lambda _, k=f'color_{key_prefix}', b=btn_color: self._pick_color(k, b))
        
        row_layout.addWidget(lbl)
        row_layout.addWidget(cmb_font)
        row_layout.addWidget(QLabel("크기:"))
        row_layout.addWidget(spin_size)
        row_layout.addWidget(QLabel("색상:"))
        row_layout.addWidget(btn_color)
        
        layout.addWidget(row)
        self.style_controls[key_prefix] = {'font': cmb_font, 'size': spin_size, 'btn': btn_color}

    def _init_items_tab(self, tab):
        layout = QVBoxLayout(tab)
        layout.setContentsMargins(0, 10, 0, 0)
        
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
        self.items_layout.addStretch()
        scroll.setWidget(self.items_widget)
        layout.addWidget(scroll)
        
        self.entries = []
        for item in self.data['items']:
            self.add_item_row(item['title'], item['date'])
            
        btn_add = QPushButton("+ D-Day 추가")
        btn_add.setStyleSheet("""
            QPushButton { background-color: #f0f8ff; color: #0078d7; border: 1px solid #0078d7; font-weight: bold; }
            QPushButton:hover { background-color: #e6f2ff; }
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
        
        # 2. 5가지 개별 세부 디자인 요소 초기화
        for key_prefix, controls in self.style_controls.items():
            controls['font'].setCurrentFont(QFont(DEFAULT_DATA[f'font_{key_prefix}']))
            controls['size'].setValue(DEFAULT_DATA[f'size_{key_prefix}'])
            
            default_color = DEFAULT_DATA[f'color_{key_prefix}']
            self.data[f'color_{key_prefix}'] = default_color
            controls['btn'].setStyleSheet(f"background-color: {default_color}; border: 1px solid #888; border-radius: 4px;")

    def add_item_row(self, title, date):
        row = QWidget()
        row_layout = QHBoxLayout(row)
        row_layout.setContentsMargins(0, 5, 0, 5)
        
        edt_title = QLineEdit(title)
        
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
            QPushButton { color: #555555; background-color: transparent; border: 1px solid #e0e0e0; border-radius: 4px; padding: 0px; }
            QPushButton:hover { background-color: #f0f0f0; color: #000; }
        """

        btn_up = QPushButton("▲"); btn_up.setFixedSize(24, 24)
        btn_up.setStyleSheet(arrow_style)
        btn_up.clicked.connect(lambda checked, r=row: self.move_item(r, -1))
        
        btn_down = QPushButton("▼"); btn_down.setFixedSize(24, 24)
        btn_down.setStyleSheet(arrow_style)
        btn_down.clicked.connect(lambda checked, r=row: self.move_item(r, 1))

        btn_del = QPushButton("✕"); btn_del.setFixedSize(28, 28)
        btn_del.setStyleSheet("""
            QPushButton { color: #888888; font-size: 15px; font-weight: bold; border: 1px solid #e0e0e0; background-color: white; border-radius: 4px; }
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
                # 버튼의 배경색을 선택한 색상으로 동기화
                btn_widget.setStyleSheet(f"background-color: {c.name()}; border: 1px solid #888; border-radius: 4px;")

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
            new_items.append({'title': t.text(), 'date': d.date().toString("yyyy-MM-dd")})
        self.data['items'] = new_items
        
        return self.data