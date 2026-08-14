# -*- coding: utf-8 -*-
"""DeepSeek Harness 桌面开关：大圆开关 + 托盘状态灯 + 一键启停"""
import io
import os
import subprocess
import sys
import threading
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import customtkinter as ctk
from PIL import Image, ImageDraw

from shared import paths
from shared.dsh_core import (get_status, port_open, read_pid, process_alive,
                             start_service, stop_service)
from shared.paths import log
from shared.ui_theme import (AMBER, BG, CARD, CARD_2, CYAN, FONT, FONT_BIG,
                             FONT_BTN, FONT_SMALL, FONT_TITLE, GREEN, LINE,
                             PRIMARY, RED, TEXT, TEXT_DIM, setup_theme)

W, H = 470, 640
RING_R = 118
CORE_R = 88


def make_switch_bg(on: bool, size=560):
    """圆形开关背景：外圈辉光 + 内圆 + 电源符号"""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = cy = size // 2
    # 外圈辉光
    glow = GREEN if on else "#5a5f85"
    for r in range(int(RING_R * 2.15), int(RING_R * 1.65), -2):
        a = int(60 * (1 - (r - RING_R * 1.65) / (RING_R * 0.5)))
        d.ellipse([cx - r, cy - r, cx + r, cy + r],
                  fill=(glow[0] if isinstance(glow, tuple) else int(glow[1:3], 16),
                        glow[1] if isinstance(glow, tuple) else int(glow[3:5], 16),
                        glow[2] if isinstance(glow, tuple) else int(glow[5:7], 16), a))
    # 外环
    d.ellipse([cx - RING_R, cy - RING_R, cx + RING_R, cy + RING_R],
              fill=(255, 255, 255, 22), outline=(255, 255, 255, 90), width=3)
    # 内圆
    d.ellipse([cx - CORE_R, cy - CORE_R, cx + CORE_R, cy + CORE_R],
              fill=(38, 43, 76, 255))
    d.ellipse([cx - CORE_R + 8, cy - CORE_R + 8, cx + CORE_R - 8, cy + CORE_R - 8],
              fill=(52, 58, 100, 255), outline=(120, 130, 190, 120), width=2)
    # 电源符号（I / 圆弧）
    col = (110, 255, 170, 255) if on else (190, 195, 220, 255)
    bw = 16
    d.line([(cx, cy - 46), (cx, cy - 4)], fill=col, width=bw)
    d.arc([cx - 46, cy - 36, cx + 46, cy + 56], start=210, end=330,
          fill=col, width=bw)
    # 内圆中心小字
    f = None
    try:
        from PIL import ImageFont as IF
        f = IF.truetype("C:/Windows/Fonts/arialbd.ttf", 44)
    except Exception:
        pass
    status = "ON" if on else "OFF"
    if f:
        tw = d.textlength(status, font=f)
        d.text((cx - tw / 2, cy + 62), status, font=f, fill=(255, 255, 255, 200))
    else:
        d.text((cx - 36, cy + 58), status, fill=(255, 255, 255, 200))
    return img


