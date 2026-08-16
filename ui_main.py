import os
import sys
import calendar
import webbrowser
import subprocess
from datetime import date, datetime, timedelta
from PySide6.QtWidgets import (QApplication, QWidget, QLabel, QVBoxLayout,
                             QHBoxLayout, QMenu, QFrame, QSizeGrip,
                             QSizePolicy, QSystemTrayIcon, QMessageBox, QPushButton, QGridLayout)
from PySide6.QtCore import Qt, QTimer, Signal, QRect
from PySide6.QtGui import QFont, QIcon, QAction, QPainter, QPainterPath, QColor, QPen, QCursor

# 사용자 정의 모듈 임포트
from utils import resource_path, should_relocate_widget
from config_manager import ConfigManager
from startup_manager import StartupManager
from ui_settings import SettingsDialog, GlassInfoDialog
import glass_theme

ICON_FILE_NAME = 'icon.png'
KOREAN_WEEKDAYS = ("월", "화", "수", "목", "금", "토", "일")
ENGLISH_WEEKDAYS = ("Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun")

WIDGET_PANEL_STYLE = """
    QWidget#clockContainer, QWidget#ddayRow {
        background-color: rgba(255, 255, 255, 0.05);
        border: 1px solid rgba(255, 255, 255, 0.15);
    }
    QWidget#clockContainer {
        border-radius: 15px;
    }
    QWidget#ddayRow {
        border-radius: 12px;
    }
    QWidget#clockContainer:hover, QWidget#ddayRow:hover {
        background-color: rgba(255, 255, 255, 0.05);
        border-color: rgba(255, 255, 255, 0.6);
    }
    QFrame#widgetDivider {
        border-top: 1px solid rgba(255, 255, 255, 0.1);
        border-bottom: 1px solid rgba(255, 255, 255, 0.3);
        background: transparent;
        max-height: 2px;
    }
"""


def _format_date(value, date_format):
    if date_format == 'mm/dd/yyyy':
        return value.strftime('%m/%d/%Y')
    if date_format == 'dd/mm/yyyy':
        return value.strftime('%d/%m/%Y')
    return value.strftime('%Y-%m-%d')


def _screen_rects(screens):
    return [
        (rect.x(), rect.y(), rect.width(), rect.height())
        for rect in (screen.availableGeometry() for screen in screens)
    ]


def _centered_geometry(source_rect, screen):
    available = screen.availableGeometry()
    width = min(max(source_rect.width(), 100), available.width())
    height = min(max(source_rect.height(), 100), available.height())
    target = QRect(0, 0, width, height)
    target.moveCenter(available.center())
    return target


def _open_first_windows_target(targets):
    startfile = getattr(os, "startfile", None)
    if startfile is None:
        return False
    for target in targets:
        try:
            startfile(target)
            return True
        except OSError:
            continue
    return False


def _open_macos_app(app_name):
    try:
        subprocess.Popen(['open', '-a', app_name])
        return True
    except OSError:
        return False

