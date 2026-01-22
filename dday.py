import sys
import os
import configparser
import copy
import calendar
from datetime import datetime
from PyQt6.QtWidgets import (QApplication, QWidget, QLabel, QVBoxLayout, 
                             QHBoxLayout, QMenu, QDialog, QLineEdit, 
                             QPushButton, QComboBox, QSlider, QCheckBox, 
                             QFrame, QColorDialog, QScrollArea, QSizeGrip,
                             QDateEdit, QSizePolicy, QFontComboBox, QSystemTrayIcon, QMessageBox)
from PyQt6.QtCore import Qt, QTimer, QPoint, QDate
from PyQt6.QtGui import QFont, QColor, QPalette, QPainter, QCursor, QAction, QIcon

# ---------------------------------------------------------
# 리소스 경로 찾기 함수 (EXE 내부 포함)
# ---------------------------------------------------------
def resource_path(relative_path):
    """ Get absolute path to resource, works for dev and for PyInstaller """
    try:
        # PyInstaller creates a temp folder and stores path in _MEIPASS
        base_path = sys._MEIPASS
    except Exception:
        base_path = os.path.abspath(".")

    return os.path.join(base_path, relative_path)

# ---------------------------------------------------------
# 기본 설정
# ---------------------------------------------------------
CONFIG_FILE = 'dday_config_pyqt.ini'
# 기본 데이터를 오늘 날짜로 설정
DEFAULT_DATA = [{"title": "D-Day", "date": datetime.now().strftime("%Y-%m-%d")}]
# 아이콘 경로를 resource_path 함수로 감싸서 내부 파일도 찾게 함
ICON_FILE = resource_path('icon.png') 

# [수정] 강제 다크 모드 스타일시트 삭제 (시스템 기본 테마 사용)

