# -*- coding: utf-8 -*-
"""DeepSeek Harness 一键安装向导（精美 GUI）"""
import os
import subprocess
import sys
import threading

import customtkinter as ctk

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from installer import detector, engine
from shared import paths
from shared.dsh_core import start_service
from shared.ui_theme import (AMBER, BG, BG_2, CARD, CARD_2, CYAN, FONT, FONT_BIG,
                             FONT_BTN, FONT_SMALL, FONT_TITLE, GREEN, LINE,
                             PRIMARY, PRIMARY_HOVER, RED, TEXT, TEXT_DIM,
                             Card, StatusDot, setup_theme)

STEPS = ["欢迎", "环境检测", "安装", "完成"]
W, H = 1020, 700


def resource_img(name, size):
    base = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    p = os.path.join(base, "assets", name)
    if not os.path.exists(p):
        p = os.path.join(os.path.dirname(sys.executable), "assets", name)
    if os.path.exists(p):
        from PIL import Image
        return ctk.CTkImage(Image.open(p), size=size)
    return None


class StepRail(ctk.CTkFrame):
    """左侧步骤指示条"""

    def __init__(self, master):
        super().__init__(master, fg_color="transparent", width=170)
        self.labels: list[ctk.CTkLabel] = []
        self.circles: list[ctk.CTkLabel] = []
        for i, name in enumerate(STEPS):
            circle = ctk.CTkLabel(self, text=str(i + 1), width=30, height=30,
                                  corner_radius=15, font=("Microsoft YaHei UI", 13, "bold"))
            lab = ctk.CTkLabel(self, text=name, font=("Microsoft YaHei UI", 14),
                               anchor="w", width=100)
            row = ctk.CTkFrame(self, fg_color="transparent")
            row.pack(fill="x", padx=14, pady=8)
            circle.pack(side="left", padx=(0, 10))
            lab.pack(side="left")
            self.circles.append(circle)
            self.labels.append(lab)
            if i < len(STEPS) - 1:
                line = ctk.CTkFrame(self, width=2, height=18, fg_color=LINE)
                line.pack(pady=0)

    def set_step(self, idx: int):
        for i, (c, l) in enumerate(zip(self.circles, self.labels)):
            if i < idx:
                c.configure(fg_color=GREEN, text_color="#0b0e1c", text="✓")
                l.configure(text_color=TEXT)
            elif i == idx:
                c.configure(fg_color=PRIMARY, text_color="white")
                l.configure(text_color=TEXT, font=("Microsoft YaHei UI", 14, "bold"))
            else:
                c.configure(fg_color=CARD_2, text_color=TEXT_DIM, text=str(i + 1))
                l.configure(text_color=TEXT_DIM, font=("Microsoft YaHei UI", 14))


class CheckItem(Card):
    def __init__(self, master, label, level, message, detail):
        super().__init__(master, fg=CARD, border=LINE)
        self.grid_columnconfigure(1, weight=1)
        self.dot = StatusDot(self, level)
        self.dot.grid(row=0, column=0, padx=(16, 12), pady=12, sticky="nw")
        t = ctk.CTkLabel(self, text=label, font=("Microsoft YaHei UI", 14, "bold"),
                         text_color=TEXT, anchor="w")
        t.grid(row=0, column=1, padx=(0, 8), pady=(12, 0), sticky="w")
        m = ctk.CTkLabel(self, text=message, font=("Microsoft YaHei UI", 12),
                         text_color={"ok": GREEN, "warn": AMBER, "fail": RED,
                                     "info": CYAN}.get(level, TEXT_DIM),
                         anchor="w")
        m.grid(row=0, column=2, padx=(0, 16), pady=(12, 0), sticky="e")
        if detail:
            d = ctk.CTkLabel(self, text=detail, font=FONT_SMALL, text_color=TEXT_DIM,
                             anchor="w", justify="left", wraplength=720)
            d.grid(row=1, column=1, columnspan=2, padx=(0, 16), pady=(2, 10), sticky="we")


