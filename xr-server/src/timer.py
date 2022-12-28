import time


class Timer:
    def __init__(self, name: str = "default", *, silent: bool = False) -> None:
        self._name = name
        self._silent = silent

        self._start = None

    def __enter__(self):
        self._start = time.time()

    def __exit__(self, exception_type, exception_value, traceback):
        self._result = time.time() - self._start
        if not self._silent:
            print(f"Time for timer {self._name}: {self._result}")
