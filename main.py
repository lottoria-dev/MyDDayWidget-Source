import sys
from PySide6.QtWidgets import QApplication
from ui_main import DDayWidget

def main():
    app = QApplication(sys.argv)
    
    # 애플리케이션 생성 및 실행
    widget = DDayWidget()
    
    sys.exit(app.exec())

if __name__ == '__main__':
    main()