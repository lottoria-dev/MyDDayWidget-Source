import sys
from pathlib import Path


def should_relocate_widget(widget_rect, screen_rects):
    """위젯 중심이 현재 사용 가능한 모든 화면 밖에 있는지 반환합니다.

    각 사각형은 (x, y, width, height) 튜플입니다.
    """
    if not screen_rects:
        return False

    x, y, width, height = widget_rect
    center_x = x + width / 2
    center_y = y + height / 2
    return not any(
        (
            screen_x <= center_x < screen_x + screen_width
            and screen_y <= center_y < screen_y + screen_height
        )
        for screen_x, screen_y, screen_width, screen_height in screen_rects
    )

# ---------------------------------------------------------
# 리소스 경로 찾기 함수
# ---------------------------------------------------------
def resource_path(relative_path):
    """개발 실행과 PyInstaller 실행에서 동일한 리소스 경로를 반환합니다."""
    base_path = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
    return str(base_path / relative_path)
