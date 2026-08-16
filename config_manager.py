import configparser
import copy
import io
import logging
import os
import re
import shutil
import sys
import tempfile
from dataclasses import dataclass, field
from datetime import datetime
from logging.handlers import RotatingFileHandler
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional, Tuple


APP_NAME = "MyDDayWidget"
CONFIG_FILE = "dday_config.ini"
BACKUP_COUNT = 3
SCHEMA_VERSION = 1

DEFAULT_DATA = {
    "x": 100, "y": 100, "w": 350, "h": 250,
    "items": [{"title": "D-Day", "date": datetime.now().strftime("%Y-%m-%d")}],
    "alpha": 0.9, "topmost": False, "use_glass_background": False,
    "auto_start": False,
    "show_calendar": False, "time_format": "24h",
    "date_format": "yyyy-mm-dd", "day_format": "kor",
    "color_time": "#ffffff", "color_date": "#ffffff",
    "color_dday_title": "#ffffff", "color_dday_count": "#ff6b6b",
    "color_dday_date": "#aaaaaa", "color_calendar": "#ffffff",
    "font_time": "Segoe UI", "font_date": "Segoe UI",
    "font_dday_title": "Segoe UI", "font_dday_count": "Segoe UI",
    "font_dday_date": "Segoe UI", "font_calendar": "Segoe UI",
    "size_time": 45, "size_date": 12, "size_dday_title": 12,
    "size_dday_count": 15, "size_dday_date": 8, "size_calendar": 10,
}

COLOR_KEYS = (
    "color_time", "color_date", "color_dday_title", "color_dday_count",
    "color_dday_date", "color_calendar",
)
FONT_KEYS = (
    "font_time", "font_date", "font_dday_title", "font_dday_count",
    "font_dday_date", "font_calendar",
)
SIZE_KEYS = (
    "size_time", "size_date", "size_dday_title", "size_dday_count",
    "size_dday_date", "size_calendar",
)
WINDOW_INT_RANGES = {
    "x": (-100000, 100000),
    "y": (-100000, 100000),
    "w": (100, 10000),
    "h": (100, 10000),
    **{key: (5, 150) for key in SIZE_KEYS},
}
BOOLEAN_KEYS = ("topmost", "use_glass_background", "show_calendar", "auto_start")
ENUM_OPTIONS = {
    "time_format": {"12h", "24h"},
    "date_format": {"yyyy-mm-dd", "mm/dd/yyyy", "dd/mm/yyyy"},
    "day_format": {"kor", "eng"},
}
HEX_COLOR_PATTERN = re.compile(r"#[0-9a-fA-F]{6}\Z")


@dataclass
class LoadResult:
    data: Dict[str, Any]
    status: str
    source: Optional[str] = None
    warnings: List[str] = field(default_factory=list)
    error: Optional[str] = None
    preserved_path: Optional[str] = None


@dataclass
class SaveResult:
    success: bool
    message: str
    path: Optional[str] = None
    backup_path: Optional[str] = None
    skipped: bool = False


def _fresh_defaults() -> Dict[str, Any]:
    data = copy.deepcopy(DEFAULT_DATA)
    data["items"] = [{"title": "D-Day", "date": datetime.now().strftime("%Y-%m-%d")}]
    return data


