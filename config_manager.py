import configparser
import os
import copy
from datetime import datetime

# 기존 'dday_config_pyside.ini'에서 'dday_config.ini'로 파일명 변경
CONFIG_FILE = 'dday_config.ini'

# 기본 설정값 정의
DEFAULT_DATA = {
    'x': 100, 'y': 100, 'w': 350, 'h': 250, 
    'items': [{"title": "D-Day", "date": datetime.now().strftime("%Y-%m-%d")}],
    'alpha': 0.9,
    'topmost': False,
    'text_color': '#ffffff',
    'count_color': '#ff6b6b',
    'time_size': 45, 
    'date_size': 12,
    'font_family': 'Malgun Gothic'
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
                    data['text_color'] = config.get('Window', 'text_color', fallback=DEFAULT_DATA['text_color'])
                    data['count_color'] = config.get('Window', 'count_color', fallback=DEFAULT_DATA['count_color'])
                    data['time_size'] = config.getint('Window', 'time_size', fallback=DEFAULT_DATA['time_size'])
                    data['date_size'] = config.getint('Window', 'date_size', fallback=DEFAULT_DATA['date_size'])
                    data['font_family'] = config.get('Window', 'font_family', fallback=DEFAULT_DATA['font_family'])
                
                sections = [s for s in config.sections() if s.startswith('DDay-')]
                sections.sort(key=lambda x: int(x.split('-')[1]))
                for s in sections:
                    data['items'].append({
                        'title': config.get(s, 'title'),
                        'date': config.get(s, 'date')
                    })
            except Exception as e:
                print(f"Error loading settings: {e}")
            
        # 저장된 아이템이 없으면 기본값 사용
        if not data['items']:
            data['items'] = copy.deepcopy(DEFAULT_DATA['items'])
            
        return data

    def save_settings(self, data, geometry=None):
        """데이터를 설정 파일에 저장합니다.
        geometry: (x, y, w, h) 튜플 혹은 None. 제공되면 data를 업데이트함.
        """
        if geometry:
            data['x'], data['y'], data['w'], data['h'] = geometry

        config = configparser.ConfigParser()
        config['Window'] = {
            'x': str(data['x']), 'y': str(data['y']),
            'w': str(data['w']), 'h': str(data['h']),
            'alpha': str(data['alpha']),
            'topmost': str(data['topmost']),
            'text_color': data['text_color'],
            'count_color': data['count_color'],
            'time_size': str(data['time_size']),
            'date_size': str(data['date_size']),
            'font_family': data['font_family']
        }
        
        for i, item in enumerate(data['items']):
            config[f'DDay-{i+1}'] = item
            
        with open(self.config_file, 'w', encoding='utf-8') as f:
            config.write(f)