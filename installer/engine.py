# -*- coding: utf-8 -*-
"""安装引擎：校验离线资源 → 解压部署 → 写配置 → 创建快捷方式 → 开机自启"""
from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import sys
import time
import zipfile

from shared import paths
from shared.dsh_core import stop_service
from shared.paths import log

ProgressCb = callable


def _sha256(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while True:
            chunk = f.read(1024 * 1024)
            if not chunk:
                break
            h.update(chunk)
    return h.hexdigest()


def load_checksums() -> dict:
    """读取安装包自带的校验文件；不存在则返回空（跳过校验）"""
    p = paths.bundle_file("checksums.json")
    if os.path.isfile(p):
        try:
            with open(p, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            return {}
    return {}


def load_bundle_info() -> dict:
    p = paths.bundle_file("bundle_info.json")
    if os.path.isfile(p):
        try:
            with open(p, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            pass
    return {}


def verify_bundles() -> list[str]:
    """校验三个离线资源完整。返回缺失/损坏项列表（空=全部就绪）"""
    problems: list[str] = []
    checksums = load_checksums()
    for name in ("node.zip", "dsh.zip", "switch.zip"):
        p = paths.bundle_file(name)
        if not os.path.isfile(p) or os.path.getsize(p) == 0:
            problems.append(f"{name} 缺失或为空")
            continue
        expect = checksums.get(name)
        if expect:
            try:
                got = _sha256(p)
                if got.lower() != str(expect).lower():
                    problems.append(f"{name} 校验不一致（文件损坏），请重新下载")
            except Exception as e:
                problems.append(f"{name} 校验失败：{e}")
    return problems


def safe_extract_zip(zip_path: str, dest: str,
                     progress: ProgressCb | None = None,
                     phase: str = "") -> None:
    """安全解压 zip（防路径穿越），带字节级进度回调"""
    os.makedirs(dest, exist_ok=True)
    with zipfile.ZipFile(zip_path, "r") as zf:
        infos = zf.infolist()
        total = sum(i.file_size for i in infos)
        done = 0
        for info in infos:
            name = info.filename
            # 路径穿越防护
            target = os.path.normpath(os.path.join(dest, name))
            if not target.startswith(os.path.normpath(dest) + os.sep) and target != os.path.normpath(dest):
                continue
            if name.endswith("/"):
                os.makedirs(target, exist_ok=True)
                continue
            os.makedirs(os.path.dirname(target), exist_ok=True)
            with zf.open(info) as src, open(target, "wb") as out:
                shutil.copyfileobj(src, out, length=1024 * 256)
            done += info.file_size
            if progress:
                progress(done, total, phase)
    if progress:
        progress(total, total, phase)


def _write_install_json(bundle_info: dict):
    data = {
        "version": paths.TOOL_VERSION,
        "dsh_version": bundle_info.get("dsh_version", "unknown"),
        "node_version": bundle_info.get("node_version", "unknown"),
        "installed_at": time.strftime("%Y-%m-%d %H:%M:%S"),
        "install_root": paths.install_root(),
        "port": paths.PORT,
    }
    os.makedirs(paths.install_root(), exist_ok=True)
    with open(paths.install_json(), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    return data


# ---------- 快捷方式 ----------

def _create_lnk_pywin32(lnk_path: str, target: str, workdir: str, icon: str, desc: str) -> bool:
    try:
        from win32com.client import Dispatch
        shell = Dispatch("WScript.Shell")
        lnk = shell.CreateShortCut(lnk_path)
        lnk.TargetPath = target
        lnk.WorkingDirectory = workdir
        lnk.IconLocation = f"{icon},0"
        lnk.Description = desc
        lnk.Save()
        return True
    except Exception as e:
        log(f"pywin32 shortcut failed: {e}", "engine")
        return False


def _create_lnk_powershell(lnk_path: str, target: str, workdir: str, icon: str, desc: str) -> bool:
    try:
        ps = (
            "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('%s');"
            "$s.TargetPath='%s';$s.WorkingDirectory='%s';$s.IconLocation='%s,0';"
            "$s.Description='%s';$s.Save()"
            % (lnk_path.replace("'", "''"), target.replace("'", "''"),
               workdir.replace("'", "''"), icon.replace("'", "''"),
               desc.replace("'", "''"))
        )
        subprocess.run(["powershell", "-NoProfile", "-Command", ps],
                       capture_output=True, timeout=30,
                       creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        return os.path.isfile(lnk_path)
    except Exception as e:
        log(f"powershell shortcut failed: {e}", "engine")
        return False


def create_shortcuts(icon_path: str) -> dict:
    """创建桌面 + 开始菜单快捷方式。返回 {'desktop': bool, 'startmenu': bool}"""
    result = {"desktop": False, "startmenu": False}
    target = paths.switch_exe()
    if not os.path.isfile(target):
        return result

    # 桌面
    try:
        os.makedirs(paths.desktop_dir(), exist_ok=True)
        desk_lnk = os.path.join(paths.desktop_dir(), "DeepSeek Harness 开关.lnk")
        if _create_lnk_pywin32(desk_lnk, target, paths.switch_dir(), icon_path,
                               "DeepSeek Harness 开关：查看运行状态、一键启停"):
            result["desktop"] = True
        elif _create_lnk_powershell(desk_lnk, target, paths.switch_dir(), icon_path,
                                    "DeepSeek Harness 开关"):
            result["desktop"] = True
    except Exception as e:
        log(f"desktop shortcut error: {e}", "engine")

    # 开始菜单
    try:
        sm = paths.start_menu_dir()
        os.makedirs(sm, exist_ok=True)
        sm_lnk = os.path.join(sm, "DeepSeek Harness 开关.lnk")
        if _create_lnk_pywin32(sm_lnk, target, paths.switch_dir(), icon_path,
                               "DeepSeek Harness 开关") or _create_lnk_powershell(
                sm_lnk, target, paths.switch_dir(), icon_path, "DeepSeek Harness 开关"):
            result["startmenu"] = True
    except Exception as e:
        log(f"startmenu shortcut error: {e}", "engine")

    return result


# ---------- 开机自启 ----------

RUN_KEY = r"Software\Microsoft\Windows\CurrentVersion\Run"
RUN_NAME = "DeepSeekHarnessSwitch"


def set_autostart(enabled: bool) -> bool:
    """写入/移除 HKCU 开机自启（winreg 标准库，零外部依赖）"""
    if not paths.is_windows():
        return False
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, RUN_KEY, 0,
                            winreg.KEY_SET_VALUE) as key:
            if enabled:
                cmd = f'"{paths.switch_exe()}" --minimized'
                winreg.SetValueEx(key, RUN_NAME, 0, winreg.REG_SZ, cmd)
            else:
                try:
                    winreg.DeleteValue(key, RUN_NAME)
                except FileNotFoundError:
                    pass
        return True
    except Exception as e:
        log(f"autostart failed: {e}", "engine")
        return False


def get_autostart() -> bool:
    if not paths.is_windows():
        return False
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, RUN_KEY, 0,
                            winreg.KEY_QUERY_VALUE) as key:
            winreg.QueryValueEx(key, RUN_NAME)
            return True
    except FileNotFoundError:
        return False
    except Exception:
        return False


# ---------- 主安装流程 ----------

def install_all(progress: ProgressCb | None = None,
                cancel_flag: list = None) -> tuple[bool, str, dict]:
    """
    执行完整安装。progress(done_bytes, total_bytes, phase_text)
    返回 (成功?, 消息, 详情)
    """
    log("install_all start", "engine")
    phases = {
        "verify": "校验离线资源完整性…",
        "stop": "停止旧的服务实例…",
        "node": "部署内置 Node.js 运行环境…",
        "dsh": "部署 DeepSeek Harness 程序…",
        "switch": "部署桌面开关…",
        "config": "写入配置…",
        "shortcut": "创建桌面快捷方式…",
        "autostart": "设置开机自启…",
    }
    detail: dict = {}

    def cb(done, total, phase=""):
        if progress:
            progress(done, total, phase)

    # 0. 校验
    cb(0, 100, phases["verify"])
    problems = verify_bundles()
    if problems:
        log(f"verify failed: {problems}", "engine")
        return False, "离线资源不完整：" + "；".join(problems), {}

    # 1. 停止旧服务
    cb(0, 100, phases["stop"])
    try:
        stop_service(wait=False)
    except Exception as e:
        log(f"stop old service warn: {e}", "engine")

    root = paths.install_root()
    os.makedirs(root, exist_ok=True)
    bundle_info = load_bundle_info()

    # 2. Node
    cb(0, 100, phases["node"])
    safe_extract_zip(paths.bundle_file("node.zip"), paths.runtime_node_dir(),
                     cb, phases["node"])
    detail["node_ok"] = os.path.isfile(paths.node_exe())

    # 3. dsh
    cb(0, 100, phases["dsh"])
    safe_extract_zip(paths.bundle_file("dsh.zip"), paths.runtime_app_dir(),
                     cb, phases["dsh"])
    detail["dsh_ok"] = os.path.isfile(paths.dsh_entry())

    # 4. switch
    cb(0, 100, phases["switch"])
    safe_extract_zip(paths.bundle_file("switch.zip"), paths.switch_dir(),
                     cb, phases["switch"])
    detail["switch_ok"] = os.path.isfile(paths.switch_exe())

    # 5. 配置
    cb(0, 100, phases["config"])
    data = _write_install_json(bundle_info)

    # 6. 快捷方式
    cb(0, 100, phases["shortcut"])
    icon_path = os.path.join(paths.switch_dir(), "_internal", "assets", "icon.ico")
    if not os.path.isfile(icon_path):
        icon_path = paths.switch_exe()
    sc = create_shortcuts(icon_path)
    detail["shortcut"] = sc

    # 7. 开机自启（默认开启）
    cb(0, 100, phases["autostart"])
    detail["autostart"] = set_autostart(True)

    cb(100, 100, "安装完成")
    log("install_all done", "engine")
    return True, "安装完成", detail


# ---------- 卸载 ----------

def uninstall_all() -> tuple[bool, str]:
    """停止服务、删除安装目录与快捷方式、移除自启"""
    try:
        stop_service(wait=True)
    except Exception as e:
        log(f"uninstall stop warn: {e}", "engine")
    set_autostart(False)
    for lnk in ("DeepSeek Harness 开关.lnk",):
        for d in (paths.desktop_dir(), paths.start_menu_dir()):
            try:
                p = os.path.join(d, lnk)
                if os.path.isfile(p):
                    os.remove(p)
            except Exception:
                pass
    try:
        sm = paths.start_menu_dir()
        if os.path.isdir(sm) and not os.listdir(sm):
            os.rmdir(sm)
    except Exception:
        pass
    try:
        shutil.rmtree(paths.install_root(), ignore_errors=True)
    except Exception:
        pass
    return not os.path.exists(paths.install_root()), "已卸载 DeepSeek Harness"
