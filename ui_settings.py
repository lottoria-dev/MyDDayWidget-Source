import copy
from PySide6.QtWidgets import (QDialog, QVBoxLayout, QHBoxLayout, QLabel, 
                             QSlider, QCheckBox, QPushButton, QFrame, 
                             QScrollArea, QWidget, QLineEdit, QDateEdit, 
                             QColorDialog, QFontComboBox, QSizePolicy, QGridLayout)
from PySide6.QtCore import Qt, QDate
from PySide6.QtGui import QFont, QColor, QPainter, QPainterPath

# 테마 모듈 임포트
import glass_theme

# [추가] 색상 선택창에도 글래스 테마를 적용하기 위한 커스텀 클래스
class GlassColorDialog(QColorDialog):
    def __init__(self, color, parent=None):
        super().__init__(color, parent)
        # 네이티브 다이얼로그 비활성화 (필수)
        self.setOption(QColorDialog.DontUseNativeDialog, True)
        
        # 투명 배경 설정
        self.setAttribute(Qt.WA_TranslucentBackground)
        # 글래스 테마 스타일시트 적용
        self.setStyleSheet(glass_theme.get_glass_dialog_style())
        # 폰트 설정
        self.setFont(QFont("Segoe UI"))

    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        
        # 둥근 모서리 배경 그리기 (반지름 15px)
        path = QPainterPath()
        path.addRoundedRect(0, 0, self.width(), self.height(), 15, 15)
        
        painter.fillPath(path, glass_theme.get_glass_background_brush())
        
        # 얇은 테두리 추가 (경계선 명확화)
        painter.setPen(QColor(255, 255, 255, 100))
        painter.drawPath(path)

# [추가] 정보(About) 창을 위한 커스텀 글래스 테마 다이얼로그
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
        info_label.setOpenExternalLinks(True) # 링크 클릭 활성화
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
            <b>■ 버전 정보:</b> v1.0.1<br><br>
            <b>■ 공식 배포 페이지</b><br>
            <a href="https://mathtime.kr/dday.html" style="color: #0078D7; text-decoration: none;">https://mathtime.kr/dday.html</a><br><br>
            <b>■ 개발자 정보</b><br>
            - 개발일: 2026.01.21<br>
            - ✉: trsketch@gmail.com<br>
            Copyright 2026 lottoria-dev. All rights reserved.<br><br>
            <span style='color: #d9534f;'><b>정식 배포 페이지를 제외한 곳에서<br>
            임의의 수정 및 재배포를 금지합니다.</b></span>
        </div>
        """
        info_label.setText(info_html)
        layout.addWidget(info_label)
        layout.addStretch()


# -------------------------------------------------------------------------
# [추가] 역삼각형 아이콘 이미지 로딩 문제를 회피하기 위해 텍스트 "▼"를 직접 그려주는 클래스
# -------------------------------------------------------------------------
class GlassFontComboBox(QFontComboBox):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setStyleSheet("""
            QComboBox { padding-right: 20px; } /* 텍스트가 화살표를 침범하지 않게 여백 */
            QComboBox::down-arrow { image: none; } /* 깨진 이미지/기본 화살표 제거 */
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
        # 콤보박스가 다 그려진 후, 우측에 텍스트로 '▼'를 덮어 그립니다.
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        painter.setPen(QColor("#555555"))
        f = self.font()
        f.setPointSize(8) # 크기를 적당히 줄여 자연스럽게 만듦
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
        # 날짜입력창이 다 그려진 후, 우측에 텍스트로 '▼'를 덮어 그립니다.
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        painter.setPen(QColor("#555555"))
        f = self.font()
        f.setPointSize(8)
        painter.setFont(f)
        painter.drawText(self.rect().adjusted(0, 0, -8, 0), Qt.AlignRight | Qt.AlignVCenter, "▼")
# -------------------------------------------------------------------------