class CalendarCell(QLabel):
    rightClicked = Signal(str)

    def __init__(self, parent=None):
        super().__init__(parent)
        self.date_str = None
        self.is_today = False
        self.has_dday = False
        self.setAlignment(Qt.AlignCenter)
        self.setMinimumSize(24, 24)
        self.setCursor(Qt.PointingHandCursor)
        self.setToolTip("우클릭하여 메뉴 열기")

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
        today = datetime.now().date()
        self.display_year = today.year
        self.display_month = today.month
        self.dday_set = set()
        self.text_color = "#ffffff"
        self._calendar = calendar.Calendar(firstweekday=calendar.SUNDAY)

        self.setObjectName("calendarContainer")
        self.setStyleSheet("""
            QWidget#calendarContainer {
                background-color: rgba(255, 255, 255, 0.05);
                border: 1px solid rgba(255, 255, 255, 0.15);
                border-radius: 12px;
            }
            QWidget#calendarContainer:hover {
                background-color: rgba(255, 255, 255, 0.05);
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

        weekdays = ("일", "월", "화", "수", "목", "금", "토")
        self.weekday_labels = []
        for i, wd in enumerate(weekdays):
            lbl = QLabel(wd)
            lbl.setAlignment(Qt.AlignCenter)
            self.grid_layout.addWidget(lbl, 0, i)
            self.weekday_labels.append(lbl)

        self.cells = []
        for row in range(1, 7):
            for col in range(7):
                cell = CalendarCell()
                cell.rightClicked.connect(self.dateRightClicked.emit)
                self.grid_layout.addWidget(cell, row, col)
                self.cells.append(cell)

        self.layout.addLayout(self.grid_layout)

    def set_style_and_data(self, dday_dates, font_family, font_size, text_color):
        """달력의 폰트, 크기, 색상, 데이터를 모두 적용"""
        self.dday_set = set(dday_dates)
        self.text_color = text_color

        # 헤더 폰트 설정 (기본 크기보다 조금 더 크게)
        header_font = QFont(font_family, font_size + 1, QFont.Bold)
        self.lbl_month.setFont(header_font)
        self.lbl_month.setStyleSheet(f"color: {text_color}; background: transparent;")

        for btn in (self.btn_prev, self.btn_next):
            btn.setStyleSheet(f"background: transparent; border: none; color: {text_color}; font-size: {font_size + 2}px;")

        base_font = QFont(font_family, font_size)
        bold_font = QFont(font_family, max(8, font_size - 1), QFont.Bold)

        # 요일 라벨 스타일 설정
        for i, lbl in enumerate(self.weekday_labels):
            lbl.setFont(bold_font)
            if i == 0:
                lbl.setStyleSheet(f"color: #ff6b6b; background: transparent;")
            elif i == 6:
                lbl.setStyleSheet(f"color: #6baaff; background: transparent;")
            else:
                lbl.setStyleSheet(f"color: {text_color}; background: transparent;")

        # 각 셀 폰트 설정
        for cell in self.cells:
            cell.setFont(base_font)

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

        dates = list(self._calendar.itermonthdates(
            self.display_year, self.display_month
        ))

        # 달력 빈 칸이 없도록 42칸(6주) 꽉 채우기
        while len(dates) < 42:
            dates.append(dates[-1] + timedelta(days=1))

        for cell, d in zip(self.cells, dates[:42]):
            date_str = d.strftime("%Y-%m-%d")
            is_today = (d == today)
            has_dday = (date_str in self.dday_set)
            is_current_month = (d.month == self.display_month)

            cell.set_data(str(d.day), date_str, is_today, has_dday, is_current_month, self.text_color)


class DDayWidget(QWidget):
    def __init__(self, config_mgr=None, startup_manager=None):
        super().__init__()

        # 1. 설정 관리자 초기화 및 로드
        self.config_mgr = config_mgr or ConfigManager()
        self.startup_manager = startup_manager or StartupManager()
        self.data = self.config_mgr.load_settings()
        self.config_load_result = self.config_mgr.last_load_result

        self.drag_position = None
        self._quitting = False
        self._last_date = None
        self._counts_need_refresh = True

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
        QTimer.singleShot(0, self.notify_config_status)
        QTimer.singleShot(0, self.sync_startup_registration)

        # 5. 타이머 설정
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.update_counts)
        self.timer.start(1000)

        # 실행 중 모니터가 분리되거나 주 모니터가 바뀌는 상황을 감시합니다.
        self.init_screen_monitoring()

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
        if self.isVisible():
            self.hide()
        else:
            self.show()
            self.activateWindow()

    def quit_application(self, checked=False):
        self._quitting = True
        self.save_current_state(user_initiated=False)
        if hasattr(self, 'tray_icon'): self.tray_icon.hide()
        QApplication.instance().quit()

    def save_current_state(self, user_initiated=False):
        geometry = (self.x(), self.y(), self.width(), self.height())
        result = self.config_mgr.save_settings(
            self.data, geometry, user_initiated=user_initiated
        )
        if not result.success and not result.skipped:
            QMessageBox.warning(self, "설정 저장 실패", result.message)
        return result

    def sync_startup_registration(self, show_error=False):
        result = self.startup_manager.sync(self.data.get('auto_start', False))
        if not result.success:
            if show_error:
                QMessageBox.warning(self, "시작 프로그램 설정", result.message)
            elif hasattr(self, 'tray_icon'):
                self.tray_icon.showMessage(
                    "시작 프로그램 설정 오류",
                    result.message,
                    QSystemTrayIcon.Warning,
                    7000,
                )
        return result

    def notify_config_status(self):
        result = self.config_load_result
        if result.status == 'migrated':
            self.tray_icon.showMessage(
                "설정 파일 이전 완료",
                "기존 INI 설정을 사용자 설정 폴더로 복사했습니다.",
                QSystemTrayIcon.Information, 5000
            )
        elif result.status in ('backup_recovered', 'backup_recovered_readonly'):
            self.tray_icon.showMessage(
                "설정 백업 복구",
                "기본 설정 파일에 문제가 있어 최근 BAK에서 복구했습니다.",
                QSystemTrayIcon.Warning, 8000
            )
        elif result.status == 'partial':
            self.tray_icon.showMessage(
                "설정 일부 복구",
                "잘못된 값은 기본값으로 대체했습니다. 설정 창에서 확인 후 저장해 주세요.",
                QSystemTrayIcon.Warning, 8000
            )
        elif result.status == 'defaults_after_error':
            QMessageBox.warning(
                self, "설정 파일 오류",
                "설정 파일과 백업을 읽을 수 없어 기본값으로 실행합니다.\n"
                "기존 파일은 보호되며 자동으로 덮어쓰지 않습니다."
            )

    def init_screen_monitoring(self):
        """화면 구성 변경 신호를 짧게 모아 한 번만 위치를 검사합니다."""
        self.screen_change_timer = QTimer(self)
        self.screen_change_timer.setSingleShot(True)
        self.screen_change_timer.setInterval(400)
        self.screen_change_timer.timeout.connect(self.ensure_widget_on_available_screen)

        app = QApplication.instance()
        app.screenRemoved.connect(self.schedule_screen_position_check)
        app.screenAdded.connect(self.schedule_screen_position_check)
        app.primaryScreenChanged.connect(self.schedule_screen_position_check)

    def schedule_screen_position_check(self, *args):
        """Windows의 화면 좌표 갱신이 끝난 뒤 검사하도록 타이머를 재시작합니다."""
        self.screen_change_timer.start()

    def ensure_widget_on_available_screen(self):
        """위젯 중심이 모든 화면에서 벗어나면 주 모니터 중앙으로 이동합니다."""
        screens = QApplication.screens()
        if not screens:
            return

        widget_rect = self.frameGeometry()
        if not should_relocate_widget(
            (widget_rect.x(), widget_rect.y(), widget_rect.width(), widget_rect.height()),
            _screen_rects(screens),
        ):
            return

        primary_screen = QApplication.primaryScreen() or screens[0]
        target = _centered_geometry(widget_rect, primary_screen)
        self.setGeometry(target)

        self.data['x'], self.data['y'] = target.x(), target.y()
        self.data['w'], self.data['h'] = target.width(), target.height()
        self.save_current_state(user_initiated=False)

        if hasattr(self, 'tray_icon'):
            self.tray_icon.showMessage(
                "위젯 위치 복구",
                "사용 중이던 화면이 연결 해제되어 위젯을 주 모니터로 옮겼습니다.",
                QSystemTrayIcon.Information, 5000
            )

    def apply_window_settings(self):
        geometry = QRect(
            self.data['x'], self.data['y'], self.data['w'], self.data['h']
        )
        screens = QApplication.screens()
        if screens and should_relocate_widget(
            (geometry.x(), geometry.y(), geometry.width(), geometry.height()),
            _screen_rects(screens),
        ):
            primary_screen = QApplication.primaryScreen() or screens[0]
            geometry = _centered_geometry(geometry, primary_screen)
            self.data['x'], self.data['y'] = geometry.x(), geometry.y()
            self.data['w'], self.data['h'] = geometry.width(), geometry.height()
        self.setGeometry(geometry)
        self.setWindowOpacity(self.data['alpha'])

        current_flags = self.windowFlags()
        if self.data['topmost']:
            self.setWindowFlags(current_flags | Qt.WindowStaysOnTopHint)
        else:
            self.setWindowFlags(current_flags & ~Qt.WindowStaysOnTopHint)
        self.show()

    def init_ui(self):
        self.setStyleSheet(WIDGET_PANEL_STYLE)
        self.layout = QVBoxLayout()
        self.layout.setContentsMargins(20, 20, 20, 20)
        self.layout.setSpacing(12)
        self.layout.setAlignment(Qt.AlignTop)
        self.setLayout(self.layout)

        # 시계와 달력을 담을 디지털 시계 느낌의 둥근 컨테이너
        self.clock_container = QWidget()
        self.clock_container.setObjectName("clockContainer")
        self.clock_layout = QVBoxLayout(self.clock_container)
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
        self.line.setObjectName("widgetDivider")
        self.line.setFrameShape(QFrame.HLine)
        self.layout.addWidget(self.line)

        # D-Day 아이템 컨테이너
        self.items_layout = QVBoxLayout()
        self.items_layout.setSpacing(2)
        self.layout.addLayout(self.items_layout)

        # SF 글래스 테마 투명 달력 위젯
        self.calendar = SFGlassCalendar()
        self.calendar.dateRightClicked.connect(self.show_calendar_context_menu)
        self.layout.addWidget(self.calendar)

        self.layout.addStretch(1)

        # 리사이즈 그립
        self.sizegrip = QSizeGrip(self)
        self.sizegrip.setStyleSheet("background-color: transparent; width: 20px; height: 20px;")

        self.lbl_grip = QLabel("◢", self)
        self.lbl_grip.setAlignment(Qt.AlignBottom | Qt.AlignRight)
        self.lbl_grip.setAttribute(Qt.WA_TransparentForMouseEvents)
        self.lbl_grip.setStyleSheet(
            "color: rgba(255,255,255,0.5); background-color: transparent; "
            "font-size: 16px;"
        )
        self.lbl_grip.adjustSize()

        self.refresh_widgets()

    def refresh_widgets(self):
        while self.items_layout.count():
            child = self.items_layout.takeAt(0)
            widget = child.widget()
            if widget:
                widget.deleteLater()

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

        # 달력 글꼴 옵션
        font_calendar_family = self.data.get('font_calendar', 'Segoe UI')
        size_calendar = self.data.get('size_calendar', 10)
        color_calendar = self.data.get('color_calendar', '#ffffff')

        self.labels = []

        for item in self.data['items']:
            row = QWidget()
            row.setObjectName("ddayRow")
            row_layout = QHBoxLayout(row)
            row_layout.setContentsMargins(8, 2, 8, 4)

            lbl_title = QLabel(item['title'])
            lbl_title.setTextFormat(Qt.PlainText)
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
            try:
                target_date = date.fromisoformat(item['date'])
            except (TypeError, ValueError):
                target_date = None
            self.labels.append({
                'target_date': target_date,
                'count': lbl_count,
                'detail': lbl_detail,
            })

        # 메인 시계 및 날짜 업데이트
        self.lbl_time.setFont(font_time)
        self.lbl_time.setStyleSheet(f"color: {color_time};")

        self.lbl_date.setFont(font_date)
        self.lbl_date.setStyleSheet(f"color: {color_date};")

        # 달력 데이터 갱신 및 표시 여부 결정
        show_calendar = self.data.get('show_calendar', False)
        if show_calendar:
            dday_dates = [item['date'] for item in self.data['items']]
            self.calendar.set_style_and_data(dday_dates, font_calendar_family, size_calendar, color_calendar)
            self.calendar.show()
        else:
            self.calendar.hide()

        self._counts_need_refresh = True
        self.update_counts()
        self.update()

    def update_counts(self):
        now = datetime.now()
        today = now.date()

        previous_date = self._last_date
        date_changed = previous_date != today
        if date_changed:
            if previous_date and (
                self.calendar.display_year == previous_date.year and
                self.calendar.display_month == previous_date.month
            ):
                self.calendar.display_year = today.year
                self.calendar.display_month = today.month
            if self.data.get('show_calendar', False):
                self.calendar.update_calendar()
            self._last_date = today

        time_format_setting = self.data.get('time_format', '24h')
        day_fmt = self.data.get('day_format', 'kor')
        date_fmt = self.data.get('date_format', 'yyyy-mm-dd')

        # 1. 시계 표기 포맷 적용 (12h/24h)
        if time_format_setting == '12h':
            if day_fmt == 'kor':
                am_pm_str = "오전" if now.hour < 12 else "오후"
            else:
                am_pm_str = "AM" if now.hour < 12 else "PM"
            hr = now.hour % 12
            if hr == 0:
                hr = 12

            base_size = self.data.get('size_time', 45)
            small_size = max(10, int(base_size * 0.25))
            self.lbl_time.setText(
                f"<span style='font-size: {small_size}pt;'>{am_pm_str}</span> "
                f"{hr:02d}:{now.strftime('%M:%S')}"
            )
        else:
            self.lbl_time.setText(now.strftime("%H:%M:%S"))

        # 2. 요일 표기 포맷 적용
        days = ENGLISH_WEEKDAYS if day_fmt == 'eng' else KOREAN_WEEKDAYS
        day_str = days[now.weekday()]

        # 3. 날짜 표기 포맷 적용
        self.lbl_date.setText(f"{_format_date(now, date_fmt)} ({day_str})")

        # D-Day 값과 목표 날짜는 날짜·설정이 바뀔 때만 다시 계산합니다.
        if not (date_changed or self._counts_need_refresh):
            return

        # 4. D-Day 계산 및 업데이트
        for item in self.labels:
            target_date = item['target_date']
            if target_date is None:
                item['count'].setText("Error")
                item['detail'].setText("날짜 오류")
                continue

            diff_days = (target_date - today).days
            if diff_days > 0:
                count_text = f"D-{diff_days}"
            elif diff_days < 0:
                count_text = f"D+{abs(diff_days)}"
            else:
                count_text = "D-Day"

            item['count'].setText(count_text)
            target_weekday = days[target_date.weekday()]
            item['detail'].setText(
                f"{_format_date(target_date, date_fmt)} ({target_weekday})"
            )

        self._counts_need_refresh = False

    # 유리판 배경 렌더링
    def paintEvent(self, event):
        if self.data.get('use_glass_background', False):
            painter = QPainter(self)
            painter.setRenderHint(QPainter.Antialiasing)
            path = QPainterPath()
            path.addRoundedRect(0, 0, self.width(), self.height(), 15, 15)

            # 투명한 화이트 톤 유리
            bg_color = QColor(255, 255, 255, 20)
            painter.fillPath(path, bg_color)

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
        if event.button() == Qt.LeftButton:
            self.open_settings()

    def contextMenuEvent(self, event):
        menu = QMenu(self)
        menu.setAttribute(Qt.WA_TranslucentBackground)
        menu.setStyleSheet(glass_theme.get_glass_menu_style())

        action_edit = menu.addAction("설정 편집")
        action_info = menu.addAction("정보 (About)")
        menu.addSeparator()

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
            self.data['use_glass_background'] = not bg_is_on
            self.update()
            self.save_current_state(user_initiated=True)
        elif action == action_toggle_cal:
            self.data['show_calendar'] = not cal_is_on
            self.refresh_widgets()
            self.save_current_state(user_initiated=True)
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
        if hasattr(self, 'tray_icon'):
            self.tray_icon.hide()
        if not self._quitting:
            self.save_current_state(user_initiated=False)
        event.accept()

    def show_info(self, checked=False):
        dlg = GlassInfoDialog(self)
        dlg.exec()

    def show_calendar_context_menu(self, date_str):
        """달력의 특정 날짜를 우클릭했을 때 나타나는 컨텍스트 메뉴"""
        menu = QMenu(self)
        menu.setAttribute(Qt.WA_TranslucentBackground)
        menu.setStyleSheet(glass_theme.get_glass_menu_style())

        action_add_dday = menu.addAction(f"새 D-Day 추가 ({date_str})")
        menu.addSeparator()
        action_google = menu.addAction("구글 캘린더 열기")
        action_apple = menu.addAction("애플 캘린더 열기")
        action_outlook = menu.addAction("Outlook 캘린더 열기")

        action = menu.exec(QCursor.pos())

        if action == action_add_dday:
            self.open_settings_for_new_dday(date_str)

        elif action == action_google:
            # 구글 캘린더의 경우 선택한 날짜가 바로 열리도록 포맷 변환
            y, m, d = date_str.split('-')
            url = f"https://calendar.google.com/calendar/u/0/r/day/{y}/{int(m)}/{int(d)}"
            webbrowser.open(url)

        elif action == action_apple:
            # Mac OS인 경우 로컬 앱 실행 시도, 그 외의 경우 웹 iCloud 캘린더 오픈
            if sys.platform != 'darwin' or not _open_macos_app('Calendar'):
                webbrowser.open("https://www.icloud.com/calendar/")

        elif action == action_outlook:
            if sys.platform == 'win32':
                success = _open_first_windows_target((
                    "ms-outlook:", "olk.exe", "outlookcal:", "outlook:",
                ))
            elif sys.platform == 'darwin':
                success = _open_macos_app('Microsoft Outlook')
            else:
                success = False

            if not success:
                webbrowser.open("https://outlook.live.com/calendar/")

    def _sync_geometry_to_data(self):
        self.data.update({
            'x': self.x(),
            'y': self.y(),
            'w': self.width(),
            'h': self.height(),
        })

    def _run_settings_dialog(self, dialog):
        if not dialog.exec():
            return
        self.data = dialog.get_data()
        self.apply_window_settings()
        self.refresh_widgets()
        save_result = self.save_current_state(user_initiated=True)
        if save_result.success:
            self.sync_startup_registration(show_error=True)

    def open_settings_for_new_dday(self, date_str):
        self._sync_geometry_to_data()

        dlg = SettingsDialog(self.data, self)
        dlg.tabs.setCurrentIndex(0)
        dlg.add_item_row("새 D-Day", date_str)
        self._run_settings_dialog(dlg)

    def open_settings(self, checked=False):
        self._sync_geometry_to_data()
        dlg = SettingsDialog(self.data, self)
        self._run_settings_dialog(dlg)
