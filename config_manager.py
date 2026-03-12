import configparser
import os
import copy
from datetime import datetime

# 기존 'dday_config_pyside.ini'에서 'dday_config.ini'로 파일명 변경
CONFIG_FILE = 'dday_config.ini'

# 기본 설정값 정의 (버전 3.0: 5가지 요소 세분화 및 탭 스타일)
DEFAULT_DATA = {
    'x': 100, 'y': 100, 'w': 350, 'h': 250, 
    'items': [{"title": "D-Day", "date": datetime.now().strftime("%Y-%m-%d")}],
    'alpha': 0.9,
    'topmost': False,
    'use_glass_background': False,
    'time_format': '24h',
    'date_format': 'yyyy-mm-dd',
    'day_format': 'kor',
    
    # [디테일 설정] 색상
    'color_time': '#ffffff',
    'color_date': '#ffffff',
    'color_dday_title': '#ffffff',
    'color_dday_count': '#ff6b6b',
    'color_dday_date': '#aaaaaa',
    
    # [디테일 설정] 글꼴
    'font_time': 'Segoe UI',
    'font_date': 'Segoe UI',
    'font_dday_title': 'Segoe UI',
    'font_dday_count': 'Segoe UI',
    'font_dday_date': 'Segoe UI',
    
    # [디테일 설정] 크기
    'size_time': 45,
    'size_date': 12,
    'size_dday_title': 12,
    'size_dday_count': 15,
    'size_dday_date': 8,
}

class ConfigManager:
    def __init__(self):
        self.config_file = CONFIG_FILE

    def load_settings(self):
        """설정 파일에서 데이터를 읽어옵니다."""
        config = configparser.ConfigParser()
        data = copy.deepcopy(DEFAULT_DATA)
        data['items'] = [] # 아이템은 파일에서 다시 채우기 위해 초기화
        
        if os.path.exists(self.config_file):
            try:
                config.read(self.config_file, encoding='utf-8')
                if 'Window' in config:
                    data['x'] = config.getint('Window', 'x', fallback=DEFAULT_DATA['x'])
                    data['y'] = config.getint('Window', 'y', fallback=DEFAULT_DATA['y'])
                    data['w'] = config.getint('Window', 'w', fallback=DEFAULT_DATA['w'])
                    data['h'] = config.getint('Window', 'h', fallback=DEFAULT_DATA['h'])
                    data['alpha'] = config.getfloat('Window', 'alpha', fallback=DEFAULT_DATA['alpha'])
                    data['topmost'] = config.getboolean('Window', 'topmost', fallback=DEFAULT_DATA['topmost'])
                    data['use_glass_background'] = config.getboolean('Window', 'use_glass_background', fallback=DEFAULT_DATA['use_glass_background'])
                    
                    data['time_format'] = config.get('Window', 'time_format', fallback=DEFAULT_DATA['time_format'])
                    data['date_format'] = config.get('Window', 'date_format', fallback=DEFAULT_DATA['date_format'])
                    data['day_format'] = config.get('Window', 'day_format', fallback=DEFAULT_DATA['day_format'])

                    # 기존의 단일 글꼴/크기 설정을 불러오는 하위 호환성 유지 로직
                    old_text_color = config.get('Window', 'text_color', fallback='#ffffff')
                    old_count_color = config.get('Window', 'count_color', fallback='#ff6b6b')
                    old_font = config.get('Window', 'font_family', fallback=None)
                    old_time_font = config.get('Window', 'time_font_family', fallback=old_font if old_font else 'Segoe UI')
                    old_dday_font = config.get('Window', 'dday_font_family', fallback=old_font if old_font else 'Segoe UI')
                    old_time_size = config.getint('Window', 'time_size', fallback=45)
                    old_date_size = config.getint('Window', 'date_size', fallback=12)

                    # [디테일 설정] 색상
                    data['color_time'] = config.get('Window', 'color_time', fallback=old_text_color)
                    data['color_date'] = config.get('Window', 'color_date', fallback=old_text_color)
                    data['color_dday_title'] = config.get('Window', 'color_dday_title', fallback=old_text_color)
                    data['color_dday_count'] = config.get('Window', 'color_dday_count', fallback=old_count_color)
                    data['color_dday_date'] = config.get('Window', 'color_dday_date', fallback=old_text_color)
                    
                    # [디테일 설정] 글꼴
                    data['font_time'] = config.get('Window', 'font_time', fallback=old_time_font)
                    data['font_date'] = config.get('Window', 'font_date', fallback=old_time_font)
                    data['font_dday_title'] = config.get('Window', 'font_dday_title', fallback=old_dday_font)
                    data['font_dday_count'] = config.get('Window', 'font_dday_count', fallback=old_dday_font)
                    data['font_dday_date'] = config.get('Window', 'font_dday_date', fallback=old_dday_font)

                    # [디테일 설정] 크기
                    data['size_time'] = config.getint('Window', 'size_time', fallback=old_time_size)
                    data['size_date'] = config.getint('Window', 'size_date', fallback=old_date_size)
                    data['size_dday_title'] = config.getint('Window', 'size_dday_title', fallback=12)
                    data['size_dday_count'] = config.getint('Window', 'size_dday_count', fallback=15)
                    data['size_dday_date'] = config.getint('Window', 'size_dday_date', fallback=8)
                
                sections = [s for s in config.sections() if s.startswith('DDay-')]
                sections.sort(key=lambda x: int(x.split('-')[1]))
                for s in sections:
                    data['items'].append({
                        'title': config.get(s, 'title'),
                        'date': config.get(s, 'date')
                    })
            except Exception as e:
                print(f"Error loading settings: {e}")
            
        if not data['items']:
            data['items'] = copy.deepcopy(DEFAULT_DATA['items'])
            
        return data

    def save_settings(self, data, geometry=None):
        if geometry:
            data['x'], data['y'], data['w'], data['h'] = geometry

        config = configparser.ConfigParser()
        config['Window'] = {
            'x': str(data['x']), 'y': str(data['y']),
            'w': str(data['w']), 'h': str(data['h']),
            'alpha': str(data['alpha']),
            'topmost': str(data['topmost']),
            'use_glass_background': str(data['use_glass_background']),
            'time_format': data['time_format'],
            'date_format': data['date_format'],
            'day_format': data['day_format'],
            
            # [디테일 설정] 색상
            'color_time': data['color_time'],
            'color_date': data['color_date'],
            'color_dday_title': data['color_dday_title'],
            'color_dday_count': data['color_dday_count'],
            'color_dday_date': data['color_dday_date'],
            
            # [디테일 설정] 글꼴
            'font_time': data['font_time'],
            'font_date': data['font_date'],
            'font_dday_title': data['font_dday_title'],
            'font_dday_count': data['font_dday_count'],
            'font_dday_date': data['font_dday_date'],
            
            # [디테일 설정] 크기
            'size_time': str(data['size_time']),
            'size_date': str(data['size_date']),
            'size_dday_title': str(data['size_dday_title']),
            'size_dday_count': str(data['size_dday_count']),
            'size_dday_date': str(data['size_dday_date'])
        }
        
        for i, item in enumerate(data['items']):
            config[f'DDay-{i+1}'] = item
            
        with open(self.config_file, 'w', encoding='utf-8') as f:
            config.write(f)