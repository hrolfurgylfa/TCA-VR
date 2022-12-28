# -*- mode: python ; coding: utf-8 -*-


block_cipher = None


a = Analysis(
    ['src\\main.py'],
    pathex=[],
    binaries=[
        ( "env/Lib/site-packages/xr/api_layer/windows/XrApiLayer_api_dump.dll", "xr/api_layer/windows" ),
        ( "env/Lib/site-packages/xr/api_layer/windows/XrApiLayer_core_validation.dll", "xr/api_layer/windows" ),
        ( "env/Lib/site-packages/xr/api_layer/windows/XrApiLayer_python.dll", "xr/api_layer/windows" ),
        ( "env/Lib/site-packages/xr/api_layer/windows/__init__.py", "xr/api_layer/windows" ),
        ( "env/Lib/site-packages/xr/library/openxr_loader.dll", "xr/library" ),
        ( "env/Lib/site-packages/glfw/glfw3.dll", "." ),
    ],
    datas=[],
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)
pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='xr_server',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)
coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name='TCA_VR-xr_server',
)