class DDayWidget(QWidget):
    def __init__(self):
        super().__init__()
        
        self.data = self.load_settings()
        self.drag_position = None
        self.resizing = False
        
        # 아이콘 설정
        if os.path.exists(ICON_FILE):
            self.app_icon = QIcon(ICON_FILE)
            self.setWindowIcon(self.app_icon)
        else:
            self.app_icon = QIcon()

        # 윈도우 설정 (투명 배경 핵심)
        self.setWindowFlags(Qt.WindowType.FramelessWindowHint | Qt.WindowType.Tool)
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        
        # 시스템 트레이 설정
        self.init_tray_icon()
        
        # UI 초기화
        self.init_ui()

        # 설정값 적용
        self.apply_window_settings()
        
        # 타이머 설정 (0.1초마다 갱신)
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.update_counts)
        self.timer.start(100)

    def init_tray_icon(self):
        self.tray_icon = QSystemTrayIcon(self)
        self.tray_icon.setIcon(self.app_icon)
        
        # 트레이 메뉴
        tray_menu = QMenu()
        
        action_show = QAction("보이기/숨기기", self)
        action_show.triggered.connect(self.toggle_visibility)
        tray_menu.addAction(action_show)
        
        action_settings = QAction("설정", self)
        action_settings.triggered.connect(self.open_settings)
        tray_menu.addAction(action_settings)
        
        tray_menu.addSeparator()
        
        action_quit = QAction("종료", self)
        action_quit.triggered.connect(self.quit_application) # 종료 함수 연결
        tray_menu.addAction(action_quit)
        
        self.tray_icon.setContextMenu(tray_menu)
        self.tray_icon.show()
        
        self.tray_icon.activated.connect(self.on_tray_activated)

    def on_tray_activated(self, reason):
        if reason == QSystemTrayIcon.ActivationReason.DoubleClick:
            self.open_settings()
        elif reason == QSystemTrayIcon.ActivationReason.Trigger:
            self.activateWindow()
            self.raise_()

    def toggle_visibility(self):
        if self.isVisible():
            self.hide()
        else:
            self.show()
            self.activateWindow()

    def quit_application(self):
        """애플리케이션 완전 종료"""
        self.save_settings()
        # 트레이 아이콘을 숨겨야 잔상이 남지 않음
        if hasattr(self, 'tray_icon'):
            self.tray_icon.hide()
        # 애플리케이션 강제 종료
        QApplication.instance().quit()

    def load_settings(self):
        config = configparser.ConfigParser()
        data = {
            'x': 100, 'y': 100, 'w': 350, 'h': 250, 
            'items': [],
            'alpha': 0.9,
            'topmost': False,
            'text_color': '#ffffff',
            'count_color': '#ff6b6b',
            'time_size': 45, 
            'date_size': 12,
            'font_family': 'Malgun Gothic'
        }
        
        if os.path.exists(CONFIG_FILE):
            try:
                config.read(CONFIG_FILE, encoding='utf-8')
                if 'Window' in config:
                    data['x'] = config.getint('Window', 'x', fallback=100)
                    data['y'] = config.getint('Window', 'y', fallback=100)
                    data['w'] = config.getint('Window', 'w', fallback=350)
                    data['h'] = config.getint('Window', 'h', fallback=250)
                    data['alpha'] = config.getfloat('Window', 'alpha', fallback=0.9)
                    data['topmost'] = config.getboolean('Window', 'topmost', fallback=False)
                    data['text_color'] = config.get('Window', 'text_color', fallback='#ffffff')
                    data['count_color'] = config.get('Window', 'count_color', fallback='#ff6b6b')
                    data['time_size'] = config.getint('Window', 'time_size', fallback=45)
                    data['date_size'] = config.getint('Window', 'date_size', fallback=12)
                    data['font_family'] = config.get('Window', 'font_family', fallback='Malgun Gothic')
                
                sections = [s for s in config.sections() if s.startswith('DDay-')]
                sections.sort(key=lambda x: int(x.split('-')[1]))
                for s in sections:
                    data['items'].append({
                        'title': config.get(s, 'title'),
                        'date': config.get(s, 'date')
                    })
            except: pass
            
        if not data['items']:
            data['items'] = copy.deepcopy(DEFAULT_DATA)
            
        return data

    def save_settings(self):
        config = configparser.ConfigParser()
        config['Window'] = {
            'x': str(self.x()), 'y': str(self.y()),
            'w': str(self.width()), 'h': str(self.height()),
            'alpha': str(self.data['alpha']),
            'topmost': str(self.data['topmost']),
            'text_color': self.data['text_color'],
            'count_color': self.data['count_color'],
            'time_size': str(self.data['time_size']),
            'date_size': str(self.data['date_size']),
            'font_family': self.data['font_family']
        }
        for i, item in enumerate(self.data['items']):
            config[f'DDay-{i+1}'] = item
            
        with open(CONFIG_FILE, 'w', encoding='utf-8') as f:
            config.write(f)

    def apply_window_settings(self):
        self.setGeometry(self.data['x'], self.data['y'], self.data['w'], self.data['h'])
        self.setWindowOpacity(self.data['alpha'])
        
        current_flags = self.windowFlags()
        if self.data['topmost']:
            self.setWindowFlags(current_flags | Qt.WindowType.WindowStaysOnTopHint)
        else:
            self.setWindowFlags(current_flags & ~Qt.WindowType.WindowStaysOnTopHint)
        self.show()

    def init_ui(self):
        # 메인 레이아웃
        self.layout = QVBoxLayout()
        self.layout.setContentsMargins(20, 10, 20, 20) 
        self.layout.setSpacing(0) 
        self.layout.setAlignment(Qt.AlignmentFlag.AlignTop) 
        self.setLayout(self.layout)
        
        # 1. 상단 시계 영역 (시간 + 날짜)
        # 시간 (크게)
        self.lbl_time = QLabel("00:00:00")
        self.lbl_time.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.lbl_time.setSizePolicy(QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Fixed) 
        self.layout.addWidget(self.lbl_time)
        
        # 날짜 (작게)
        self.lbl_date = QLabel("0000-00-00 (월)")
        self.lbl_date.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.lbl_date.setSizePolicy(QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Fixed) 
        self.layout.addWidget(self.lbl_date)
        
        # 2. 구분선
        self.line = QFrame()
        self.line.setFrameShape(QFrame.Shape.HLine)
        self.line.setFrameShadow(QFrame.Shadow.Plain)
        self.layout.addWidget(self.line)
        
        # 3. D-Day 항목들을 담을 컨테이너
        self.items_layout = QVBoxLayout()
        self.items_layout.setSpacing(2) 
        self.layout.addLayout(self.items_layout)
        
        # [중요] 하단 빈 공간 채우기 (Stretch)
        self.layout.addStretch(1)
        
        # 리사이즈 그립 (우측 하단, 크기 조절 기능)
        self.sizegrip = QSizeGrip(self)
        self.sizegrip.setStyleSheet("background-color: transparent; width: 20px; height: 20px;")
        
        # 시각적 그립 (문자 '◢' 사용)
        self.lbl_grip = QLabel("◢", self)
        self.lbl_grip.setAlignment(Qt.AlignmentFlag.AlignBottom | Qt.AlignmentFlag.AlignRight)
        self.lbl_grip.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents)
        
        self.refresh_widgets()

    def refresh_widgets(self):
        # 기존 위젯 삭제
        while self.items_layout.count():
            child = self.items_layout.takeAt(0)
            if child.widget(): child.widget().deleteLater()
            
        font_color = self.data['text_color']
        font_family = self.data['font_family']
        self.labels = []
        
        # 폰트 설정
        font_title = QFont(font_family, 12, QFont.Weight.Bold)
        font_count = QFont(font_family, 15, QFont.Weight.Bold)
        font_detail = QFont(font_family, 9)
        
        for item in self.data['items']:
            row = QWidget()
            row_layout = QHBoxLayout(row)
            row_layout.setContentsMargins(0, 5, 0, 5)
            
            # 왼쪽: 타이틀
            lbl_title = QLabel(item['title'])
            lbl_title.setFont(font_title)
            lbl_title.setStyleSheet(f"color: {font_color};")
            row_layout.addWidget(lbl_title, alignment=Qt.AlignmentFlag.AlignTop)
            
            # 오른쪽: 정보들
            right_box = QWidget()
            right_layout = QVBoxLayout(right_box)
            right_layout.setContentsMargins(0, 0, 0, 0)
            right_layout.setSpacing(2)
            
            lbl_count = QLabel("D-?")
            lbl_count.setFont(font_count)
            lbl_count.setAlignment(Qt.AlignmentFlag.AlignRight)
            
            lbl_detail = QLabel("...")
            lbl_detail.setFont(font_detail)
            lbl_detail.setStyleSheet(f"color: {font_color};")
            lbl_detail.setAlignment(Qt.AlignmentFlag.AlignRight)
            
            right_layout.addWidget(lbl_count)
            right_layout.addWidget(lbl_detail)
            
            row_layout.addWidget(right_box)
            
            self.items_layout.addWidget(row)
            self.labels.append({
                'date': item['date'],
                'count': lbl_count,
                'detail': lbl_detail
            })
            
        # 스타일 적용 (설정된 폰트 크기 반영)
        self.line.setStyleSheet(f"color: {font_color}; background-color: {font_color};")
        
        self.lbl_time.setStyleSheet(f"color: {font_color}; font-family: '{font_family}'; font-size: {self.data['time_size']}pt; font-weight: bold;")
        self.lbl_date.setStyleSheet(f"color: {font_color}; font-family: '{font_family}'; font-size: {self.data['date_size']}pt; font-weight: bold;")
        
        self.lbl_grip.setStyleSheet(f"color: {font_color}; background-color: transparent; font-size: 16px;")
        self.lbl_grip.adjustSize()

    def calculate_ymd_diff(self, start_date, end_date):
        """두 날짜 사이의 년/월/일 차이 계산"""
        # 순서 정렬 (항상 start <= end가 되도록)
        if start_date > end_date:
            start_date, end_date = end_date, start_date
            
        years = end_date.year - start_date.year
        months = end_date.month - start_date.month
        days = end_date.day - start_date.day

        # 일(Day) 보정
        if days < 0:
            months -= 1
            # 전달의 마지막 날짜 구하기
            prev_month = end_date.month - 1 if end_date.month > 1 else 12
            prev_year = end_date.year if end_date.month > 1 else end_date.year - 1
            _, days_in_prev_month = calendar.monthrange(prev_year, prev_month)
            days += days_in_prev_month

        # 월(Month) 보정
        if months < 0:
            years -= 1
            months += 12
            
        return years, months, days

    def update_counts(self):
        now = datetime.now()
        today = now.date()
        
        # 현재 시간 업데이트
        self.lbl_time.setText(now.strftime("%H:%M:%S"))
        
        # 날짜 업데이트 (요일 포함)
        days = ["월", "화", "수", "목", "금", "토", "일"]
        day_str = days[now.weekday()]
        self.lbl_date.setText(f"{now.strftime('%Y-%m-%d')} ({day_str})")
        
        for item in self.labels:
            try:
                target_dt = datetime.strptime(item['date'], "%Y-%m-%d")
                target_date = target_dt.date()
                
                # D-Day 계산
                diff_days = (target_date - today).days
                
                # 모든 상태에서 사용자 설정 색상 사용
                color = self.data['count_color']
                
                if diff_days > 0:
                    txt = f"D-{diff_days}"
                    suffix = " 남음"
                elif diff_days < 0:
                    txt = f"D+{abs(diff_days)}"
                    suffix = " 지남"
                else:
                    txt = "D-Day"
                    suffix = ""
                
                item['count'].setText(txt)
                item['count'].setStyleSheet(f"color: {color};")
                
                # 상세 날짜
                if diff_days == 0:
                    item['detail'].setText("오늘입니다!")
                else:
                    y, m, d = self.calculate_ymd_diff(today, target_date)
                    
                    parts = []
                    if y > 0: parts.append(f"{y}년")
                    if m > 0: parts.append(f"{m}개월")
                    if d > 0: parts.append(f"{d}일")
                    if not parts: parts = ["0일"] # 당일이 아닌데 차이가 없는 경우 방지
                    
                    # 예: 1년 2개월 3일 남음 (430일)
                    item['detail'].setText(f"{' '.join(parts)}{suffix} ({abs(diff_days)}일)")
                
            except:
                item['count'].setText("Error")

    # ----------------------------------------------------
    # 이벤트 처리
    # ----------------------------------------------------
    def mousePressEvent(self, event):
        if event.button() == Qt.MouseButton.LeftButton:
            self.drag_position = event.globalPosition().toPoint() - self.frameGeometry().topLeft()
            event.accept()

    def mouseMoveEvent(self, event):
        if event.buttons() == Qt.MouseButton.LeftButton and self.drag_position:
            self.move(event.globalPosition().toPoint() - self.drag_position)
            event.accept()

    def mouseDoubleClickEvent(self, event):
        if event.button() == Qt.MouseButton.LeftButton:
            self.open_settings()

    def contextMenuEvent(self, event):
        menu = QMenu(self)
        # [수정] 메뉴 스타일도 기본 스타일로 변경 (다크 테마 제거)
        action_edit = menu.addAction("설정 편집")
        action_info = menu.addAction("정보 (About)")
        menu.addSeparator()
        action_exit = menu.addAction("종료")
        
        action = menu.exec(event.globalPos())
        if action == action_edit: self.open_settings()
        elif action == action_info: self.show_info()
        elif action == action_exit: self.quit_application()

    def resizeEvent(self, event):
        # sizegrip 위치 조정
        if hasattr(self, 'sizegrip'):
            rect = self.rect()
            if hasattr(self, 'lbl_grip'):
                self.lbl_grip.move(rect.right() - self.lbl_grip.width() - 2, rect.bottom() - self.lbl_grip.height() - 2)
            self.sizegrip.move(rect.right() - self.sizegrip.width(), rect.bottom() - self.sizegrip.height())
        super().resizeEvent(event)

    def closeEvent(self, event):
        if hasattr(self, 'tray_icon'):
            self.tray_icon.hide()
        self.save_settings()
        event.accept()

    def show_info(self):
        msg = QMessageBox(self)
        msg.setWindowTitle("정보")
        if hasattr(self, 'app_icon'):
            msg.setWindowIcon(self.app_icon)
        
        # [수정] 다크 스타일 제거 -> 기본 시스템 스타일
        # msg.setStyleSheet(DARK_STYLESHEET) 
        
        # [수정] 글자 색상을 시스템 기본으로 사용 (흰색 강제 제거)
        info_html = """
        <div style="font-family: 'Malgun Gothic', sans-serif; font-size: 13px;">
            <h3 style="margin-bottom: 10px;">■ My D-Day Widget</h3>
            
            <p><b>[사용 방법]</b></p>
            <ul style="margin-top: 5px; padding-left: 20px;">
                <li><b>이동 :</b> 마우스 드래그</li>
                <li><b>설정 :</b> 위젯 더블 클릭</li>
                <li><b>크기 :</b> 우측 하단(◢) 드래그</li>
            </ul>

            <p><b>[정보]</b></p>
            <ul style="margin-top: 5px; padding-left: 20px;">
                <li><b>개발일 :</b> 2026. 01. 21</li>
                <li><b>문의 :</b> trsketch@gmail.com</li>
            </ul>
            
            <hr>
            <p style="color: #d32f2f; margin-top: 10px;"><b>※ 무단 배포 및 상업적 사용 금지</b></p>
            <p style="font-size: 11px; opacity: 0.7;">Copyright 2026. All rights reserved.</p>
        </div>
        """
        msg.setText(info_html)
        msg.show()

    def open_settings(self):
        # [수정] 설정 창 열기 전 현재 위치와 크기 정보를 data에 동기화
        self.data['x'] = self.x()
        self.data['y'] = self.y()
        self.data['w'] = self.width()
        self.data['h'] = self.height()
        
        dlg = SettingsDialog(self.data, self)
        if dlg.exec():
            # 설정 저장 및 적용
            self.data = dlg.get_data()
            self.apply_window_settings()
            self.refresh_widgets()
            self.save_settings()

