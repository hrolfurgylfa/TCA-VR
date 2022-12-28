import win32gui
import win32ui
import win32con


class Screenshot:
    def __init__(self, width: int, height: int) -> None:
        self._width = width
        self._height = height

    def __enter__(self):
        w = 1920  # set this
        h = 1080  # set this
        bmpfilenamename = "out.bmp"  # set this

        hwnd = win32gui.FindWindow(None, windowname)
        wDC = win32gui.GetWindowDC(hwnd)
        dcObj = win32ui.CreateDCFromHandle(wDC)
        cDC = dcObj.CreateCompatibleDC()
        dataBitMap = win32ui.CreateBitmap()
        dataBitMap.CreateCompatibleBitmap(dcObj, self._width, self._height)
        cDC.SelectObject(dataBitMap)
        cDC.BitBlt((0, 0), (self._width, self._height), dcObj, (0, 0), win32con.SRCCOPY)
        dataBitMap.SaveBitmapFile(cDC, bmpfilenamename)

    def __exit__(self):
        # Free Resources
        dcObj.DeleteDC()
        cDC.DeleteDC()
        win32gui.ReleaseDC(hwnd, wDC)
        win32gui.DeleteObject(dataBitMap.GetHandle())
