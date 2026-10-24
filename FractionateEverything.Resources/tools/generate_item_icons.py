"""重绘文明资源与丰碑火箭图标；透明背景与多面体轮廓适配 80×80 物品格。"""
from pathlib import Path
import math
from PIL import Image, ImageDraw

SIZE = 80
SCALE = 4
OUTPUT = Path(__file__).resolve().parents[1] / "Icons"


class Icon:
    def __init__(self):
        self.image = Image.new("RGBA", (SIZE * SCALE, SIZE * SCALE))
        self.draw = ImageDraw.Draw(self.image)

    def polygon(self, points, color):
        self.draw.polygon([(round(x * SCALE), round(y * SCALE)) for x, y in points], fill=color)

    def line(self, points, color, width=1):
        self.draw.line([(round(x * SCALE), round(y * SCALE)) for x, y in points],
                       fill=color, width=round(width * SCALE), joint="curve")

    def ellipse(self, box, color, width=None):
        box = tuple(round(value * SCALE) for value in box)
        if width is None:
            self.draw.ellipse(box, fill=color)
        else:
            self.draw.ellipse(box, outline=color, width=round(width * SCALE))

    def save(self, name):
        self.image.resize((SIZE, SIZE), Image.Resampling.LANCZOS).save(OUTPUT / f"{name}.png")


def fragment():
    icon = Icon()
    # 不规则断片与分离碎屑；冷灰金属表面留下青色纹路。
    for points, color in [
        ([(13, 29), (34, 13), (47, 23), (36, 38), (41, 47), (22, 57)], "#859c9e"),
        ([(13, 29), (22, 57), (19, 63), (9, 34)], "#34494e"),
        ([(22, 57), (41, 47), (45, 51), (19, 63)], "#496267"),
        ([(34, 13), (47, 23), (36, 38), (29, 30)], "#b9c9c5"),
        ([(48, 35), (63, 29), (70, 43), (56, 51)], "#8fa9a5"),
        ([(56, 51), (70, 43), (67, 50), (55, 57), (48, 42), (48, 35)], "#3c5962"),
        ([(42, 60), (50, 58), (55, 66), (44, 71), (37, 68)], "#809c9e"),
    ]:
        icon.polygon(points, color)
    icon.line([(19, 30), (27, 36), (30, 47)], "#b4f1de", 2)
    icon.line([(27, 36), (35, 29), (39, 29)], "#b4f1de", 1)
    icon.line([(53, 39), (59, 42), (64, 38)], "#b4f1de", 1.5)
    icon.save("fragment")


def memory_core():
    icon = Icon()
    # 球形源点与不闭合的金属环，和片状残片、方形数据体分离。
    icon.ellipse((12, 12, 68, 68), "#233a56", 5)
    icon.ellipse((14, 13, 66, 66), "#6e9fbe", 1.5)
    for radius in range(23, 0, -1):
        t = 1 - radius / 23
        color = (round(31 + 151 * t), round(102 + 137 * t), round(162 + 93 * t), 255)
        icon.ellipse((40 - radius, 39 - radius, 40 + radius, 39 + radius), color)
    icon.ellipse((27, 24, 40, 32), "#d4f8ff")
    for angle in (30, 150, 270):
        a = math.radians(angle)
        x, y = 40 + 29 * math.cos(a), 40 + 29 * math.sin(a)
        icon.polygon([(x - 5, y), (x, y - 5), (x + 5, y), (x, y + 5)], "#abc6d4")
        icon.ellipse((x - 2, y - 2, x + 2, y + 2), "#e3ffff")
    icon.save("memory-core")


def metadata():
    icon = Icon()
    # 悬浮的多层数据晶片，以方形轮廓区分能源球和结晶。
    for y, top, left, right in [(46, "#5d4d83", "#25253f", "#403452"),
                               (34, "#8f70b6", "#393256", "#604572"),
                               (22, "#bca1da", "#584675", "#816096")]:
        icon.polygon([(12, y + 10), (40, y - 4), (68, y + 10), (40, y + 24)], top)
        icon.polygon([(12, y + 10), (40, y + 24), (40, y + 30), (12, y + 16)], left)
        icon.polygon([(40, y + 24), (68, y + 10), (68, y + 16), (40, y + 30)], right)
        icon.line([(16, y + 12), (40, y + 24), (64, y + 12)], "#dab5fa", 1)
    icon.polygon([(25, 32), (40, 24), (55, 32), (40, 40)], "#35284e")
    icon.line([(31, 32), (40, 28), (49, 32), (40, 36), (31, 32)], "#f0dbff", 1.5)
    icon.save("metadata")


def monument_crystal():
    icon = Icon()
    # 金色高耸结晶簇，中央晶体保留明显的丰碑轮廓。
    for x, y, width, height in [(19, 31, 15, 33), (57, 27, 14, 38), (38, 7, 23, 62)]:
        icon.polygon([(x, y), (x + width / 2, y + 12), (x + width / 2, y + height - 6),
                      (x, y + height), (x - width / 2, y + height - 12), (x - width / 2, y + 14)], "#aa691e")
        icon.polygon([(x, y), (x, y + height), (x - width / 2, y + height - 12),
                      (x - width / 2, y + 14)], "#f5c35b")
        icon.polygon([(x, y), (x + width / 2, y + 12), (x, y + 20),
                      (x - width / 2, y + 14)], "#ffe9a1")
        icon.line([(x, y + 20), (x, y + height - 2)], "#ffe6a0", 1)
    icon.save("monument-crystal")


def monument_rocket():
    icon = Icon()
    # 金色丰碑载荷与紫色双助推器，保持运载火箭的细长轮廓。
    icon.polygon([(14, 61), (21, 39), (28, 35), (29, 66)], "#57416f")
    icon.polygon([(52, 35), (59, 39), (66, 61), (51, 66)], "#382c50")
    for x in (23, 57):
        icon.polygon([(x, 24), (x + 6, 38), (x + 6, 64), (x - 6, 64), (x - 6, 38)], "#a088bf")
        icon.polygon([(x, 24), (x, 64), (x - 6, 64), (x - 6, 38)], "#d8c5e6")
        icon.polygon([(x - 4, 64), (x + 4, 64), (x, 74)], "#7ad7f0")
    icon.polygon([(40, 4), (49, 20), (49, 60), (45, 67), (35, 67), (31, 60), (31, 20)], "#a46c29")
    icon.polygon([(40, 4), (40, 67), (35, 67), (31, 60), (31, 20)], "#e5bd6a")
    icon.polygon([(40, 4), (49, 20), (40, 26), (31, 20)], "#fff0b2")
    icon.polygon([(34, 30), (46, 30), (46, 49), (34, 49)], "#433653")
    icon.polygon([(40, 33), (44, 38), (40, 45), (36, 38)], "#e6d0fa")
    icon.line([(32, 53), (48, 53)], "#f8e4b0", 2)
    icon.polygon([(35, 67), (45, 67), (40, 77)], "#9ae8ff")
    icon.save("monument-rocket")


if __name__ == "__main__":
    fragment()
    memory_core()
    metadata()
    monument_crystal()
    monument_rocket()
