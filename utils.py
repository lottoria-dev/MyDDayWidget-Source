import sys
import os
import calendar

# ---------------------------------------------------------
# 리소스 경로 찾기 함수
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
# 날짜 계산 로직
# ---------------------------------------------------------
def calculate_ymd_diff(start_date, end_date):
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