class SettingsDialog(QDialog):
    def __init__(self, data, parent=None):
        super().__init__(parent)
        self.data = copy.deepcopy(data)
        
        self.drag_position = None
        
        # [테마 적용] 프레임 제거로 완벽한 Glass 구현
        self.setWindowFlags(Qt.FramelessWindowHint | Qt.Dialog)
        
        # 기본 글꼴을 Segoe UI로 설정
        self.setFont(QFont("Segoe UI"))
        
        # 부모에게 아이콘이 있다면 설정
        if parent and hasattr(parent, 'app_icon'):
            self.setWindowIcon(parent.app_icon)
            
        self.resize(450, 600)
        
        # [테마 적용] 투명 배경 속성 설정
        self.setAttribute(Qt.WA_TranslucentBackground)
        # [테마 적용] 스타일시트 로드
        self.setStyleSheet(glass_theme.get_glass_dialog_style())

        self.init_ui()

    # [테마 적용] 배경 그리기 이벤트 오버라이드 (둥근 모서리 적용)
    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        
        # 둥근 모서리 배경 그리기 (반지름 20px - 이미지처럼 부드럽게)
        rect_path = QPainterPath()
        rect_path.addRoundedRect(0, 0, self.width(), self.height(), 20, 20)
        
        # 배경 채우기
        painter.fillPath(rect_path, glass_theme.get_glass_background_brush())
        
        # 흰색 반투명 테두리 그리기 (유리 느낌 강조)
        painter.setPen(QColor(255, 255, 255, 150))
        painter.setBrush(Qt.NoBrush)
        painter.drawPath(rect_path)

    # 창 이동(드래그)을 위한 이벤트 처리 추가
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
        # 둥근 모서리 안쪽에 내용이 잘리지 않도록 마진 설정
        layout.setContentsMargins(25, 25, 25, 25)
        self.setLayout(layout)
        
        # 타이틀 및 닫기 버튼 영역 (커스텀 타이틀바)
        header_layout = QHBoxLayout()
        lbl_header = QLabel("D-Day Settings")
        lbl_header.setStyleSheet("font-size: 18pt; font-weight: bold; color: #101010;")
        header_layout.addWidget(lbl_header)
        
        header_layout.addStretch()
        
        # 닫기 버튼
        btn_close = QPushButton("✕")
        btn_close.setFixedSize(30, 30)
        btn_close.setStyleSheet("""
            QPushButton { 
                background-color: transparent; 
                color: #505050; 
                border: none; 
                font-size: 14pt; 
            }
            QPushButton:hover { color: #d32f2f; }
        """)
        btn_close.clicked.connect(self.reject)
        header_layout.addWidget(btn_close)
        
        layout.addLayout(header_layout)
        layout.addSpacing(10)
        
        layout.addWidget(QLabel("<b>[화면 설정]</b>"))
        
        # [수정] 화면 설정 영역의 요소들을 QGridLayout으로 묶어 모두 일직선으로 정렬되게 구성
        grid_settings = QGridLayout()
        grid_settings.setContentsMargins(0, 5, 0, 5)
        grid_settings.setSpacing(10)
        
        # 투명도
        grid_settings.addWidget(QLabel("창 투명도 (전체):"), 0, 0)
        self.slider_alpha = QSlider(Qt.Horizontal)
        self.slider_alpha.setRange(20, 100)
        self.slider_alpha.setValue(int(self.data['alpha'] * 100))
        if self.parent():
            self.slider_alpha.valueChanged.connect(lambda v: self.parent().setWindowOpacity(v/100))
        grid_settings.addWidget(self.slider_alpha, 0, 1)

        # 시간 글자 크기
        grid_settings.addWidget(QLabel("시간 크기:"), 1, 0)
        self.slider_time = QSlider(Qt.Horizontal)
        self.slider_time.setRange(20, 100)
        self.slider_time.setValue(self.data['time_size'])
        grid_settings.addWidget(self.slider_time, 1, 1)

        # 날짜 글자 크기
        grid_settings.addWidget(QLabel("날짜 크기:"), 2, 0)
        self.slider_date = QSlider(Qt.Horizontal)
        self.slider_date.setRange(8, 50)
        self.slider_date.setValue(self.data['date_size'])
        grid_settings.addWidget(self.slider_date, 2, 1)
        
        # 글꼴 선택
        grid_settings.addWidget(QLabel("글꼴:"), 3, 0)
        self.combo_font = GlassFontComboBox()
        self.combo_font.setCurrentFont(QFont(self.data['font_family']))
        self.combo_font.setSizePolicy(QSizePolicy.Expanding, QSizePolicy.Fixed)
        grid_settings.addWidget(self.combo_font, 3, 1)

        # 정렬된 그리드 레이아웃 추가
        layout.addLayout(grid_settings)

        self.chk_top = QCheckBox("항상 위에 고정")
        self.chk_top.setChecked(self.data['topmost'])
        layout.addWidget(self.chk_top)
        
        # 색상 변경 버튼
        h_color_layout = QHBoxLayout()
        btn_text_color = QPushButton("글자 색상")
        btn_text_color.clicked.connect(self.pick_text_color)
        h_color_layout.addWidget(btn_text_color)
        
        btn_count_color = QPushButton("숫자 색상")
        btn_count_color.clicked.connect(self.pick_count_color)
        h_color_layout.addWidget(btn_count_color)
        layout.addLayout(h_color_layout)
        
        # 구분선
        line = QFrame()
        line.setFrameShape(QFrame.HLine)
        line.setStyleSheet("background-color: rgba(0,0,0,0.1); max-height: 1px;")
        layout.addWidget(line)
        
        layout.addWidget(QLabel("<b>[아이템 관리]</b>"))

        # 정렬 버튼
        h_sort_layout = QHBoxLayout()
        btn_sort_near = QPushButton("가까운 날짜 순 (▲)")
        btn_sort_near.clicked.connect(lambda: self.sort_items(reverse=False))
        btn_sort_far = QPushButton("먼 날짜 순 (▼)")
        btn_sort_far.clicked.connect(lambda: self.sort_items(reverse=True))
        h_sort_layout.addWidget(btn_sort_near)
        h_sort_layout.addWidget(btn_sort_far)
        layout.addLayout(h_sort_layout)
        
        # 스크롤 영역
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
            QPushButton { 
                background-color: #f0f8ff; 
                color: #0078d7; 
                border: 1px solid #0078d7; 
                font-weight: bold;
            }
            QPushButton:hover { background-color: #e6f2ff; }
        """)
        btn_add.clicked.connect(lambda checked: self.add_item_row("D-Day", QDate.currentDate().toString("yyyy-MM-dd")))
        layout.addWidget(btn_add)
        
        # 저장 버튼
        btn_save = QPushButton("저장 및 적용")
        btn_save.clicked.connect(self.accept)
        btn_save.setStyleSheet("""
            QPushButton { 
                background-color: #0078d7; 
                color: white; 
                border: none; 
                font-weight: bold; 
                padding: 10px;
            }
            QPushButton:hover { background-color: #0063b1; }
        """)
        layout.addWidget(btn_save)

    def add_item_row(self, title, date):
        row = QWidget()
        row_layout = QHBoxLayout(row)
        row_layout.setContentsMargins(0, 5, 0, 5)
        
        edt_title = QLineEdit(title)
        
        # 직접 텍스트를 그리는 커스텀 클래스로 교체
        edt_date = GlassDateEdit()
        edt_date.setCalendarPopup(True)
        edt_date.setDisplayFormat("yyyy-MM-dd")
        
        # 다크모드에서도 일관된 라이트 테마 캘린더 스타일 적용
        edt_date.calendarWidget().setStyleSheet(glass_theme.get_calendar_style())
        
        qdate = QDate.fromString(date, "yyyy-MM-dd")
        if qdate.isValid():
            edt_date.setDate(qdate)
        else:
            edt_date.setDate(QDate.currentDate())
        
        arrow_style = """
            QPushButton { 
                color: #555555; 
                background-color: transparent;
                border: 1px solid #e0e0e0; 
                border-radius: 4px;
                padding: 0px; 
            }
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
            QPushButton { 
                color: #888888; 
                font-size: 15px; 
                font-weight: bold;
                border: 1px solid #e0e0e0; 
                background-color: white; 
                border-radius: 4px; 
            }
            QPushButton:hover { 
                color: #d32f2f; 
                background-color: #ffebee; 
                border: 1px solid #ef9a9a; 
            }
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

    def pick_text_color(self):
        self._pick_color('text_color', "글자 색상 선택")

    def pick_count_color(self):
        self._pick_color('count_color', "D-Day 숫자 색상 선택")

    def _pick_color(self, key, title):
        """글래스 테마가 적용된 색상 대화상자 호출"""
        current_color = QColor(self.data[key])
        dlg = GlassColorDialog(current_color, self)
        dlg.setWindowTitle(title)
        
        if dlg.exec():
            c = dlg.selectedColor()
            if c.isValid():
                self.data[key] = c.name()

    def get_data(self):
        self.data['alpha'] = self.slider_alpha.value() / 100.0
        self.data['topmost'] = self.chk_top.isChecked()
        self.data['time_size'] = self.slider_time.value()
        self.data['date_size'] = self.slider_date.value()
        self.data['font_family'] = self.combo_font.currentFont().family()
        
        new_items = []
        for row, t, d in self.entries:
            new_items.append({'title': t.text(), 'date': d.date().toString("yyyy-MM-dd")})
        self.data['items'] = new_items
        return self.data