class WizardApp(ctk.CTk):
    def __init__(self):
        super().__init__()
        self.title("DeepSeek Harness 一键安装")
        self.geometry(f"{W}x{H}")
        self.minsize(W, H)
        self.configure(fg_color=BG)
        self.resizable(False, False)
        ico = os.path.join(_res_root(), "assets", "icon.ico")
        if os.path.exists(ico):
            try:
                self.iconbitmap(ico)
            except Exception:
                pass

        self.step = 0
        self.detect_results: list = []
        self.detect_done = False
        self.install_result: tuple | None = None
        self._installing = False
        self._cancel = []

        self._build_layout()
        self.show_step(0)

    # ---------- 布局 ----------
    def _build_layout(self):
        self.grid_columnconfigure(1, weight=1)
        self.grid_rowconfigure(1, weight=1)

        # 顶部 banner
        banner_img = resource_img("banner.png", (W, 190))
        banner = ctk.CTkLabel(self, image=banner_img, text="", fg_color="transparent")
        banner.grid(row=0, column=0, columnspan=2, sticky="ew")

        # 左侧步骤条
        self.rail = StepRail(self)
        self.rail.grid(row=1, column=0, sticky="nsw", padx=(24, 8), pady=20)

        # 内容区
        self.content = ctk.CTkFrame(self, fg_color="transparent")
        self.content.grid(row=1, column=1, sticky="nsew", padx=(8, 24), pady=20)

        # 底部按钮条
        self.btnbar = ctk.CTkFrame(self, fg_color="transparent")
        self.btnbar.grid(row=2, column=0, columnspan=2, sticky="ew",
                         padx=28, pady=(0, 22))
        self.btn_back = ctk.CTkButton(self.btnbar, text="← 上一步", width=110,
                                      font=FONT_BTN, fg_color=CARD_2, text_color=TEXT,
                                      hover_color="#353a66", command=self.go_back)
        self.btn_back.pack(side="left")
        self.btn_next = ctk.CTkButton(self.btnbar, text="下一步 →", width=150,
                                      font=FONT_BTN, fg_color=PRIMARY,
                                      hover_color=PRIMARY_HOVER, command=self.go_next)
        self.btn_next.pack(side="right")

        self.pages = {}
        for i in range(4):
            f = ctk.CTkFrame(self.content, fg_color="transparent")
            self.pages[i] = f

        self._build_welcome()
        self._build_detect()
        self._build_install()
        self._build_done()

    def _page(self, i):
        for k, f in self.pages.items():
            f.pack_forget()
        self.pages[i].pack(fill="both", expand=True)

    # ---------- 页 1：欢迎 ----------
    def _build_welcome(self):
        f = self.pages[0]
        f.grid_columnconfigure(0, weight=1)
        ctk.CTkLabel(f, text="欢迎使用 DeepSeek Harness", font=FONT_TITLE,
                     text_color=TEXT).pack(anchor="w", pady=(40, 6))
        ctk.CTkLabel(f, text="DeepSeek 官方出品的 AI 智能体工作台",
                     font=("Microsoft YaHei UI", 16), text_color=CYAN).pack(anchor="w", pady=(0, 26))

        desc = Card(f, fg=CARD)
        desc.pack(fill="x", pady=(0, 18))
        ctk.CTkLabel(desc, text=(
            "这是一个能帮你自动完成任务的 AI 助手工具：\n"
            "· 安装过程完全自动化，你只需要跟着提示点「下一步」\n"
            "· 所有需要的文件都已经打包在安装程序里，无需联网下载、无需懂任何技术\n"
            "· 装好后，桌面上会有一个「开关」，随时可以看到它是开还是关\n"
            "· 程序只在本机运行，不占用系统权限，卸载也只需点一下"),
            font=("Microsoft YaHei UI", 14), text_color=TEXT, justify="left",
            anchor="w", wraplength=780).pack(fill="x", padx=24, pady=22)

        req = Card(f, fg=CARD_2)
        req.pack(fill="x")
        ctk.CTkLabel(req, text="电脑要求（安装时会自动检测）",
                     font=("Microsoft YaHei UI", 14, "bold"), text_color=TEXT,
                     anchor="w").pack(fill="x", padx=24, pady=(18, 10))
        for txt in ("· Windows 10 或 11（64 位）", "· 内存 4GB 以上，磁盘剩余 1.5GB 以上",
                    "· 任意浏览器（如 Edge、Chrome）"):
            ctk.CTkLabel(req, text=txt, font=("Microsoft YaHei UI", 13),
                         text_color=TEXT_DIM, anchor="w").pack(fill="x", padx=24, pady=3)
        ctk.CTkLabel(req, text="",
                     font=FONT_SMALL, text_color=TEXT_DIM).pack(fill="x", padx=24, pady=(10, 16))

    # ---------- 页 2：环境检测 ----------
    def _build_detect(self):
        f = self.pages[1]
        f.grid_columnconfigure(0, weight=1)
        f.grid_rowconfigure(1, weight=1)

        head = ctk.CTkFrame(f, fg_color="transparent")
        head.grid(row=0, column=0, sticky="ew", pady=(14, 10))
        self.detect_title = ctk.CTkLabel(head, text="正在检查你的电脑…",
                                         font=("Microsoft YaHei UI", 20, "bold"),
                                         text_color=TEXT, anchor="w")
        self.detect_title.pack(side="left")
        self.detect_spinner = ctk.CTkLabel(head, text="●", text_color=CYAN,
                                           font=("Microsoft YaHei UI", 16))
        self.detect_spinner.pack(side="right")

        box = ctk.CTkScrollableFrame(f, fg_color="transparent", scrollbar_button_color=CARD_2,
                                     scrollbar_button_hover_color="#3a3f70")
        box.grid(row=1, column=0, sticky="nsew")
        self.detect_box = box

        self.detect_summary = Card(f, fg=CARD_2)
        self.detect_summary.grid(row=2, column=0, sticky="ew", pady=(12, 0))
        self.detect_summary_txt = ctk.CTkLabel(self.detect_summary, text="",
                                               font=("Microsoft YaHei UI", 14, "bold"),
                                               text_color=TEXT)
        self.detect_summary_txt.pack(padx=20, pady=14)

        self.detect_items: dict[str, CheckItem] = {}

    def _start_detect(self):
        self.detect_title.configure(text="正在检查你的电脑…")
        self.detect_summary_txt.configure(text="", text_color=TEXT)
        for w in self.detect_box.winfo_children():
            w.destroy()
        self.detect_items.clear()
        self.detect_done = False
        self.btn_next.configure(state="disabled")

        def work():
            results = detector.run_all() + detector.check_bundle_files()
            level, msg = detector.summary(results)
            self.detect_results = results
            self.detect_done = True
            self.after(0, lambda: self._render_detect(results, level, msg))

        threading.Thread(target=work, daemon=True).start()
        self._spin_detect()

    def _spin_detect(self):
        if not self.detect_done:
            self.detect_spinner.configure(text_color=CYAN)
            self.after(400, self._spin_detect)
        else:
            self.detect_spinner.configure(text="")

    def _render_detect(self, results, level, msg):
        for r in results:
            item = CheckItem(self.detect_box, r.label, r.level, r.message, r.detail)
            item.pack(fill="x", pady=4)
            self.detect_items[r.key] = item
        color = {"ok": GREEN, "warn": AMBER, "fail": RED}.get(level, TEXT)
        self.detect_summary_txt.configure(text=("✓ " if level == "ok" else "") + msg,
                                          text_color=color)
        self.detect_title.configure(text="检测完成")
        self.btn_next.configure(state="normal")

    # ---------- 页 3：安装 ----------
    def _build_install(self):
        f = self.pages[2]
        f.grid_columnconfigure(0, weight=1)
        f.grid_rowconfigure(1, weight=1)

        ctk.CTkLabel(f, text="开始安装", font=FONT_TITLE, text_color=TEXT,
                     anchor="w").pack(fill="x", pady=(36, 4))
        ctk.CTkLabel(f, text="正在把 DeepSeek Harness 和它需要的全部文件安装到你的电脑",
                     font=("Microsoft YaHei UI", 14), text_color=TEXT_DIM,
                     anchor="w").pack(fill="x", pady=(0, 22))

        self.inst_phase = ctk.CTkLabel(f, text="准备就绪，点击「开始安装」",
                                       font=("Microsoft YaHei UI", 15),
                                       text_color=TEXT, anchor="w")
        self.inst_phase.pack(fill="x", padx=6, pady=(0, 10))

        self.inst_bar = ctk.CTkProgressBar(f, height=18, corner_radius=9,
                                           fg_color=CARD_2, progress_color=PRIMARY)
        self.inst_bar.pack(fill="x", padx=6, pady=(0, 4))
        self.inst_bar.set(0)
        self.inst_pct = ctk.CTkLabel(f, text="0%", font=("Microsoft YaHei UI", 12),
                                     text_color=TEXT_DIM, anchor="e")
        self.inst_pct.pack(fill="x", padx=6, pady=(0, 14))

        logbox = ctk.CTkTextbox(f, height=170, fg_color=BG_2, text_color=TEXT_DIM,
                                font=("Consolas", 11), corner_radius=12,
                                border_width=1, border_color=LINE,
                                state="disabled", wrap="word")
        logbox.pack(fill="x", padx=6)
        self.inst_log = logbox

        self.inst_btn = ctk.CTkButton(f, text="开始安装", height=48, font=FONT_BTN,
                                      fg_color=PRIMARY, hover_color=PRIMARY_HOVER,
                                      corner_radius=14, command=self._start_install)
        self.inst_btn.pack(pady=(24, 0))

    def _append_log(self, txt):
        self.inst_log.configure(state="normal")
        self.inst_log.insert("end", txt + "\n")
        self.inst_log.see("end")
        self.inst_log.configure(state="disabled")

    def _start_install(self):
        if self._installing:
            return
        self._installing = True
        self._cancel = []
        self.inst_btn.configure(state="disabled", text="安装中…")
        self.btn_back.configure(state="disabled")
        self.btn_next.configure(state="disabled")

        last_phase = ""

        def cb(done, total, phase):
            nonlocal last_phase
            if phase and phase != last_phase:
                last_phase = phase
                self.after(0, lambda p=phase: (self.inst_phase.configure(text=p),
                                               self._append_log(p)))
            if total > 0:
                pct = min(100, int(done * 100 / total))
                self.after(0, lambda p=pct: (self.inst_bar.set(p / 100),
                                             self.inst_pct.configure(text=f"{p}%")))

        def work():
            ok, msg, detail = engine.install_all(cb, self._cancel)
            self.install_result = (ok, msg, detail)
            self.after(0, self._install_finished)

        threading.Thread(target=work, daemon=True).start()

    def _install_finished(self):
        ok, msg, detail = self.install_result
        self._installing = False
        self.inst_btn.configure(state="normal", text="开始安装")
        self.btn_back.configure(state="normal")
        self.btn_next.configure(state="normal")
        if ok:
            self.inst_phase.configure(text="✅ " + msg, text_color=GREEN)
            self.inst_bar.set(1)
            self.inst_pct.configure(text="100%")
            self._append_log("✔ 安装成功！")
            self.btn_next.configure(text="完成 →", state="normal")
        else:
            self.inst_phase.configure(text="❌ " + msg, text_color=RED)
            self._append_log("✘ " + msg)
            self.btn_next.configure(state="disabled", text="下一步 →")
            self.btn_next.configure(text="完成 →", state="normal")

    # ---------- 页 4：完成 ----------
    def _build_done(self):
        f = self.pages[3]
        f.grid_columnconfigure(0, weight=1)
        done_img = resource_img("done.png", (110, 110))
        self.done_img_label = ctk.CTkLabel(f, image=done_img, text="")
        self.done_img_label.pack(pady=(50, 10))

        ctk.CTkLabel(f, text="安装完成！", font=FONT_TITLE, text_color=GREEN).pack()
        self.done_summary = ctk.CTkLabel(f, text="", font=("Microsoft YaHei UI", 15),
                                         text_color=TEXT_DIM, justify="left",
                                         wraplength=760)
        self.done_summary.pack(pady=(18, 6))

        card = Card(f, fg=CARD)
        card.pack(fill="x", padx=40, pady=12)
        ctk.CTkLabel(card, text=(
            "接下来怎么做？\n"
            "1. 桌面上已经放好了「DeepSeek Harness 开关」\n"
            "2. 点击「立即启动」，程序会自动打开浏览器界面\n"
            "3. 在浏览器界面里填入你的 DeepSeek API Key，就可以开始对话了\n"
            "4. 以后想开就点开关，想关也点开关，开关上能直接看到运行状态\n"
            "（开关会常驻屏幕右下角托盘，开机自动运行，随时可看状态）"),
            font=("Microsoft YaHei UI", 14), text_color=TEXT, justify="left",
            anchor="w", wraplength=800).pack(padx=28, pady=22)

        self.launch_btn = ctk.CTkButton(f, text="🚀 立即启动", height=52, font=FONT_BTN,
                                        fg_color=GREEN, hover_color="#3ecf77",
                                        corner_radius=14, command=self.launch_now)
        self.launch_btn.pack(pady=(20, 0))
        self.btn_next.pack_forget()  # 完成页没有下一步

    def launch_now(self):
        exe = paths.switch_exe()
        if os.path.isfile(exe):
            try:
                subprocess.Popen([exe, "--launch-web"], cwd=paths.switch_dir(),
                                 creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            except Exception:
                pass
        self.destroy()

    # ---------- 导航 ----------
    def show_step(self, i: int):
        self.step = i
        self.rail.set_step(i)
        self._page(i)
        self.btn_back.configure(state="normal" if i > 0 else "disabled")
        if i == 0:
            self.btn_next.configure(text="开始检测 →", state="normal")
        elif i == 1:
            if not self.detect_done:
                self.btn_next.configure(text="下一步 →", state="disabled")
                self._start_detect()
            else:
                self.btn_next.configure(text="下一步 →", state="normal")
        elif i == 2:
            self.btn_next.configure(text="下一步 →", state="disabled")
        else:
            self.btn_next.pack(side="right")
            self.btn_next.configure(text="关闭", state="normal", command=self.destroy)
        if i != 3:
            try:
                self.btn_next.pack(side="right")
            except Exception:
                pass

    def go_next(self):
        if self.step == 1 and not self.detect_done:
            return
        if self.step == 2:
            if not self.install_result or not self.install_result[0]:
                if not self._installing:
                    self._start_install()
                return
        if self.step < 3:
            self.show_step(self.step + 1)

    def go_back(self):
        if self.step > 0:
            self.show_step(self.step - 1)


def main():
    setup_theme()
    app = WizardApp()
    app.mainloop()


if __name__ == "__main__":
    main()
