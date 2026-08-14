# -*- coding: utf-8 -*-
"""make_zip.py <src_dir> <out_zip> —— 目录打包为 zip（根=目录内容），长路径安全
用于替代 Compress-Archive（Windows PowerShell 有 MAX_PATH 限制）"""
import os
import sys
import zipfile


def main():
    src, dst = sys.argv[1], sys.argv[2]
    src = os.path.abspath(src)
    total = 0
    for root, dirs, files in os.walk(src):
        total += len(files)
    done = 0
    with zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED, allowZip64=True) as z:
        for root, dirs, files in os.walk(src):
            for f in files:
                p = os.path.join(root, f)
                rel = os.path.relpath(p, src)
                z.write(p, rel)
                done += 1
                if done % 2000 == 0:
                    print(f"  zipped {done}/{total} files", flush=True)
    print(f"done: {total} files -> {dst} ({os.path.getsize(dst)//1024//1024} MB)")


if __name__ == "__main__":
    main()
