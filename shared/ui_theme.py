# -*- coding: utf-8 -*-
"""customtkinter 主题与通用组件（两个 exe 共用）"""
import customtkinter as ctk

# ---------- 调色板 ----------
BG = "#131528"            # 全局背景
BG_2 = "#191b33"          # 次级背景
CARD = "#20233f"          # 卡片
CARD_2 = "#2a2e52"        # 卡片高亮
LINE = "#343860"          # 分隔线
TEXT = "#eef0ff"          # 主文字
TEXT_DIM = "#9aa0c8"      # 次级文字
PRIMARY = "#4d6bfe"       # DeepSeek 蓝
PRIMARY_HOVER = "#6d85ff"
GREEN = "#4ed884"
GREEN_DARK = "#2a9d5b"
AMBER = "#f5b544"
RED = "#ff5c66"
CYAN = "#69e0ff"

FONT = ("Microsoft YaHei UI", 13)
FONT_SMALL = ("Microsoft YaHei UI", 11)
FONT_BIG = ("Microsoft YaHei UI", 22, "bold")
FONT_TITLE = ("Microsoft YaHei UI", 30, "bold")
FONT_BTN = ("Microsoft YaHei UI", 14, "bold")


def setup_theme():
    ctk.set_appearance_mode("dark")
    ctk.set_default_color_theme("blue")
    ctk.deactivate_automatic_dpi_awareness()


class Card(ctk.CTkFrame):
    """圆角卡片容器"""

    def __init__(self, master, corner=16, fg=CARD, border=None, **kw):
        kw.setdefault("corner_radius", corner)
        kw.setdefault("fg_color", fg)
        if border:
            kw.setdefault("border_width", 1)
            kw.setdefault("border_color", border)
        super().__init__(master, **kw)


class StatusDot(ctk.CTkLabel):
    """状态圆点（绿/黄/红）"""

    def __init__(self, master, state="ok", size=14):
        color = {"ok": GREEN, "warn": AMBER, "fail": RED, "info": CYAN,
                 "running": GREEN, "stopped": "#6a6f96"}.get(state, "#6a6f96")
        super().__init__(master, text="●", text_color=color,
                         font=("Microsoft YaHei UI", size, "bold"))


class SectionTitle(ctk.CTkLabel):
    def __init__(self, master, text, sub=None):
        super().__init__(master, text=text, font=FONT_BIG, text_color=TEXT,
                         anchor="w")
        if sub:
            self._sub = ctk.CTkLabel(master, text=sub, font=FONT_SMALL,
                                     text_color=TEXT_DIM, anchor="w", justify="left")
        else:
            self._sub = None

    def pack(self, **kw):
        super().pack(**kw)
        if self._sub:
            self._sub.pack(fill="x", pady=(4, 0), padx=kw.get("padx", 0))
