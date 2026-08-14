# -*- mode: python ; coding: utf-8 -*-
"""PyInstaller spec：桌面开关 switch.exe（onedir）"""
import os
from PyInstaller.utils.hooks import collect_all, collect_submodules

ROOT = os.path.dirname(os.path.abspath(SPEC))

datas = [(os.path.join(ROOT, "assets"), "assets")]
binaries = []
hiddenimports = []

# pystray 托盘（含后端）
for pkg in ("pystray", "PIL"):
    try:
        d, b, h = collect_all(pkg)
        datas += d
        binaries += b
        hiddenimports += h
    except Exception:
        pass

hiddenimports += collect_submodules("customtkinter")
hiddenimports += ["installer", "installer.engine", "installer.detector"]

a = Analysis(
    [os.path.join(ROOT, "build", "entry_switch.py")],
    pathex=[ROOT],
    binaries=binaries,
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="switch",
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
    name="switch",
)
