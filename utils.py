import sys
from pathlib import Path

# ---------------------------------------------------------
# 리소스 경로 찾기 함수
# ---------------------------------------------------------
def resource_path(relative_path):
    """개발 실행과 PyInstaller 실행에서 동일한 리소스 경로를 반환합니다."""
    base_path = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
    return str(base_path / relative_path)
