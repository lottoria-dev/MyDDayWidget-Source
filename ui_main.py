import os
from datetime import datetime
from PySide6.QtWidgets import (QApplication, QWidget, QLabel, QVBoxLayout, 
                             QHBoxLayout, QMenu, QFrame, QSizeGrip,
                             QSizePolicy, QSystemTrayIcon, QMessageBox)
from PySide6.QtCore import Qt, QTimer
from PySide6.QtGui import QFont, QIcon, QAction

# 사용자 정의 모듈 임포트
from utils import resource_path, calculate_ymd_diff
from config_manager import ConfigManager
from ui_settings import SettingsDialog, GlassInfoDialog
import glass_theme

ICON_FILE_NAME = 'icon.png'

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
        # [테마 적용] 트레이 메뉴 스타일 설정
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
        """현재 위치/크기 정보와 함께 설정을 저장"""
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
        self.layout.setContentsMargins(20, 10, 20, 20) 
        self.layout.setSpacing(0) 
        self.layout.setAlignment(Qt.AlignTop) 
        self.setLayout(self.layout)
        
        # 시간 및 날짜
        self.lbl_time = QLabel("00:00:00")
        self.lbl_time.setAlignment(Qt.AlignCenter)
        self.lbl_time.setSizePolicy(QSizePolicy.Preferred, QSizePolicy.Fixed) 
        self.layout.addWidget(self.lbl_time)
        
        self.lbl_date = QLabel("0000-00-00 (월)")
        self.lbl_date.setAlignment(Qt.AlignCenter)
        self.lbl_date.setSizePolicy(QSizePolicy.Preferred, QSizePolicy.Fixed) 
        self.layout.addWidget(self.lbl_date)
        
        # 구분선
        self.line = QFrame()
        self.line.setFrameShape(QFrame.HLine)
        self.line.setFrameShadow(QFrame.Plain)
        self.layout.addWidget(self.line)
        
        # D-Day 아이템 컨테이너
        self.items_layout = QVBoxLayout()
        self.items_layout.setSpacing(2) 
        self.layout.addLayout(self.items_layout)
        
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
            
        font_color = self.data['text_color']
        font_family = self.data['font_family']
        self.labels = []
        
        font_title = QFont(font_family, 12, QFont.Bold)
        font_count = QFont(font_family, 15, QFont.Bold)
        font_detail = QFont(font_family, 9)
        
        for item in self.data['items']:
            row = QWidget()
            row_layout = QHBoxLayout(row)
            row_layout.setContentsMargins(0, 5, 0, 5)
            
            lbl_title = QLabel(item['title'])
            lbl_title.setFont(font_title)
            lbl_title.setStyleSheet(f"color: {font_color};")
            row_layout.addWidget(lbl_title, alignment=Qt.AlignTop)
            
            right_box = QWidget()
            right_layout = QVBoxLayout(right_box)
            right_layout.setContentsMargins(0, 0, 0, 0)
            right_layout.setSpacing(2)
            
            lbl_count = QLabel("D-?")
            lbl_count.setFont(font_count)
            lbl_count.setAlignment(Qt.AlignRight)
            
            lbl_detail = QLabel("...")
            lbl_detail.setFont(font_detail)
            lbl_detail.setStyleSheet(f"color: {font_color};")
            lbl_detail.setAlignment(Qt.AlignRight)
            
            right_layout.addWidget(lbl_count)
            right_layout.addWidget(lbl_detail)
            row_layout.addWidget(right_box)
            
            self.items_layout.addWidget(row)
            self.labels.append({'date': item['date'], 'count': lbl_count, 'detail': lbl_detail})
            
        self.line.setStyleSheet(f"color: {font_color}; background-color: {font_color};")
        self.lbl_time.setStyleSheet(f"color: {font_color}; font-family: '{font_family}'; font-size: {self.data['time_size']}pt; font-weight: bold;")
        self.lbl_date.setStyleSheet(f"color: {font_color}; font-family: '{font_family}'; font-size: {self.data['date_size']}pt; font-weight: bold;")
        self.lbl_grip.setStyleSheet(f"color: {font_color}; background-color: transparent; font-size: 16px;")
        self.lbl_grip.adjustSize()

    def update_counts(self):
        now = datetime.now()
        today = now.date()
        
        self.lbl_time.setText(now.strftime("%H:%M:%S"))
        days = ["월", "화", "수", "목", "금", "토", "일"]
        self.lbl_date.setText(f"{now.strftime('%Y-%m-%d')} ({days[now.weekday()]})")
        
        for item in self.labels:
            try:
                target_dt = datetime.strptime(item['date'], "%Y-%m-%d")
                target_date = target_dt.date()
                diff_days = (target_date - today).days
                
                color = self.data['count_color']
                
                if diff_days > 0:
                    txt = f"D-{diff_days}"; suffix = " 남음"
                elif diff_days < 0:
                    txt = f"D+{abs(diff_days)}"; suffix = " 지남"
                else:
                    txt = "D-Day"; suffix = ""
                
                item['count'].setText(txt)
                item['count'].setStyleSheet(f"color: {color};")
                
                if diff_days == 0:
                    item['detail'].setText("오늘입니다!")
                else:
                    y, m, d = calculate_ymd_diff(today, target_date)
                    parts = []
                    if y > 0: parts.append(f"{y}년")
                    if m > 0: parts.append(f"{m}개월")
                    if d > 0: parts.append(f"{d}일")
                    if not parts: parts = ["0일"]
                    item['detail'].setText(f"{' '.join(parts)}{suffix}")
            except:
                item['count'].setText("Error")

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
        
        # [테마 적용] 우클릭 메뉴 스타일 설정
        menu.setAttribute(Qt.WA_TranslucentBackground)
        menu.setStyleSheet(glass_theme.get_glass_menu_style())
        
        action_edit = menu.addAction("설정 편집")
        action_info = menu.addAction("정보 (About)")
        menu.addSeparator()
        action_exit = menu.addAction("종료")
        
        action = menu.exec(event.globalPos())
        if action == action_edit: self.open_settings()
        elif action == action_info: self.show_info()
        elif action == action_exit: self.quit_application()

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