#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""prepare_bundles.py —— 组装全部离线资源（跨平台，CI 与本地均可运行）

产出（写入 _assets/）：
  node.zip    Node 便携版（zip 根 = node.exe，已去除顶层目录）
  dsh.zip     @deepseek-ai/dsh 完整依赖树（zip 根 = node_modules）
  bundle_info.json / checksums.json
"""
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import urllib.request
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "_assets")
WORK = os.path.join(ROOT, "build", "_bundle_work")
NODE_VERSION = "24.19.0"
DSH_PACKAGE = "@deepseek-ai/dsh"

NODE_URL = f"https://nodejs.org/dist/v{NODE_VERSION}/node-v{NODE_VERSION}-win-x64.zip"


def log(msg):
    print(f"==> {msg}", flush=True)


def download(url: str, dest: str):
    log(f"downloading {url}")
    for attempt in range(3):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
            with urllib.request.urlopen(req, timeout=180) as r, open(dest, "wb") as f:
                shutil.copyfileobj(r, f, length=1024 * 1024)
            log(f"downloaded {os.path.getsize(dest) // 1024 // 1024} MB -> {dest}")
            return
        except Exception as e:
            log(f"download attempt {attempt + 1} failed: {e}")
            if attempt == 2:
                raise
    raise RuntimeError("download failed")


def make_zip(src: str, dst: str, strip_top: bool = False):
    """打包目录为 zip。strip_top=True 时去掉第一层子目录（node 官方 zip 有顶层目录）"""
    log(f"packing {os.path.basename(dst)} from {src}")
    if strip_top:
        src = os.path.join(src, os.listdir(src)[0])
    total = sum(len(fs) for _, _, fs in os.walk(src))
    done = 0
    with zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED, allowZip64=True) as z:
        for root, dirs, files in os.walk(src):
            for f in files:
                p = os.path.join(root, f)
                z.write(p, os.path.relpath(p, src))
                done += 1
                if done % 3000 == 0:
                    log(f"  {done}/{total} files")
    log(f"  done: {total} files, {os.path.getsize(dst) // 1024 // 1024} MB")


def sha256(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while True:
            c = f.read(1024 * 1024)
            if not c:
                break
            h.update(c)
    return h.hexdigest()


def main():
    os.makedirs(ASSETS, exist_ok=True)
    os.makedirs(WORK, exist_ok=True)

    # 1. Node 便携版
    node_src_zip = os.path.join(WORK, "node-src.zip")
    if not os.path.isfile(node_src_zip):
        download(NODE_URL, node_src_zip)
    node_extract = os.path.join(WORK, "node-src")
    if os.path.isdir(node_extract):
        shutil.rmtree(node_extract, ignore_errors=True)
    log("extracting node zip")
    with zipfile.ZipFile(node_src_zip) as z:
        z.extractall(node_extract)
    top = os.path.join(node_extract, os.listdir(node_extract)[0])
    node_exe = os.path.join(top, "node.exe")
    npm_cmd = os.path.join(top, "npm.cmd")
    assert os.path.isfile(node_exe), f"node.exe not found in {top}"
    node_ver = subprocess.run([node_exe, "--version"], capture_output=True,
                              text=True, timeout=30).stdout.strip()
    log(f"node version: {node_ver}")
    make_zip(node_extract, os.path.join(ASSETS, "node.zip"), strip_top=True)

    # 2. dsh 离线依赖（必须在本机平台安装，拿原生二进制）
    dsh_app = os.path.join(WORK, "dsh-app")
    if os.path.isdir(dsh_app):
        shutil.rmtree(dsh_app, ignore_errors=True)
    os.makedirs(dsh_app)
    log(f"npm install {DSH_PACKAGE} (platform: {sys.platform})")
    npm_env = dict(os.environ)
    proc = subprocess.run(
        [npm_cmd, "install", DSH_PACKAGE, "--no-audit", "--no-fund",
         "--ignore-scripts=false"],
        cwd=dsh_app, env=npm_env, timeout=1500,
    )
    if proc.returncode != 0:
        raise RuntimeError(f"npm install failed with {proc.returncode}")
    pkg_json = os.path.join(dsh_app, "node_modules", "@deepseek-ai", "dsh", "package.json")
    with open(pkg_json, encoding="utf-8") as f:
        dsh_ver = json.load(f)["version"]
    log(f"dsh version: {dsh_ver}")

    # 3. smoke test：直接跑 dsh --version（验证离线包可用）
    entry = os.path.join(dsh_app, "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js")
    smoke = subprocess.run([node_exe, entry, "--version"], capture_output=True,
                           text=True, timeout=60, cwd=dsh_app)
    log(f"smoke test dsh --version -> {smoke.stdout.strip() or smoke.stderr.strip()}")
    if smoke.returncode != 0:
        raise RuntimeError("dsh smoke test failed")

    make_zip(dsh_app, os.path.join(ASSETS, "dsh.zip"))

    # 4. 元信息与校验
    with open(os.path.join(ASSETS, "bundle_info.json"), "w", encoding="utf-8") as f:
        json.dump({"dsh_version": dsh_ver, "node_version": node_ver,
                   "built_at": __import__("time").strftime("%Y-%m-%d %H:%M:%S")},
                  f, ensure_ascii=False, indent=2)
    checks = {}
    for name in ("node.zip", "dsh.zip"):
        p = os.path.join(ASSETS, name)
        checks[name] = sha256(p)
        log(f"{name}: {os.path.getsize(p) / 1048576:.1f} MB  sha256={checks[name][:16]}…")
    with open(os.path.join(ASSETS, "checksums.json"), "w", encoding="utf-8") as f:
        json.dump(checks, f, indent=2)
    log("ALL BUNDLES READY")


if __name__ == "__main__":
    main()
