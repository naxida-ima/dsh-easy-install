# -*- mode: python ; coding: utf-8 -*-
"""PyInstaller spec：安装向导 install.exe（onedir）"""
import os
from PyInstaller.utils.hooks import collect_all, collect_submodules

ROOT = os.path.dirname(os.path.abspath(SPEC))

datas = [(os.path.join(ROOT, "assets"), "assets")]
binaries = []
hiddenimports = []

# pywin32 全量收集（快捷方式 COM）
for pkg in ("win32com", "pythoncom", "pywintypes"):
    try:
        d, b, h = collect_all(pkg)
        datas += d
        binaries += b
        hiddenimports += h
    except Exception:
        pass

hiddenimports += collect_submodules("customtkinter")

a = Analysis(
    [os.path.join(ROOT, "build", "entry_installer.py")],
    pathex=[ROOT],
    binaries=binaries,
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=["pystray"],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="install",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    console=False,
    icon=os.path.join(ROOT, "assets", "icon.ico"),
)
coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=False,
    name="install",
)
