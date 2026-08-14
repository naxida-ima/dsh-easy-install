#!/usr/bin/env python3
"""生成 dsh 安装器全套视觉资源：icon.ico / banner.png / 托盘图标源图"""
import os
from PIL import Image, ImageDraw, ImageFont

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(BASE, "assets")
os.makedirs(ASSETS, exist_ok=True)

# DeepSeek 品牌色系
BLUE = (77, 107, 254, 255)      # #4D6BFE
BLUE_DARK = (46, 58, 168, 255)
CYAN = (105, 224, 255, 255)
BG_DARK = (22, 24, 40, 255)     # 向导背景
BG_CARD = (31, 34, 58, 255)


def find_font(size):
    for p in [
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "C:/Windows/Fonts/arialbd.ttf",
    ]:
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                pass
    return ImageFont.load_default()


def radial_gradient(size, c1, c2):
    """线性对角渐变背景"""
    w, h = size
    img = Image.new("RGB", (w, h))
    px = img.load()
    for y in range(h):
        t = y / max(h - 1, 1)
        r = int(c1[0] + (c2[0] - c1[0]) * t)
        g = int(c1[1] + (c2[1] - c1[1]) * t)
        b = int(c1[2] + (c2[2] - c1[2]) * t)
        for x in range(w):
            px[x, y] = (r, g, b)
    return img


def make_icon(size):
    """圆角方形渐变底 + 白色闪电/折线 'dsh' 图形"""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # 圆角方块背景（对角渐变）
    grad = radial_gradient((size, size), (92, 120, 255), (30, 44, 140))
    mask = Image.new("L", (size, size), 0)
    md = ImageDraw.Draw(mask)
    r = size * 0.22
    md.rounded_rectangle([0, 0, size - 1, size - 1], radius=r, fill=255)
    img.paste(grad, (0, 0), mask)
    d = ImageDraw.Draw(img)

    # 白色闪电图形（中心）
    cx, cy = size * 0.5, size * 0.56
    s = size * 0.24
    bolt = [
        (cx + s * 0.15, cy - s * 1.15),
        (cx - s * 0.55, cy + s * 0.25),
        (cx - s * 0.05, cy + s * 0.25),
        (cx - s * 0.35, cy + s * 1.15),
        (cx + s * 0.55, cy - s * 0.25),
        (cx + s * 0.05, cy - s * 0.25),
    ]
    d.polygon(bolt, fill=(255, 255, 255, 255))

    # 底部三道小弧线（地线装饰）
    y0 = size * 0.86
    lw = max(2, size // 48)
    for i, wdt in enumerate([0.44, 0.30, 0.16]):
        x0 = size * 0.5 - size * wdt / 2
        x1 = size * 0.5 + size * wdt / 2
        d.line([(x0, y0 + i * lw * 1.9), (x1, y0 + i * lw * 1.9)],
               fill=(200, 220, 255, 230), width=lw)
    return img


def make_banner(w=980, h=200):
    """向导顶部横幅：深蓝渐变 + 装饰光斑 + 圆角"""
    img = radial_gradient((w, h), (70, 95, 230), (26, 32, 96))
    d = ImageDraw.Draw(img, "RGBA")
    # 右上光斑
    for rad, alpha in [(150, 26), (95, 34)]:
        d.ellipse([w - rad * 1.6, -rad, w - rad * 0.4, rad * 0.9],
                  fill=(120, 160, 255, alpha))
    # 左侧光斑
    for rad, alpha in [(110, 20), (60, 26)]:
        d.ellipse([-rad * 0.5, h * 0.35, rad, h * 0.35 + rad * 1.5],
                  fill=(90, 200, 255, alpha))
    # 底部细线
    d.line([(0, h - 2), (w, h - 2)], fill=(255, 255, 255, 40), width=2)
    return img


def make_tray_icon(size=64, on=True):
    """托盘图标：绿=运行 / 灰=停止 圆点"""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    col = (76, 217, 100, 255) if on else (150, 152, 168, 255)
    d.ellipse([2, 2, size - 3, size - 3], fill=col)
    ring = (255, 255, 255, 255) if on else (230, 230, 235, 255)
    d.ellipse([size * 0.28, size * 0.28, size * 0.72, size * 0.72], fill=ring)
    inner = (255, 255, 255, 255)
    d.ellipse([size * 0.40, size * 0.40, size * 0.60, size * 0.60], fill=inner)
    return img


def write_ico(path, imgs, sizes):
    """手写多尺寸 ICO（Vista+ PNG 压缩格式），绕开 PIL ICO 保存 bug"""
    import struct
    pngs = []
    for img in imgs:
        buf = img.convert("RGBA")
        io = __import__("io").BytesIO()
        buf.save(io, format="PNG")
        pngs.append(io.getvalue())
    count = len(pngs)
    header = struct.pack("<HHH", 0, 1, count)
    entries = b""
    offset = 6 + 16 * count
    for (w, h), data in zip(sizes, pngs):
        entries += struct.pack(
            "<BBBBHHII", 0 if w >= 256 else w, 0 if h >= 256 else h,
            0, 0, 1, 32, len(data), offset)
        offset += len(data)
    with open(path, "wb") as f:
        f.write(header + entries + b"".join(pngs))


def main():
    # icon.ico：多尺寸
    sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    imgs = [make_icon(s).resize((s, s), Image.LANCZOS) for s, _ in sizes]
    write_ico(os.path.join(ASSETS, "icon.ico"), imgs, sizes)
    # banner
    make_banner().save(os.path.join(ASSETS, "banner.png"))
    # 托盘源图
    make_tray_icon(64, True).save(os.path.join(ASSETS, "tray_on.png"))
    make_tray_icon(64, False).save(os.path.join(ASSETS, "tray_off.png"))
    # 完成页大勾图
    img = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([6, 6, 122, 122], fill=(56, 210, 120, 255))
    d.line([(34, 66), (56, 90), (96, 42)], fill=(255, 255, 255, 255), width=14, joint="curve")
    img.save(os.path.join(ASSETS, "done.png"))
    print("assets generated:")
    for f in os.listdir(ASSETS):
        print(" ", f, os.path.getsize(os.path.join(ASSETS, f)))


if __name__ == "__main__":
    main()
