import sys
from PySide6.QtCore import QLockFile, Qt
from PySide6.QtWidgets import QApplication, QMessageBox
from config_manager import ConfigManager
from startup_manager import StartupManager
from ui_main import DDayWidget
import glass_theme

def main():
    # 자동 실행 식별 인수는 Qt에 전달하지 않습니다.
    qt_args = [arg for arg in sys.argv if arg != "--startup"]
    QApplication.setAttribute(Qt.AA_DontUseNativeDialogs, True)
    app = QApplication(qt_args)
    glass_theme.configure_application_theme(app)
    app.setOrganizationName("lottoria-dev")
    app.setApplicationName("MyDDayWidget")

    try:
        config_mgr = ConfigManager()
    except OSError as exc:
        QMessageBox.critical(
            None,
            "My D-Day Widget 실행 오류",
            "사용자 설정 폴더를 준비하지 못했습니다.\n\n{}".format(exc)
        )
        return 1

    try:
        # 두 프로세스가 서로의 설정을 덮어쓰지 않도록 단일 실행을 보장합니다.
        instance_lock = QLockFile(str(config_mgr.lock_file))
        instance_lock.setStaleLockTime(0)
        if not instance_lock.tryLock(100):
            QMessageBox.information(
                None,
                "My D-Day Widget",
                "프로그램이 이미 실행 중입니다."
            )
            return 0
        app.instance_lock = instance_lock

        # 애플리케이션 생성 및 실행
        widget = DDayWidget(config_mgr, StartupManager())
        return app.exec()
    finally:
        config_mgr.close()

if __name__ == '__main__':
    sys.exit(main())