class ConfigManager:
    """INI 설정의 경로, 검증, 백업, 복구와 원자적 저장을 관리합니다."""

    def __init__(self, config_dir: Optional[os.PathLike] = None,
                 legacy_paths: Optional[Iterable[os.PathLike]] = None):
        self.config_dir = Path(config_dir) if config_dir else self._default_config_dir()
        self.config_dir.mkdir(parents=True, exist_ok=True)
        self.config_file = self.config_dir / CONFIG_FILE
        self.backup_files = [
            self.config_dir / "dday_config.bak{}.ini".format(i)
            for i in range(1, BACKUP_COUNT + 1)
        ]
        self.lock_file = self.config_dir / ".instance.lock"
        self.log_file = self.config_dir / "dday_widget.log"
        self.logger = self._create_logger()
        self.safe_to_auto_save = True
        self.last_migration_source = None
        self.last_load_result = LoadResult(_fresh_defaults(), "defaults_new")
        self._legacy_paths = ([Path(p) for p in legacy_paths]
                              if legacy_paths is not None else self._default_legacy_paths())
        self._migrate_legacy_file_if_needed()

    @staticmethod
    def _default_config_dir() -> Path:
        try:
            from PySide6.QtCore import QStandardPaths
            location = QStandardPaths.writableLocation(QStandardPaths.AppConfigLocation)
            if location:
                return Path(location)
        except (ImportError, RuntimeError):
            pass
        if sys.platform == "win32":
            base = os.environ.get("LOCALAPPDATA") or os.environ.get("APPDATA")
            if base:
                return Path(base) / APP_NAME
        if sys.platform == "darwin":
            return Path.home() / "Library" / "Application Support" / APP_NAME
        base = os.environ.get("XDG_CONFIG_HOME")
        return (Path(base) if base else Path.home() / ".config") / APP_NAME

    def _default_legacy_paths(self) -> List[Path]:
        candidates = [Path.cwd() / CONFIG_FILE,
                      Path(__file__).resolve().parent / CONFIG_FILE]
        if getattr(sys, "frozen", False):
            candidates.append(Path(sys.executable).resolve().parent / CONFIG_FILE)
        unique = []
        for candidate in candidates:
            if candidate != self.config_file and candidate not in unique:
                unique.append(candidate)
        return unique

    def _create_logger(self) -> logging.Logger:
        # 임시 ConfigManager별 로거를 전역 logging 레지스트리에 누적하지 않습니다.
        logger = logging.Logger("{}.config".format(APP_NAME))
        logger.setLevel(logging.INFO)
        logger.propagate = False
        handler = RotatingFileHandler(
            self.log_file, maxBytes=512 * 1024, backupCount=2,
            encoding="utf-8", delay=True,
        )
        handler.setFormatter(logging.Formatter("%(asctime)s [%(levelname)s] %(message)s"))
        logger.addHandler(handler)
        self._log_handler = handler
        return logger

    def close(self) -> None:
        handler = getattr(self, "_log_handler", None)
        if handler is None:
            return
        self.logger.removeHandler(handler)
        handler.close()
        self._log_handler = None

    def __del__(self):
        self.close()

    @staticmethod
    def _new_parser() -> configparser.ConfigParser:
        # '%'가 포함된 제목을 안전하게 처리합니다.
        return configparser.ConfigParser(interpolation=None)

    def _migrate_legacy_file_if_needed(self) -> None:
        if self.config_file.exists():
            return
        candidates = [p for p in self._legacy_paths if p.is_file()]
        candidates.sort(key=lambda p: p.stat().st_mtime, reverse=True)
        for candidate in candidates:
            result = self._read_file(candidate)
            if result.status not in {"ok", "partial"}:
                continue
            try:
                self._atomic_copy(candidate, self.config_file)
                self.last_migration_source = str(candidate)
                self.logger.info("기존 설정을 새 위치로 복사했습니다: %s", candidate)
                return
            except OSError as exc:
                self.logger.error("기존 설정 이전 실패: %s", exc)

    def load_settings(self) -> Dict[str, Any]:
        self.last_load_result = self.load_with_status()
        return copy.deepcopy(self.last_load_result.data)

    def load_with_status(self) -> LoadResult:
        primary = self._read_file(self.config_file)
        if primary.status in {"ok", "partial"}:
            if self.last_migration_source:
                primary.warnings.insert(0, "기존 INI 파일을 사용자 설정 폴더로 복사했습니다.")
                if primary.status == "ok":
                    primary.status = "migrated"
            if primary.status == "partial":
                primary.preserved_path = self._preserve_problem_file(self.config_file)
                self.safe_to_auto_save = False
            else:
                self.safe_to_auto_save = True
            return primary

        if primary.status == "missing":
            self.safe_to_auto_save = True
            return LoadResult(_fresh_defaults(), "defaults_new", str(self.config_file))

        preserved = self._preserve_problem_file(self.config_file)
        self.logger.error("기본 설정 읽기 실패: %s", primary.error)
        for backup_path in self.backup_files:
            backup = self._read_file(backup_path)
            if backup.status not in {"ok", "partial"}:
                continue
            warnings = ["{}에서 설정을 복구했습니다.".format(backup_path.name)] + backup.warnings
            try:
                self._atomic_write_text(self.config_file, self._serialize(backup.data))
                self.safe_to_auto_save = True
                return LoadResult(backup.data, "backup_recovered", str(backup_path),
                                  warnings, preserved_path=preserved)
            except (OSError, ValueError, configparser.Error) as exc:
                self.safe_to_auto_save = False
                warnings.append("백업은 읽었지만 기본 INI로 다시 저장하지 못했습니다.")
                return LoadResult(backup.data, "backup_recovered_readonly",
                                  str(backup_path), warnings, str(exc), preserved)

        self.safe_to_auto_save = False
        return LoadResult(
            _fresh_defaults(), "defaults_after_error", str(self.config_file),
            ["설정 파일과 백업을 모두 읽을 수 없어 기본값으로 실행합니다."],
            primary.error, preserved,
        )

    def _read_file(self, path: os.PathLike) -> LoadResult:
        path = Path(path)
        if not path.exists():
            return LoadResult(_fresh_defaults(), "missing", str(path))
        parser = self._new_parser()
        try:
            with path.open("r", encoding="utf-8") as handle:
                parser.read_file(handle)
        except (OSError, UnicodeError, configparser.Error) as exc:
            return LoadResult(_fresh_defaults(), "invalid", str(path), error=str(exc))
        if not parser.sections():
            return LoadResult(_fresh_defaults(), "invalid", str(path),
                              error="설정 섹션이 없습니다.")

        data = _fresh_defaults()
        default_items = data["items"]
        data["items"] = []
        warnings = []
        if "Window" in parser:
            self._read_window(parser, data, warnings)
        else:
            warnings.append("[Window] 섹션이 없어 창 설정은 기본값을 사용합니다.")

        numbered = []
        for section in parser.sections():
            if section.startswith("DDay-"):
                suffix = section.split("-", 1)[1]
                if suffix.isdigit():
                    numbered.append((int(suffix), section))
                else:
                    warnings.append("잘못된 섹션을 건너뜁니다: {}".format(section))
        for _, section in sorted(numbered):
            title = parser.get(section, "title", fallback="").strip()
            date_text = parser.get(section, "date", fallback="").strip()
            if not title:
                warnings.append("제목이 없는 항목을 건너뜁니다: {}".format(section))
            elif not self._valid_date(date_text):
                warnings.append("날짜가 잘못된 항목을 건너뜁니다: {}".format(section))
            else:
                data["items"].append({"title": title, "date": date_text})
        if not data["items"]:
            data["items"] = default_items
            if numbered:
                warnings.append("사용 가능한 D-Day가 없어 기본 항목을 사용합니다.")
        return LoadResult(data, "partial" if warnings else "ok", str(path), warnings)

    def _read_window(self, parser: configparser.ConfigParser,
                     data: Dict[str, Any], warnings: List[str]) -> None:
        for key, (low, high) in WINDOW_INT_RANGES.items():
            if not parser.has_option("Window", key):
                continue
            try:
                value = parser.getint("Window", key)
                if not low <= value <= high:
                    raise ValueError
                data[key] = value
            except (ValueError, configparser.Error):
                warnings.append("{} 값이 잘못되어 기본값을 사용합니다.".format(key))
        if parser.has_option("Window", "alpha"):
            try:
                value = parser.getfloat("Window", "alpha")
                if not 0.2 <= value <= 1.0:
                    raise ValueError
                data["alpha"] = value
            except (ValueError, configparser.Error):
                warnings.append("alpha 값이 잘못되어 기본값을 사용합니다.")
        for key in BOOLEAN_KEYS:
            if parser.has_option("Window", key):
                try:
                    data[key] = parser.getboolean("Window", key)
                except (ValueError, configparser.Error):
                    warnings.append("{} 값이 잘못되어 기본값을 사용합니다.".format(key))
        for key, allowed in ENUM_OPTIONS.items():
            value = parser.get("Window", key, fallback=data[key]).strip()
            if value in allowed:
                data[key] = value
            elif parser.has_option("Window", key):
                warnings.append("{} 값이 잘못되어 기본값을 사용합니다.".format(key))
        for key in COLOR_KEYS:
            value = parser.get("Window", key, fallback=data[key]).strip()
            if HEX_COLOR_PATTERN.fullmatch(value):
                data[key] = value.lower()
            elif parser.has_option("Window", key):
                warnings.append("{} 값이 잘못되어 기본값을 사용합니다.".format(key))
        for key in FONT_KEYS:
            value = parser.get("Window", key, fallback=data[key]).strip()
            if value:
                data[key] = value
            elif parser.has_option("Window", key):
                warnings.append("{} 값이 비어 있어 기본값을 사용합니다.".format(key))

    @staticmethod
    def _valid_date(value: str) -> bool:
        try:
            datetime.strptime(value, "%Y-%m-%d")
            return True
        except (TypeError, ValueError):
            return False

    def save_settings(self, data: Dict[str, Any],
                      geometry: Optional[Tuple[int, int, int, int]] = None,
                      user_initiated: bool = False) -> SaveResult:
        if not self.safe_to_auto_save and not user_initiated:
            return SaveResult(False, "부분 복구 상태이므로 자동 저장을 건너뛰었습니다.",
                              str(self.config_file), skipped=True)
        working = copy.deepcopy(data)
        if geometry:
            working["x"], working["y"], working["w"], working["h"] = geometry
        try:
            normalized = self._normalize(working)
            serialized = self._serialize_normalized(normalized)
            current = self._read_file(self.config_file)
            if current.status == "ok":
                self._rotate_backups()
            elif self.config_file.exists():
                self._preserve_problem_file(self.config_file)
            self._atomic_write_text(self.config_file, serialized)
            verification = self._read_file(self.config_file)
            if verification.status != "ok":
                raise OSError("저장 후 검증에 실패했습니다.")
            data.update(copy.deepcopy(normalized))
            self.safe_to_auto_save = True
            self.last_load_result = verification
            backup = str(self.backup_files[0]) if self.backup_files[0].exists() else None
            return SaveResult(True, "설정을 저장했습니다.", str(self.config_file), backup)
        except (OSError, ValueError, configparser.Error) as exc:
            self.logger.exception("설정 저장 실패")
            return SaveResult(False, "설정을 저장하지 못했습니다: {}".format(exc))

    def _normalize(self, data: Dict[str, Any]) -> Dict[str, Any]:
        result = _fresh_defaults()
        for key in ("x", "y", "w", "h"):
            value = int(data.get(key, result[key]))
            low, high = WINDOW_INT_RANGES[key]
            if not low <= value <= high:
                raise ValueError("{} 값이 범위를 벗어났습니다.".format(key))
            result[key] = value
        result["alpha"] = float(data.get("alpha", result["alpha"]))
        if not 0.2 <= result["alpha"] <= 1.0:
            raise ValueError("투명도 값이 올바르지 않습니다.")
        for key in BOOLEAN_KEYS:
            result[key] = bool(data.get(key, result[key]))
        for key, values in ENUM_OPTIONS.items():
            value = str(data.get(key, result[key]))
            if value not in values:
                raise ValueError("{} 값이 올바르지 않습니다.".format(key))
            result[key] = value
        for key in COLOR_KEYS:
            value = str(data.get(key, result[key])).strip()
            if not HEX_COLOR_PATTERN.fullmatch(value):
                raise ValueError("{} 값이 올바르지 않습니다.".format(key))
            result[key] = value.lower()
        for key in FONT_KEYS:
            value = str(data.get(key, result[key])).strip()
            if not value:
                raise ValueError("{} 값이 비어 있습니다.".format(key))
            result[key] = value
        for key in SIZE_KEYS:
            value = int(data.get(key, result[key]))
            if not 5 <= value <= 150:
                raise ValueError("{} 값이 범위를 벗어났습니다.".format(key))
            result[key] = value
        items = []
        for item in data.get("items", []):
            title = str(item.get("title", "")).strip()
            date_text = str(item.get("date", "")).strip()
            if title and self._valid_date(date_text):
                items.append({"title": title, "date": date_text})
            elif title:
                raise ValueError("D-Day 날짜가 올바르지 않습니다: {}".format(date_text))
        if items:
            result["items"] = items
        return result

    def _serialize(self, data: Dict[str, Any]) -> str:
        return self._serialize_normalized(self._normalize(data))

    def _serialize_normalized(self, data: Dict[str, Any]) -> str:
        """이미 검증된 설정을 INI 문자열로 변환합니다."""
        parser = self._new_parser()
        parser["Meta"] = {"schema_version": str(SCHEMA_VERSION),
                          "saved_at": datetime.now().astimezone().isoformat(timespec="seconds")}
        window = {key: str(data[key]) for key in
                  ("x", "y", "w", "h", "alpha", "topmost",
                   "use_glass_background", "show_calendar", "auto_start", "time_format",
                   "date_format", "day_format") + COLOR_KEYS + FONT_KEYS + SIZE_KEYS}
        parser["Window"] = window
        for index, item in enumerate(data["items"], 1):
            parser["DDay-{}".format(index)] = item
        buffer = io.StringIO()
        parser.write(buffer)
        return buffer.getvalue()

    def _rotate_backups(self) -> None:
        sources = [self.config_file] + self.backup_files[:-1]
        for source, destination in reversed(list(zip(sources, self.backup_files))):
            if source.exists() and self._read_file(source).status == "ok":
                self._atomic_copy(source, destination)

    def _preserve_problem_file(self, path: os.PathLike) -> Optional[str]:
        path = Path(path)
        if not path.exists():
            return None
        stamp = datetime.now().strftime("%Y%m%d-%H%M%S-%f")
        destination = self.config_dir / "dday_config.corrupt-{}.ini".format(stamp)
        try:
            self._atomic_copy(path, destination)
            return str(destination)
        except OSError as exc:
            self.logger.error("문제 파일 보존 실패: %s", exc)
            return None

    @staticmethod
    def _atomic_write_text(path: os.PathLike, content: str) -> None:
        path = Path(path)
        path.parent.mkdir(parents=True, exist_ok=True)
        fd, temp_name = tempfile.mkstemp(prefix=path.name + ".", suffix=".tmp",
                                         dir=str(path.parent))
        try:
            with os.fdopen(fd, "w", encoding="utf-8", newline="") as handle:
                handle.write(content)
                handle.flush()
                os.fsync(handle.fileno())
            os.replace(temp_name, path)
        except Exception:
            try:
                os.unlink(temp_name)
            except OSError:
                pass
            raise

    def _atomic_copy(self, source: os.PathLike, destination: os.PathLike) -> None:
        source, destination = Path(source), Path(destination)
        fd, temp_name = tempfile.mkstemp(prefix=destination.name + ".", suffix=".tmp",
                                         dir=str(destination.parent))
        try:
            with source.open("rb") as source_handle, os.fdopen(fd, "wb") as handle:
                shutil.copyfileobj(source_handle, handle)
                handle.flush()
                os.fsync(handle.fileno())
            os.replace(temp_name, destination)
            try:
                shutil.copystat(source, destination)
            except OSError:
                pass
        except Exception:
            try:
                os.unlink(temp_name)
            except OSError:
                pass
            raise

    def load_external(self, source_path: os.PathLike) -> LoadResult:
        return self._read_file(source_path)

    def load_latest_backup(self) -> LoadResult:
        for path in self.backup_files:
            result = self._read_file(path)
            if result.status in {"ok", "partial"}:
                return result
        return LoadResult(_fresh_defaults(), "invalid",
                          warnings=["사용 가능한 백업이 없습니다."],
                          error="사용 가능한 백업이 없습니다.")

    def export_settings(self, data: Dict[str, Any],
                        destination_path: os.PathLike) -> SaveResult:
        try:
            self._atomic_write_text(destination_path, self._serialize(data))
            return SaveResult(True, "설정 파일을 내보냈습니다.", str(destination_path))
        except (OSError, ValueError, configparser.Error) as exc:
            self.logger.exception("설정 내보내기 실패")
            return SaveResult(False, "설정 파일을 내보내지 못했습니다: {}".format(exc))
