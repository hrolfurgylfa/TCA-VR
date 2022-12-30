import win32file


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
            r"\\.\pipe\tca_vr_headset_data",
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
