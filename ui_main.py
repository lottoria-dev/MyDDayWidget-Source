import os
import calendar
from datetime import datetime, timedelta
from PySide6.QtWidgets import (QApplication, QWidget, QLabel, QVBoxLayout, 
                             QHBoxLayout, QMenu, QFrame, QSizeGrip,
                             QSizePolicy, QSystemTrayIcon, QMessageBox, QPushButton, QGridLayout)
from PySide6.QtCore import Qt, QTimer, Signal
from PySide6.QtGui import QFont, QIcon, QAction, QPainter, QPainterPath, QColor, QPen

# 사용자 정의 모듈 임포트
from utils import resource_path, calculate_ymd_diff
from config_manager import ConfigManager
from ui_settings import SettingsDialog, GlassInfoDialog
import glass_theme

ICON_FILE_NAME = 'icon.png'

class CalendarCell(QLabel):
    rightClicked = Signal(str)

    def __init__(self, parent=None):
        super().__init__(parent)
        self.date_str = None
        self.is_today = False
        self.has_dday = False
        self.setAlignment(Qt.AlignCenter)
        self.setFont(QFont("Segoe UI", 9))
        self.setMinimumSize(24, 24)
        self.setCursor(Qt.PointingHandCursor)
        self.setToolTip("우클릭하여 D-Day 추가")

    def set_data(self, text, date_str, is_today, has_dday, is_current_month, text_color):
        self.setText(text)
        self.date_str = date_str
        self.is_today = is_today
        self.has_dday = has_dday

        # 현재 달이 아닌 날짜는 투명도(66 hex)를 주어 희미하게 처리
        if text_color.startswith('#') and len(text_color) == 7:
            opacity = "" if is_current_month else "66"
            self.setStyleSheet(f"color: {text_color}{opacity}; background: transparent;")
        else:
            self.setStyleSheet(f"color: {text_color}; background: transparent;")
        self.update()

    def paintEvent(self, event):
        super().paintEvent(event)
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)

        w, h = self.width(), self.height()

        # 오늘 날짜: 하단 붉은색 밑줄
        if self.is_today:
            pen = QPen(QColor("#ff4757"), 2)
            pen.setCapStyle(Qt.RoundCap)
            painter.setPen(pen)
            painter.drawLine(w // 4, h - 3, w * 3 // 4, h - 3)

        # D-Day 있는 날짜: 상단 중앙에 민트색 점
        if self.has_dday:
            painter.setBrush(QColor("#1dd1a1"))
            painter.setPen(Qt.NoPen)
            painter.drawEllipse(w // 2 - 2, 2, 4, 4)

    def mousePressEvent(self, event):
        if event.button() == Qt.RightButton and self.date_str:
            self.rightClicked.emit(self.date_str)
        super().mousePressEvent(event)


class SFGlassCalendar(QWidget):
    dateRightClicked = Signal(str)

    def __init__(self, parent=None):
        super().__init__(parent)
        self.current_date = datetime.now().date()
        self.display_year = self.current_date.year
        self.display_month = self.current_date.month
        self.dday_set = set()
        self.text_color = "#ffffff"

        self.setObjectName("calendarContainer")
        self.setStyleSheet("""
            QWidget#calendarContainer {
                background-color: rgba(255, 255, 255, 0.05);
                border: 1px solid rgba(255, 255, 255, 0.15);
                border-radius: 12px;
            }
            QWidget#calendarContainer:hover {
                background-color: rgba(255, 255, 255, 0.15);
                border: 1px solid rgba(255, 255, 255, 0.6);
            }
        """)
        self.init_ui()

    def init_ui(self):
        self.layout = QVBoxLayout(self)
        self.layout.setContentsMargins(10, 10, 10, 10)
        self.layout.setSpacing(5)

        # 달력 상단 네비게이션 (헤더)
        header_layout = QHBoxLayout()
        self.btn_prev = QPushButton("◀")
        self.btn_next = QPushButton("▶")
        for btn in (self.btn_prev, self.btn_next):
            btn.setFixedSize(24, 24)
            btn.setStyleSheet("background: transparent; border: none; color: white;")
            btn.setCursor(Qt.PointingHandCursor)
        self.btn_prev.clicked.connect(self.prev_month)
        self.btn_next.clicked.connect(self.next_month)

        self.lbl_month = QLabel()
        self.lbl_month.setAlignment(Qt.AlignCenter)
        self.lbl_month.setFont(QFont("Segoe UI", 10, QFont.Bold))

        header_layout.addWidget(self.btn_prev)
        header_layout.addWidget(self.lbl_month, 1)
        header_layout.addWidget(self.btn_next)
        self.layout.addLayout(header_layout)

        # 달력 7x6 그리드
        self.grid_layout = QGridLayout()
        self.grid_layout.setSpacing(2)

        weekdays = ["일", "월", "화", "수", "목", "금", "토"]
        for i, wd in enumerate(weekdays):
            lbl = QLabel(wd)
            lbl.setAlignment(Qt.AlignCenter)
            lbl.setFont(QFont("Segoe UI", 8, QFont.Bold))
            self.grid_layout.addWidget(lbl, 0, i)

        self.cells = []
        for row in range(1, 7):
            for col in range(7):
                cell = CalendarCell()
                cell.rightClicked.connect(self.dateRightClicked.emit)
                self.grid_layout.addWidget(cell, row, col)
                self.cells.append(cell)

        self.layout.addLayout(self.grid_layout)

    def set_data(self, dday_dates, text_color):
        self.dday_set = set(dday_dates)
        self.text_color = text_color
        self.lbl_month.setStyleSheet(f"color: {text_color}; background: transparent;")
        for btn in (self.btn_prev, self.btn_next):
            btn.setStyleSheet(f"background: transparent; border: none; color: {text_color};")

        for i in range(7):
            lbl = self.grid_layout.itemAtPosition(0, i).widget()
            if i == 0:
                lbl.setStyleSheet(f"color: #ff6b6b; background: transparent;")
            elif i == 6:
                lbl.setStyleSheet(f"color: #6baaff; background: transparent;")
            else:
                lbl.setStyleSheet(f"color: {text_color}; background: transparent;")

        self.update_calendar()

    def prev_month(self):
        if self.display_month == 1:
            self.display_month = 12
            self.display_year -= 1
        else:
            self.display_month -= 1
        self.update_calendar()

    def next_month(self):
        if self.display_month == 12:
            self.display_month = 1
            self.display_year += 1
        else:
            self.display_month += 1
        self.update_calendar()

    def update_calendar(self):
        self.lbl_month.setText(f"{self.display_year}년 {self.display_month}월")
        today = datetime.now().date()

        cal_obj = calendar.Calendar(firstweekday=calendar.SUNDAY)
        dates = list(cal_obj.itermonthdates(self.display_year, self.display_month))

        # 달력 빈 칸이 없도록 42칸(6주) 꽉 채우기
        while len(dates) < 42:
            dates.append(dates[-1] + timedelta(days=1))

        for i, d in enumerate(dates):
            if i >= 42: break
            cell = self.cells[i]
            date_str = d.strftime("%Y-%m-%d")
            is_today = (d == today)
            has_dday = (date_str in self.dday_set)
            is_current_month = (d.month == self.display_month)

            cell.set_data(str(d.day), date_str, is_today, has_dday, is_current_month, self.text_color)


class DDayWidget(QWidget):
    def __init__(self):
        super().__init__()
        
        # 1. 설정 관리자 초기화 및 로드
        self.config_mgr = ConfigManager()
        self.data = self.config_mgr.load_settings()
        
        self.drag_position = None
        self.resizing = False
        
        # 2. 아이콘 설정
        icon_path = resource_path(ICON_FILE_NAME)
        if os.path.exists(icon_path):
            self.app_icon = QIcon(icon_path)
            self.setWindowIcon(self.app_icon)
        else:
            self.app_icon = QIcon()

        # 3. 윈도우 기본 설정 (투명, 프레임 없음)
        self.setWindowFlags(Qt.FramelessWindowHint | Qt.Tool)
        self.setAttribute(Qt.WA_TranslucentBackground)
        
        # 4. 트레이 및 UI 초기화
        self.init_tray_icon()
        self.init_ui()
        self.apply_window_settings()
        
        # 5. 타이머 설정
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.update_counts)
        self.timer.start(100)

    def init_tray_icon(self):
        self.tray_icon = QSystemTrayIcon(self)
        self.tray_icon.setIcon(self.app_icon)
        
        tray_menu = QMenu()
        tray_menu.setAttribute(Qt.WA_TranslucentBackground)
        tray_menu.setStyleSheet(glass_theme.get_glass_menu_style())

        action_show = QAction("보이기/숨기기", self)
        action_show.triggered.connect(self.toggle_visibility)
        tray_menu.addAction(action_show)
        
        action_settings = QAction("설정", self)
        action_settings.triggered.connect(self.open_settings)
        tray_menu.addAction(action_settings)
        
        tray_menu.addSeparator()
        
        action_quit = QAction("종료", self)
        action_quit.triggered.connect(self.quit_application)
        tray_menu.addAction(action_quit)
        
        self.tray_icon.setContextMenu(tray_menu)
        self.tray_icon.show()
        self.tray_icon.activated.connect(self.on_tray_activated)

    def on_tray_activated(self, reason):
        if reason == QSystemTrayIcon.DoubleClick:
            self.open_settings()
        elif reason == QSystemTrayIcon.Trigger:
            self.activateWindow()
            self.raise_()

    def toggle_visibility(self, checked=False):
        if self.isVisible(): self.hide()
        else: self.show(); self.activateWindow()

    def quit_application(self, checked=False):
        self.save_current_state()
        if hasattr(self, 'tray_icon'): self.tray_icon.hide()
        QApplication.instance().quit()

    def save_current_state(self):
        geometry = (self.x(), self.y(), self.width(), self.height())
        self.config_mgr.save_settings(self.data, geometry)

    def apply_window_settings(self):
        self.setGeometry(self.data['x'], self.data['y'], self.data['w'], self.data['h'])
        self.setWindowOpacity(self.data['alpha'])
        
        current_flags = self.windowFlags()
        if self.data['topmost']:
            self.setWindowFlags(current_flags | Qt.WindowStaysOnTopHint)
        else:
            self.setWindowFlags(current_flags & ~Qt.WindowStaysOnTopHint)
        self.show()

    def init_ui(self):
        self.layout = QVBoxLayout()
        self.layout.setContentsMargins(20, 20, 20, 20) 
        self.layout.setSpacing(12) 
        self.layout.setAlignment(Qt.AlignTop) 
        self.setLayout(self.layout)
        
        # 시계와 달력을 담을 디지털 시계 느낌의 둥근 컨테이너
        self.clock_container = QWidget()
        self.clock_layout = QVBoxLayout(self.clock_container)
        # [수정] 시계 영역 유리 배경 안쪽 여백 축소 (좌 8, 상 2, 우 8, 하 6)
        self.clock_layout.setContentsMargins(8, 2, 8, 6)
        self.clock_layout.setSpacing(2)
        
        # 시간 및 날짜
        self.lbl_time = QLabel("00:00:00")
        self.lbl_time.setAttribute(Qt.WA_TransparentForMouseEvents)
        self.lbl_time.setAlignment(Qt.AlignCenter)
        self.lbl_time.setSizePolicy(QSizePolicy.Preferred, QSizePolicy.Fixed) 
        self.clock_layout.addWidget(self.lbl_time)
        
        self.lbl_date = QLabel("0000-00-00 (월)")
        self.lbl_date.setAttribute(Qt.WA_TransparentForMouseEvents)
        self.lbl_date.setAlignment(Qt.AlignCenter)
        self.lbl_date.setSizePolicy(QSizePolicy.Preferred, QSizePolicy.Fixed) 
        self.clock_layout.addWidget(self.lbl_date)
        
        self.layout.addWidget(self.clock_container)
        
        # 음각 효과를 주기 위한 구분선 설정
        self.line = QFrame()
        self.line.setFrameShape(QFrame.HLine)
        self.layout.addWidget(self.line)
        
        # D-Day 아이템 컨테이너
        self.items_layout = QVBoxLayout()
        self.items_layout.setSpacing(2) 
        self.layout.addLayout(self.items_layout)
        
        # SF 글래스 테마 투명 달력 위젯
        self.calendar = SFGlassCalendar()
        self.calendar.dateRightClicked.connect(self.open_settings_for_new_dday)
        self.layout.addWidget(self.calendar)
        
        self.layout.addStretch(1)
        
        # 리사이즈 그립
        self.sizegrip = QSizeGrip(self)
        self.sizegrip.setStyleSheet("background-color: transparent; width: 20px; height: 20px;")
        
        self.lbl_grip = QLabel("◢", self)
        self.lbl_grip.setAlignment(Qt.AlignBottom | Qt.AlignRight)
        self.lbl_grip.setAttribute(Qt.WA_TransparentForMouseEvents)
        
        self.refresh_widgets()

    def refresh_widgets(self):
        while self.items_layout.count():
            child = self.items_layout.takeAt(0)
            if child.widget(): child.widget().deleteLater()
            
        # [수정] D-Day 항목들 사이의 상하 간격 축소 (4 -> 2)
        self.items_layout.setSpacing(2)
        
        # 투명 화이트 유리 & SF 마우스 오버 효과 (시계 영역)
        self.clock_container.setObjectName("clockContainer")
        self.clock_container.setStyleSheet("""
            QWidget#clockContainer {
                background-color: rgba(255, 255, 255, 0.05);
                border: 1px solid rgba(255, 255, 255, 0.15);
                border-radius: 15px;
            }
            QWidget#clockContainer:hover {
                background-color: rgba(255, 255, 255, 0.15);
                border: 1px solid rgba(255, 255, 255, 0.6);
            }
            QLabel { background: transparent; border: none; }
        """)
        
        self.line.setStyleSheet("""
            QFrame {
                border-top: 1px solid rgba(255, 255, 255, 0.1);
                border-bottom: 1px solid rgba(255, 255, 255, 0.3);
                background: transparent;
                max-height: 2px;
            }
        """)
        
        # 각각의 글꼴, 크기, 색상 데이터 불러오기
        font_time = QFont(self.data.get('font_time', 'Segoe UI'), self.data.get('size_time', 45), QFont.Bold)
        font_date = QFont(self.data.get('font_date', 'Segoe UI'), self.data.get('size_date', 12), QFont.Bold)
        font_dday_title = QFont(self.data.get('font_dday_title', 'Segoe UI'), self.data.get('size_dday_title', 12), QFont.Bold)
        font_dday_count = QFont(self.data.get('font_dday_count', 'Segoe UI'), self.data.get('size_dday_count', 15), QFont.Bold)
        font_dday_date = QFont(self.data.get('font_dday_date', 'Segoe UI'), self.data.get('size_dday_date', 8))

        color_time = self.data.get('color_time', '#ffffff')
        color_date = self.data.get('color_date', '#ffffff')
        color_dday_title = self.data.get('color_dday_title', '#ffffff')
        color_dday_count = self.data.get('color_dday_count', '#ff6b6b')
        color_dday_date = self.data.get('color_dday_date', '#ffffff')
        
        self.labels = []
        
        for item in self.data['items']:
            row = QWidget()
            row.setObjectName("ddayRow")
            row_layout = QHBoxLayout(row)
            
            # 투명 화이트 유리 & SF 마우스 오버 효과 (D-Day 영역)
            row.setStyleSheet("""
                QWidget#ddayRow {
                    background-color: rgba(255, 255, 255, 0.05);
                    border: 1px solid rgba(255, 255, 255, 0.15);
                    border-radius: 12px;
                }
                QWidget#ddayRow:hover {
                    background-color: rgba(255, 255, 255, 0.15);
                    border: 1px solid rgba(255, 255, 255, 0.6);
                }
                QLabel { background: transparent; border: none; }
            """)
            # [수정] D-Day 항목 유리 배경 안쪽 여백 축소 (좌 8, 상 2, 우 8, 하 4)
            row_layout.setContentsMargins(8, 2, 8, 4)
            
            lbl_title = QLabel(item['title'])
            lbl_title.setFont(font_dday_title)
            lbl_title.setStyleSheet(f"color: {color_dday_title};")
            
            row_layout.addWidget(lbl_title, alignment=Qt.AlignLeft | Qt.AlignVCenter)
            
            # 우측 레이아웃 (D-Day 카운트)
            right_box = QWidget()
            right_layout = QVBoxLayout(right_box)
            right_layout.setContentsMargins(0, 0, 0, 0)
            right_layout.setSpacing(2)
            
            lbl_count = QLabel("D-?")
            lbl_count.setFont(font_dday_count)
            lbl_count.setStyleSheet(f"color: {color_dday_count};")
            lbl_count.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
            
            lbl_detail = QLabel("...")
            lbl_detail.setFont(font_dday_date)
            lbl_detail.setStyleSheet(f"color: {color_dday_date};") 
            lbl_detail.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
            
            right_layout.addWidget(lbl_count)
            right_layout.addWidget(lbl_detail)
            row_layout.addWidget(right_box, alignment=Qt.AlignRight | Qt.AlignVCenter)
            
            self.items_layout.addWidget(row)
            self.labels.append({'date': item['date'], 'count': lbl_count, 'detail': lbl_detail})
            
        # 메인 시계 및 날짜 업데이트
        self.lbl_time.setFont(font_time)
        self.lbl_time.setStyleSheet(f"color: {color_time}; background: transparent;")
        
        self.lbl_date.setFont(font_date)
        self.lbl_date.setStyleSheet(f"color: {color_date}; background: transparent;")
        
        # 달력 데이터 갱신 및 표시 여부 결정
        show_calendar = self.data.get('show_calendar', False)
        if show_calendar:
            dday_dates = [item['date'] for item in self.data['items']]
            self.calendar.set_data(dday_dates, color_date)
            self.calendar.show()
        else:
            self.calendar.hide()
        
        self.lbl_grip.setStyleSheet("color: rgba(255,255,255,0.5); background-color: transparent; font-size: 16px;")
        self.lbl_grip.adjustSize()
        
        self.update()

    def update_counts(self):
        now = datetime.now()
        today = now.date()
        
        # 포맷 설정값 불러오기
        time_format_setting = self.data.get('time_format', '24h')
        day_fmt = self.data.get('day_format', 'kor')
        date_fmt = self.data.get('date_format', 'yyyy-mm-dd')
        
        # 1. 시계 표기 포맷 적용 (12h/24h)
        if time_format_setting == '12h':
            am_pm_str = ("오전" if now.hour < 12 else "오후") if day_fmt == 'kor' else ("AM" if now.hour < 12 else "PM")
            hr = now.hour % 12
            if hr == 0: hr = 12
            
            # AM/PM을 작게 표시하기 위해 HTML 태그 사용 (현재 시간 폰트 크기의 약 25% 크기 적용)
            base_size = self.data.get('size_time', 45)
            small_size = max(10, int(base_size * 0.25))
            self.lbl_time.setText(f"<span style='font-size: {small_size}pt;'>{am_pm_str}</span> {hr:02d}:{now.strftime('%M:%S')}")
        else:
            self.lbl_time.setText(now.strftime("%H:%M:%S"))
            
        # 2. 요일 표기 포맷 적용
        if day_fmt == 'eng':
            days = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]
        else:
            days = ["월", "화", "수", "목", "금", "토", "일"]
        day_str = days[now.weekday()]
        
        # 3. 날짜 표기 포맷 적용
        if date_fmt == 'mm/dd/yyyy':
            date_str = now.strftime('%m/%d/%Y')
        elif date_fmt == 'dd/mm/yyyy':
            date_str = now.strftime('%d/%m/%Y')
        else:
            date_str = now.strftime('%Y-%m-%d')
            
        self.lbl_date.setText(f"{date_str} ({day_str})")
        
        # 4. D-Day 계산 및 업데이트
        for item in self.labels:
            try:
                target_dt = datetime.strptime(item['date'], "%Y-%m-%d")
                target_date = target_dt.date()
                diff_days = (target_date - today).days
                
                color = self.data.get('color_dday_count', '#ff6b6b')
                
                if diff_days > 0:
                    txt = f"D-{diff_days}"; suffix = " 남음" if day_fmt == 'kor' else " left"
                elif diff_days < 0:
                    txt = f"D+{abs(diff_days)}"; suffix = " 지남" if day_fmt == 'kor' else " passed"
                else:
                    txt = "D-Day"; suffix = ""
                
                item['count'].setText(txt)
                item['count'].setStyleSheet(f"color: {color};")
                
                # 유리 메뉴 스타일에 맞춰 날짜와 요일 표시
                day_item_str = days[target_date.weekday()]
                if date_fmt == 'mm/dd/yyyy':
                    t_date_str = target_date.strftime('%m/%d/%Y')
                elif date_fmt == 'dd/mm/yyyy':
                    t_date_str = target_date.strftime('%d/%m/%Y')
                else:
                    t_date_str = target_date.strftime('%Y-%m-%d')
                item['detail'].setText(f"{t_date_str} ({day_item_str})")
                
            except:
                item['count'].setText("Error")

    # 유리판 배경 렌더링
    def paintEvent(self, event):
        if self.data.get('use_glass_background', False):
            painter = QPainter(self)
            painter.setRenderHint(QPainter.Antialiasing)
            path = QPainterPath()
            path.addRoundedRect(0, 0, self.width(), self.height(), 15, 15)
            
            # 투명한 화이트 톤 유리 (SF 공상과학 느낌)
            bg_color = QColor(255, 255, 255, 20)
            painter.fillPath(path, bg_color)
            
            # 외곽 테두리 하이라이트로 빛나는 유리 질감 극대화
            painter.setPen(QColor(255, 255, 255, 60))
            painter.setBrush(Qt.NoBrush)
            painter.drawPath(path)

    # 이벤트 처리
    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self.drag_position = event.globalPosition().toPoint() - self.frameGeometry().topLeft()
            event.accept()

    def mouseMoveEvent(self, event):
        if event.buttons() == Qt.LeftButton and self.drag_position:
            self.move(event.globalPosition().toPoint() - self.drag_position)
            event.accept()

    def mouseDoubleClickEvent(self, event):
        if event.button() == Qt.LeftButton: self.open_settings()

    def contextMenuEvent(self, event):
        menu = QMenu(self)
        menu.setAttribute(Qt.WA_TranslucentBackground)
        menu.setStyleSheet(glass_theme.get_glass_menu_style())
        
        action_edit = menu.addAction("설정 편집")
        action_info = menu.addAction("정보 (About)")
        menu.addSeparator()
        
        # [추가] 마우스 우클릭 메뉴에서 바로 On/Off 할 수 있는 토글 추가
        bg_is_on = self.data.get('use_glass_background', False)
        cal_is_on = self.data.get('show_calendar', False)
        
        action_toggle_bg = menu.addAction("유리 배경 끄기" if bg_is_on else "유리 배경 켜기")
        action_toggle_cal = menu.addAction("투명 달력 끄기" if cal_is_on else "투명 달력 켜기")
        
        menu.addSeparator()
        action_exit = menu.addAction("종료")
        
        action = menu.exec(event.globalPos())
        
        if action == action_edit: 
            self.open_settings()
        elif action == action_info: 
            self.show_info()
        elif action == action_toggle_bg:
            # 상태 반전 후 즉시 화면 갱신 및 저장
            self.data['use_glass_background'] = not bg_is_on
            self.refresh_widgets()
            self.save_current_state()
        elif action == action_toggle_cal:
            # 상태 반전 후 즉시 화면 갱신 및 저장
            self.data['show_calendar'] = not cal_is_on
            self.refresh_widgets()
            self.save_current_state()
        elif action == action_exit: 
            self.quit_application()

    def resizeEvent(self, event):
        if hasattr(self, 'sizegrip'):
            rect = self.rect()
            if hasattr(self, 'lbl_grip'):
                self.lbl_grip.move(rect.right() - self.lbl_grip.width() - 2, rect.bottom() - self.lbl_grip.height() - 2)
            self.sizegrip.move(rect.right() - self.sizegrip.width(), rect.bottom() - self.sizegrip.height())
        super().resizeEvent(event)

    def closeEvent(self, event):
        if hasattr(self, 'tray_icon'): self.tray_icon.hide()
        self.save_current_state()
        event.accept()

    def show_info(self, checked=False):
        dlg = GlassInfoDialog(self)
        dlg.exec()

    def open_settings_for_new_dday(self, date_str):
        """달력에서 우클릭 시 특정 날짜로 새 D-Day를 추가하는 설정창 열기"""
        self.data['x'] = self.x()
        self.data['y'] = self.y()
        self.data['w'] = self.width()
        self.data['h'] = self.height()
        
        dlg = SettingsDialog(self.data, self)
        
        # 설정 창의 탭을 2번(D-Day 관리 탭)으로 자동 전환 후 항목 추가
        if hasattr(dlg, 'tabs'):
            dlg.tabs.setCurrentIndex(2)
        dlg.add_item_row("새 D-Day", date_str)
        
        if dlg.exec():
            self.data = dlg.get_data()
            self.apply_window_settings()
            self.refresh_widgets()
            self.save_current_state()

    def open_settings(self, checked=False):
        # 현재 화면 상태(위치, 크기)를 data에 반영 후 설정 창 오픈
        self.data['x'] = self.x()
        self.data['y'] = self.y()
        self.data['w'] = self.width()
        self.data['h'] = self.height()
        
        dlg = SettingsDialog(self.data, self)
        if dlg.exec():
            # 변경된 데이터 받아오기
            self.data = dlg.get_data()
            self.apply_window_settings()
            self.refresh_widgets()
            self.save_current_state()