class SettingsDialog(QDialog):
    def __init__(self, data, parent=None):
        super().__init__(parent)
        self.data = copy.deepcopy(data)
        self.setWindowTitle("D-Day 설정")
        if hasattr(parent, 'app_icon'):
            self.setWindowIcon(parent.app_icon)
        self.resize(450, 600)
        
        # [수정] 다크 스타일 제거 -> 기본 시스템 스타일
        # self.setStyleSheet(DARK_STYLESHEET)
        
        self.init_ui()

    def init_ui(self):
        layout = QVBoxLayout()
        self.setLayout(layout)
        
        layout.addWidget(QLabel("<b>[화면 설정]</b>"))
        
        # 투명도
        h_layout = QHBoxLayout()
        h_layout.addWidget(QLabel("투명도:"))
        self.slider_alpha = QSlider(Qt.Orientation.Horizontal)
        self.slider_alpha.setRange(20, 100)
        self.slider_alpha.setValue(int(self.data['alpha'] * 100))
        self.slider_alpha.valueChanged.connect(lambda v: self.parent().setWindowOpacity(v/100))
        h_layout.addWidget(self.slider_alpha)
        layout.addLayout(h_layout)

        # 시간 글자 크기
        h_layout_ts = QHBoxLayout()
        h_layout_ts.addWidget(QLabel("시간 크기:"))
        self.slider_time = QSlider(Qt.Orientation.Horizontal)
        self.slider_time.setRange(20, 100)
        self.slider_time.setValue(self.data['time_size'])
        h_layout_ts.addWidget(self.slider_time)
        layout.addLayout(h_layout_ts)

        # 날짜 글자 크기
        h_layout_ds = QHBoxLayout()
        h_layout_ds.addWidget(QLabel("날짜 크기:"))
        self.slider_date = QSlider(Qt.Orientation.Horizontal)
        self.slider_date.setRange(8, 50)
        self.slider_date.setValue(self.data['date_size'])
        h_layout_ds.addWidget(self.slider_date)
        layout.addLayout(h_layout_ds)
        
        # 글꼴 선택 (QFontComboBox)
        h_layout_font = QHBoxLayout()
        h_layout_font.addWidget(QLabel("글꼴:"))
        self.combo_font = QFontComboBox()
        self.combo_font.setCurrentFont(QFont(self.data['font_family']))
        h_layout_font.addWidget(self.combo_font)
        layout.addLayout(h_layout_font)

        self.chk_top = QCheckBox("항상 위에 고정")
        self.chk_top.setChecked(self.data['topmost'])
        layout.addWidget(self.chk_top)
        
        # 색상 변경 버튼 (가로 배치)
        h_color_layout = QHBoxLayout()
        btn_text_color = QPushButton("글자 색상 변경")
        btn_text_color.clicked.connect(self.pick_text_color)
        h_color_layout.addWidget(btn_text_color)
        btn_count_color = QPushButton("숫자 색상 변경")
        btn_count_color.clicked.connect(self.pick_count_color)
        h_color_layout.addWidget(btn_count_color)
        layout.addLayout(h_color_layout)
        
        line = QFrame(); line.setFrameShape(QFrame.Shape.HLine); layout.addWidget(line)
        
        layout.addWidget(QLabel("<b>[D-Day 목록]</b>"))

        # [추가] 정렬 버튼
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
        # 스크롤 영역 내부 위젯도 배경색 지정
        self.items_widget = QWidget()
        # [수정] 스크롤 컨텐츠 ID 제거 (스타일시트 제거했으므로 불필요)
        # self.items_widget.setObjectName("scroll_content") 
        
        self.items_layout = QVBoxLayout(self.items_widget)
        self.items_layout.addStretch()
        scroll.setWidget(self.items_widget)
        layout.addWidget(scroll)
        
        self.entries = []
        for item in self.data['items']:
            self.add_item_row(item['title'], item['date'])
            
        btn_add = QPushButton("+ D-Day 추가")
        btn_add.clicked.connect(lambda: self.add_item_row("D-Day", QDate.currentDate().toString("yyyy-MM-dd")))
        layout.addWidget(btn_add)
        
        btn_save = QPushButton("저장 및 적용")
        btn_save.clicked.connect(self.accept)
        # [수정] 저장 버튼 색상은 유지하되, 전체 테마는 시스템 기본을 따름
        btn_save.setStyleSheet("background-color: #4ecdc4; font-weight: bold; padding: 10px; color: black;") 
        layout.addWidget(btn_save)

    def add_item_row(self, title, date):
        row = QWidget()
        row_layout = QHBoxLayout(row)
        row_layout.setContentsMargins(0, 0, 0, 0)
        
        edt_title = QLineEdit(title)
        
        edt_date = QDateEdit()
        edt_date.setCalendarPopup(True)
        edt_date.setDisplayFormat("yyyy-MM-dd")
        
        qdate = QDate.fromString(date, "yyyy-MM-dd")
        if qdate.isValid():
            edt_date.setDate(qdate)
        else:
            edt_date.setDate(QDate.currentDate())
        
        # 순서 변경 버튼
        btn_up = QPushButton("▲")
        btn_up.setFixedWidth(25)
        btn_up.clicked.connect(lambda checked, r=row: self.move_item(r, -1))
        
        btn_down = QPushButton("▼")
        btn_down.setFixedWidth(25)
        btn_down.clicked.connect(lambda checked, r=row: self.move_item(r, 1))

        btn_del = QPushButton("X")
        btn_del.setFixedWidth(30)
        # [수정] 삭제 버튼도 기본 색상 유지
        btn_del.setStyleSheet("background-color: #ff6b6b; color: white; font-weight: bold; border: none;")
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
                idx = i
                break
        
        if idx == -1: return
        
        new_idx = idx + direction
        if 0 <= new_idx < len(self.entries):
            self.entries[idx], self.entries[new_idx] = self.entries[new_idx], self.entries[idx]
            self.items_layout.insertWidget(new_idx, row_widget)

    def sort_items(self, reverse=False):
        def get_date_key(entry):
            return entry[2].date()
            
        self.entries.sort(key=get_date_key, reverse=reverse)
        
        for i, (row, t, d) in enumerate(self.entries):
            self.items_layout.insertWidget(i, row)

    def delete_item_row(self, row_widget):
        for i, (w, t, d) in enumerate(self.entries):
            if w == row_widget:
                self.entries.pop(i)
                break
        row_widget.deleteLater()

    def pick_text_color(self):
        c = QColorDialog.getColor(QColor(self.data['text_color']), self, "글자 색상 선택")
        if c.isValid():
            self.data['text_color'] = c.name()

    def pick_count_color(self):
        c = QColorDialog.getColor(QColor(self.data['count_color']), self, "D-Day 숫자 색상 선택")
        if c.isValid():
            self.data['count_color'] = c.name()

    def get_data(self):
        self.data['alpha'] = self.slider_alpha.value() / 100.0
        self.data['topmost'] = self.chk_top.isChecked()
        self.data['time_size'] = self.slider_time.value()
        self.data['date_size'] = self.slider_date.value()
        self.data['font_family'] = self.combo_font.currentFont().family()
        
        new_items = []
        for row, t, d in self.entries:
            title = t.text()
            date_str = d.date().toString("yyyy-MM-dd")
            new_items.append({'title': title, 'date': date_str})
            
        self.data['items'] = new_items
        return self.data

if __name__ == '__main__':
    app = QApplication(sys.argv)
    widget = DDayWidget()
    sys.exit(app.exec())