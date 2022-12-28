import sys
import os
import site


def fix_pywin32_in_frozen_build() -> None:
    if sys.platform != "win32" or not getattr(sys, "frozen", False):
        return

    site.addsitedir(sys.path[0])
    print("sys.path", sys.path)

    # sys.path has been extended; use final
    # path to locate dll folder and add it to path
    win32_path = os.path.join(sys.path[0], "win32")
    os.environ["PYTHONPATH"] += ";" + win32_path

    # import pythoncom module
    # import importlib
    # import importlib.machinery

    # for name in ("win32file",):
    #     filename = os.path.join(path, name + "310.dll")
    #     loader = importlib.machinery.ExtensionFileLoader(name, filename)
    #     spec = importlib.machinery.ModuleSpec(name=name, loader=loader, origin=filename)
    #     _mod = importlib._bootstrap._load(spec)  # type: ignore


# fix_pywin32_in_frozen_build()


# print("sys.path", sys.path)
# assert (
#     os.path.split(sys.path[0])[1] == "lib"
# ), "sys.path[0] has an unexpected value, expected it to point to the lib directory"

# try:
#     import pywin32_system32
# except ImportError:
#     # We still haven't run the postinstall script for some reason, run that now
#     pass
# sys.path += [
#     os.path.join(sys.path[0], "win32"),
#     os.path.join(sys.path[0], "win32", "lib"),
#     os.path.join(sys.path[0], "pythonwin"),
# ]
# print("sys.path", sys.path)


# try:
# import pywin32_bootstrap
import win32file

# except ImportError as e:
#     print("Skipping win32file import, this is probably fine.", e)


class InvalidStateError(Exception):
    def __init__(self, message: str | None = None) -> None:
        super().__init__(
            message or "The function you called cannot be used in this state."
        )


class DataSender:
    def __init__(self) -> None:
        self._file_handle: int | None = None

    def __enter__(self) -> "DataSender":
        self._file_handle = win32file.CreateFile(
            r"\\.\pipe\Demo",
            win32file.GENERIC_WRITE,
            0,
            None,
            win32file.OPEN_EXISTING,
            0,
            None,
        )
        return self

    def write(self, data: bytes) -> None:
        if self._file_handle:
            win32file.WriteFile(self._file_handle, data)
        else:
            raise InvalidStateError(
                'Cannot call write without using the "with" statement first.'
            )

    def __exit__(self, exception_type, exception_value, traceback) -> None:
        if self._file_handle:
            win32file.CloseHandle(self._file_handle)
