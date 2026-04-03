import configparser
import os
import copy
from datetime import datetime

CONFIG_FILE = 'dday_config.ini'

# 달력 설정(font_calendar, size_calendar, color_calendar)을 포함한 기본 데이터
DEFAULT_DATA = {
    'x': 100, 'y': 100, 'w': 350, 'h': 250, 
    'items': [{"title": "D-Day", "date": datetime.now().strftime("%Y-%m-%d")}],
    'alpha': 0.9,
    'topmost': False,
    'use_glass_background': False,
    'show_calendar': False,
    'time_format': '24h',
    'date_format': 'yyyy-mm-dd',
    'day_format': 'kor',
    
    # [디테일 설정] 색상
    'color_time': '#ffffff',
    'color_date': '#ffffff',
    'color_dday_title': '#ffffff',
    'color_dday_count': '#ff6b6b',
    'color_dday_date': '#aaaaaa',
    'color_calendar': '#ffffff',
    
    # [디테일 설정] 글꼴
    'font_time': 'Segoe UI',
    'font_date': 'Segoe UI',
    'font_dday_title': 'Segoe UI',
    'font_dday_count': 'Segoe UI',
    'font_dday_date': 'Segoe UI',
    'font_calendar': 'Segoe UI',
    
    # [디테일 설정] 크기
    'size_time': 45,
    'size_date': 12,
    'size_dday_title': 12,
    'size_dday_count': 15,
    'size_dday_date': 8,
    'size_calendar': 10,
}

class ConfigManager:
    def __init__(self):
        self.config_file = CONFIG_FILE

    def load_settings(self):
        """설정 파일에서 데이터를 읽어옵니다."""
        config = configparser.ConfigParser()
        data = copy.deepcopy(DEFAULT_DATA)
        data['items'] = [] 
        
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
                    data['show_calendar'] = config.getboolean('Window', 'show_calendar', fallback=DEFAULT_DATA['show_calendar'])
                    
                    data['time_format'] = config.get('Window', 'time_format', fallback=DEFAULT_DATA['time_format'])
                    data['date_format'] = config.get('Window', 'date_format', fallback=DEFAULT_DATA['date_format'])
                    data['day_format'] = config.get('Window', 'day_format', fallback=DEFAULT_DATA['day_format'])

                    # 색상
                    data['color_time'] = config.get('Window', 'color_time', fallback=DEFAULT_DATA['color_time'])
                    data['color_date'] = config.get('Window', 'color_date', fallback=DEFAULT_DATA['color_date'])
                    data['color_dday_title'] = config.get('Window', 'color_dday_title', fallback=DEFAULT_DATA['color_dday_title'])
                    data['color_dday_count'] = config.get('Window', 'color_dday_count', fallback=DEFAULT_DATA['color_dday_count'])
                    data['color_dday_date'] = config.get('Window', 'color_dday_date', fallback=DEFAULT_DATA['color_dday_date'])
                    data['color_calendar'] = config.get('Window', 'color_calendar', fallback=DEFAULT_DATA['color_calendar'])
                    
                    # 글꼴
                    data['font_time'] = config.get('Window', 'font_time', fallback=DEFAULT_DATA['font_time'])
                    data['font_date'] = config.get('Window', 'font_date', fallback=DEFAULT_DATA['font_date'])
                    data['font_dday_title'] = config.get('Window', 'font_dday_title', fallback=DEFAULT_DATA['font_dday_title'])
                    data['font_dday_count'] = config.get('Window', 'font_dday_count', fallback=DEFAULT_DATA['font_dday_count'])
                    data['font_dday_date'] = config.get('Window', 'font_dday_date', fallback=DEFAULT_DATA['font_dday_date'])
                    data['font_calendar'] = config.get('Window', 'font_calendar', fallback=DEFAULT_DATA['font_calendar'])

                    # 크기
                    data['size_time'] = config.getint('Window', 'size_time', fallback=DEFAULT_DATA['size_time'])
                    data['size_date'] = config.getint('Window', 'size_date', fallback=DEFAULT_DATA['size_date'])
                    data['size_dday_title'] = config.getint('Window', 'size_dday_title', fallback=DEFAULT_DATA['size_dday_title'])
                    data['size_dday_count'] = config.getint('Window', 'size_dday_count', fallback=DEFAULT_DATA['size_dday_count'])
                    data['size_dday_date'] = config.getint('Window', 'size_dday_date', fallback=DEFAULT_DATA['size_dday_date'])
                    data['size_calendar'] = config.getint('Window', 'size_calendar', fallback=DEFAULT_DATA['size_calendar'])
                
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
            'show_calendar': str(data['show_calendar']),
            'time_format': data['time_format'],
            'date_format': data['date_format'],
            'day_format': data['day_format'],
            
            # 색상
            'color_time': data['color_time'],
            'color_date': data['color_date'],
            'color_dday_title': data['color_dday_title'],
            'color_dday_count': data['color_dday_count'],
            'color_dday_date': data['color_dday_date'],
            'color_calendar': data['color_calendar'],
            
            # 글꼴
            'font_time': data['font_time'],
            'font_date': data['font_date'],
            'font_dday_title': data['font_dday_title'],
            'font_dday_count': data['font_dday_count'],
            'font_dday_date': data['font_dday_date'],
            'font_calendar': data['font_calendar'],
            
            # 크기
            'size_time': str(data['size_time']),
            'size_date': str(data['size_date']),
            'size_dday_title': str(data['size_dday_title']),
            'size_dday_count': str(data['size_dday_count']),
            'size_dday_date': str(data['size_dday_date']),
            'size_calendar': str(data['size_calendar'])
        }
        
        for i, item in enumerate(data['items']):
            config[f'DDay-{i+1}'] = item
            
        with open(self.config_file, 'w', encoding='utf-8') as f:
            config.write(f)