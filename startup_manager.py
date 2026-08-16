import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Optional


RUN_KEY = r"Software\Microsoft\Windows\CurrentVersion\Run"
VALUE_NAME = "MyDDayWidget"


@dataclass
class StartupResult:
    success: bool
    changed: bool = False
    message: str = ""


class StartupManager:
    """포터블 EXE의 Windows 로그인 자동 실행 등록을 관리합니다."""

    def __init__(self, platform_name=None, frozen=None, executable=None,
                 registry=None):
        self.platform_name = platform_name if platform_name is not None else sys.platform
        self.frozen = bool(getattr(sys, "frozen", False) if frozen is None else frozen)
        self.executable = (
            str(executable)
            if executable is not None
            else str(Path(sys.executable).resolve())
        )
        self._expected_command = subprocess.list2cmdline([
            self.executable, "--startup"
        ])
        self.registry = registry
        if self.registry is None and self.platform_name == "win32":
            try:
                import winreg
                self.registry = winreg
            except ImportError:
                self.registry = None

    @property
    def is_supported(self):
        # 개발용 python main.py 경로는 등록하지 않고 PyInstaller EXE만 허용합니다.
        return self.platform_name == "win32" and self.frozen and self.registry is not None

    @property
    def expected_command(self):
        return self._expected_command

    def read_registered_command(self) -> Optional[str]:
        if not self.is_supported:
            return None
        try:
            with self.registry.OpenKey(
                self.registry.HKEY_CURRENT_USER,
                RUN_KEY,
                0,
                self.registry.KEY_READ,
            ) as key:
                value, _ = self.registry.QueryValueEx(key, VALUE_NAME)
                return str(value)
        except FileNotFoundError:
            return None

    def is_registered_current(self):
        registered = self.read_registered_command()
        return bool(
            registered
            and registered.strip().casefold() == self.expected_command.strip().casefold()
        )

    def set_enabled(self, enabled):
        if not self.is_supported:
            return StartupResult(
                False,
                message="Windows용 배포 EXE에서만 시작 프로그램을 설정할 수 있습니다.",
            )
        try:
            if enabled:
                if self.is_registered_current():
                    return StartupResult(True, False, "시작 프로그램 경로가 정상입니다.")
                with self.registry.CreateKeyEx(
                    self.registry.HKEY_CURRENT_USER,
                    RUN_KEY,
                    0,
                    self.registry.KEY_SET_VALUE,
                ) as key:
                    self.registry.SetValueEx(
                        key,
                        VALUE_NAME,
                        0,
                        self.registry.REG_SZ,
                        self.expected_command,
                    )
                return StartupResult(True, True, "시작 프로그램을 현재 위치로 등록했습니다.")

            current = self.read_registered_command()
            if current is None:
                return StartupResult(True, False, "시작 프로그램이 등록되어 있지 않습니다.")
            with self.registry.OpenKey(
                self.registry.HKEY_CURRENT_USER,
                RUN_KEY,
                0,
                self.registry.KEY_SET_VALUE,
            ) as key:
                self.registry.DeleteValue(key, VALUE_NAME)
            return StartupResult(True, True, "시작 프로그램 등록을 해제했습니다.")
        except OSError as exc:
            return StartupResult(False, False, "시작 프로그램 설정에 실패했습니다: {}".format(exc))

    def sync(self, enabled):
        """실행 때 등록 상태를 설정값과 맞추며 이동된 EXE 경로도 복구합니다."""
        if not self.is_supported:
            return StartupResult(True, False)
        return self.set_enabled(bool(enabled))