class SwitchApp(ctk.CTk):
    def __init__(self, minimized=False, launch_web=False):
        super().__init__()
        self.title("DeepSeek Harness 开关")
        self.geometry(f"{W}x{H}")
        self.resizable(False, False)
        self.configure(fg_color=BG)
        self._minimized = minimized
        self._launch_web = launch_web
        self._status = "stopped"
        self._shutdown = False
        self._tray = None
        self._start_pending = False

        self._build_ui()
        self._setup_tray()
        self.protocol("WM_DELETE_WINDOW", self._hide_to_tray)

        threading.Thread(target=self._poller, daemon=True).start()

        if minimized:
            self.after(300, self._hide_to_tray)
        if launch_web:
            self.after(300, self._on_toggle)

    # ---------- UI ----------
    def _build_ui(self):
        self.grid_columnconfigure(0, weight=1)

        # 顶栏
        top = ctk.CTkFrame(self, fg_color="transparent")
        top.grid(row=0, column=0, sticky="ew", padx=20, pady=(16, 0))
        ctk.CTkLabel(top, text="DeepSeek Harness",
                     font=("Microsoft YaHei UI", 18, "bold"), text_color=TEXT).pack(side="left")
        self.ver_lab = ctk.CTkLabel(top, text="", font=FONT_SMALL, text_color=TEXT_DIM)
        self.ver_lab.pack(side="right")

        # 开关
        self.switch_frame = ctk.CTkFrame(self, fg_color="transparent", width=W, height=310)
        self.switch_frame.grid(row=1, column=0, pady=(6, 0))
        self.switch_frame.grid_propagate(False)
        self.switch_canvas = ctk.CTkCanvas(self.switch_frame, width=W, height=310,
                                           bg=BG, highlightthickness=0)
        self.switch_canvas.pack()
        self._bg_on = make_switch_bg(True)
        self._bg_off = make_switch_bg(False)
        self._bg_img = None
        self._draw_switch(False)
        self.switch_canvas.bind("<Button-1>", lambda e: self._on_toggle())
        hint = ctk.CTkLabel(self.switch_frame, text="点我开 / 点我关",
                            font=FONT_SMALL, text_color=TEXT_DIM)
        hint.place(relx=0.5, rely=0.9, anchor="center")

        # 状态行
        self.state_lab = ctk.CTkLabel(self, text="正在检查状态…", font=FONT_BIG,
                                      text_color=TEXT_DIM)
        self.state_lab.grid(row=2, column=0, pady=(0, 2))
        self.port_lab = ctk.CTkLabel(self, text=f"界面地址：{paths.WEB_URL}",
                                     font=FONT_SMALL, text_color=TEXT_DIM)
        self.port_lab.grid(row=3, column=0)

        # 按钮行
        btns = ctk.CTkFrame(self, fg_color="transparent")
        btns.grid(row=4, column=0, pady=(20, 0))
        self.web_btn = ctk.CTkButton(btns, text="打开界面", width=130, height=42,
                                     font=FONT_BTN, fg_color=PRIMARY,
                                     hover_color="#6d85ff", corner_radius=12,
                                     command=self._open_web)
        self.web_btn.pack(side="left", padx=8)
        self.auto_var = ctk.BooleanVar(value=False)
        self.auto_ck = ctk.CTkCheckBox(btns, text="开机自动运行", font=("Microsoft YaHei UI", 13),
                                       fg_color=PRIMARY, hover_color="#6d85ff",
                                       text_color=TEXT, variable=self.auto_var,
                                       command=self._on_auto_toggle)
        self.auto_ck.pack(side="left", padx=14)

        # 底部
        bottom = ctk.CTkFrame(self, fg_color="transparent")
        bottom.grid(row=5, column=0, sticky="ew", padx=24, pady=(16, 12))
        self.uninstall_btn = ctk.CTkButton(bottom, text="卸载 DeepSeek Harness",
                                           width=110, height=30, font=("Microsoft YaHei UI", 11),
                                           fg_color="transparent", text_color="#8b90b8",
                                           hover_color=CARD_2, corner_radius=8,
                                           command=self._uninstall)
        self.uninstall_btn.pack(side="left")
        self.tray_hint = ctk.CTkLabel(bottom, text="关闭窗口后仍会在托盘运行",
                                      font=("Microsoft YaHei UI", 11),
                                      text_color=TEXT_DIM)
        self.tray_hint.pack(side="right")

        self._load_autostart()
        self._load_version()

    def _load_version(self):
        try:
            import json
            with open(paths.install_json(), "r", encoding="utf-8") as f:
                data = json.load(f)
            self.ver_lab.configure(text=f"v{data.get('dsh_version', '?')}")
        except Exception:
            self.ver_lab.configure(text="")

    def _load_autostart(self):
        try:
            from installer import engine
            self.auto_var.set(engine.get_autostart())
        except Exception:
            self.auto_var.set(False)

    def _on_auto_toggle(self):
        try:
            from installer import engine
            engine.set_autostart(self.auto_var.get())
        except Exception as e:
            log(f"autostart toggle error: {e}", "switch")

    # ---------- 开关绘制 ----------
    def _draw_switch(self, on: bool):
        img = self._bg_on if on else self._bg_off
        self._bg_img = img
        tkimg = ctk.CTkImage(img, size=(W, 300))
        self.switch_canvas.delete("all")
        self.switch_canvas.create_image(W // 2, 158, image=tkimg)
        self.switch_canvas.tkimg = tkimg

    # ---------- 状态轮询 ----------
    def _poller(self):
        while not self._shutdown:
            try:
                st = get_status()
                self._status = st
                self.after(0, self._refresh_ui)
            except Exception:
                pass
            time.sleep(3)

    def _refresh_ui(self):
        if self._shutdown:
            return
        running = self._status == "running"
        self._draw_switch(running)
        if running:
            self.state_lab.configure(text="● 正在运行", text_color=GREEN)
            self.web_btn.configure(state="normal")
        else:
            self.state_lab.configure(text="○ 已停止", text_color="#8b90b8")
            self.web_btn.configure(state="disabled")
        if self._tray:
            try:
                self._tray.icon = make_tray(running)
                self._tray.title = f"DeepSeek Harness：{'运行中' if running else '已停止'}"
            except Exception:
                pass

    # ---------- 启停 ----------
    def _on_toggle(self):
        if self._start_pending:
            return
        if self._status == "running":
            self._set_busy("正在停止…")
            threading.Thread(target=self._do_stop, daemon=True).start()
        else:
            self._set_busy("正在启动…")
            threading.Thread(target=self._do_start, daemon=True).start()

    def _set_busy(self, txt):
        self._start_pending = True
        self.state_lab.configure(text=txt, text_color=AMBER)
        self.after(5000, lambda: setattr(self, "_start_pending", False))

    def _do_start(self):
        ok, msg = start_service()
        log(f"start: {ok} {msg}", "switch")
        self.after(0, lambda: self._finish_operate(ok, msg))
        if ok:
            self.after(600, self._open_web)

    def _do_stop(self):
        ok, msg = stop_service()
        log(f"stop: {ok} {msg}", "switch")
        self.after(0, lambda: self._finish_operate(ok, msg))

    def _finish_operate(self, ok, msg):
        self._start_pending = False
        self._status = get_status()
        self.state_lab.configure(text=msg,
                                 text_color=GREEN if ok else RED)
        self._refresh_ui()

    def _open_web(self):
        try:
            os.startfile(paths.WEB_URL)
        except Exception as e:
            log(f"open web error: {e}", "switch")

    # ---------- 托盘 ----------
    def _setup_tray(self):
        try:
            import pystray
            from pystray import Menu, MenuItem
            menu = Menu(
                MenuItem("显示开关", lambda: self.after(0, self._show_window), default=True),
                MenuItem("打开界面", lambda: self.after(0, self._open_web)),
                Menu.SEPARATOR,
                MenuItem("开机自动运行", self._tray_auto, checked=lambda i: self.auto_var.get()),
                Menu.SEPARATOR,
                MenuItem("退出", lambda: self.after(0, self._quit)),
            )
            self._tray = pystray.Icon("dsh_switch", make_tray(False),
                                      "DeepSeek Harness", menu)
            threading.Thread(target=self._tray.run, daemon=True).start()
        except Exception as e:
            log(f"tray setup error: {e}", "switch")

    def _tray_auto(self, icon, item):
        self.auto_var.set(not self.auto_var.get())
        self._on_auto_toggle()

    def _show_window(self):
        self.deiconify()
        self.lift()
        self.focus_force()

    def _hide_to_tray(self):
        self.withdraw()
        if self._tray:
            try:
                self._tray.notify("仍在后台运行，点击托盘图标可打开开关", "DeepSeek Harness")
            except Exception:
                pass

    def _quit(self):
        self._shutdown = True
        if self._tray:
            try:
                self._tray.stop()
            except Exception:
                pass
        self.destroy()

    # ---------- 卸载 ----------
    def _uninstall(self):
        if not ctk.messagebox.askyesno(
                "卸载 DeepSeek Harness",
                "确定要卸载吗？\n将停止服务并删除全部程序文件。"):
            return
        try:
            from installer import engine
            engine.uninstall_all()
        except Exception as e:
            log(f"uninstall error: {e}", "switch")
        # 延迟自删（exe 正在运行无法删除）
        try:
            bat = os.path.join(os.environ.get("TEMP", "."), "dsh_uninstall.bat")
            with open(bat, "w", encoding="gbk", errors="ignore") as f:
                f.write(f'@echo off\r\ntimeout /t 2 /nobreak >nul\r\n'
                        f'rmdir /s /q "{paths.install_root()}"\r\n'
                        f'del "%~f0"\r\n')
            subprocess.Popen([bat], creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        except Exception:
            pass
        ctk.messagebox.showinfo("卸载完成", "DeepSeek Harness 已卸载。\n正在运行中的残留文件将在几秒后自动清理。")
        self._quit()


def make_tray(on: bool, size=64) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    col = (76, 217, 100, 255) if on else (150, 152, 168, 255)
    d.ellipse([2, 2, size - 3, size - 3], fill=col)
    ring = (255, 255, 255, 255) if on else (235, 236, 242, 255)
    d.ellipse([size * 0.30, size * 0.30, size * 0.70, size * 0.70], fill=ring)
    d.ellipse([size * 0.41, size * 0.41, size * 0.59, size * 0.59], fill=(255, 255, 255, 255))
    return img


def main():
    setup_theme()
    args = sys.argv[1:]
    minimized = "--minimized" in args
    launch_web = "--launch-web" in args
    app = SwitchApp(minimized=minimized, launch_web=launch_web)
    app.mainloop()


if __name__ == "__main__":
    main()
