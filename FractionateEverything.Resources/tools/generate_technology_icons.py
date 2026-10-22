"""绘制 FE 科技图标：纯白主体、透明背景，名称与中文科技系列一致。"""
from pathlib import Path
from PIL import Image, ImageDraw

SIZE = 80
SCALE = 4
OUTPUT = Path(__file__).resolve().parents[1] / "Icons"


class Icon:
    """在放大画布上绘制透明度，再缩小以保留纯白抗锯齿边缘。"""

    def __init__(self):
        self.mask = Image.new("L", (SIZE * SCALE, SIZE * SCALE))
        self.draw = ImageDraw.Draw(self.mask)

    def line(self, points, width=3):
        self.draw.line([(x * SCALE, y * SCALE) for x, y in points],
                       fill=255, width=width * SCALE, joint="curve")

    def polygon(self, points):
        self.draw.polygon([(x * SCALE, y * SCALE) for x, y in points], fill=255)

    def ellipse(self, box, width=3):
        self.draw.ellipse(tuple(v * SCALE for v in box), outline=255, width=width * SCALE)

    def rect(self, box, width=3):
        self.draw.rectangle(tuple(v * SCALE for v in box), outline=255, width=width * SCALE)

    def save(self, name):
        image = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 0))
        image.putalpha(self.mask.resize((SIZE, SIZE), Image.Resampling.LANCZOS))
        image.save(OUTPUT / f"{name}.png")


def crystal(icon, x, y, size):
    """带切面的晶体轮廓。"""
    icon.line([(x, y - size), (x + size, y), (x, y + size), (x - size, y), (x, y - size)])
    icon.line([(x, y - size), (x, y + size)], 2)
    icon.line([(x - size, y), (x + size, y)], 2)


def lightning(icon, x, y):
    """竖向闪电，用于电力科技。"""
    icon.polygon([(x + 2, y - 18), (x - 12, y + 3), (x - 2, y + 3),
                  (x - 5, y + 19), (x + 13, y - 5), (x + 3, y - 5)])


def generate():
    """生成各系列图标；等级共享同系列图案。"""
    OUTPUT.mkdir(parents=True, exist_ok=True)
    icon = Icon()
    icon.line([(12, 23), (40, 10), (68, 23), (65, 48), (55, 61), (40, 70),
               (25, 61), (15, 48), (12, 23)])
    crystal(icon, 40, 38, 18)
    icon.save("珍奇催化稳定性")

    icon = Icon()
    icon.ellipse((26, 8, 54, 38))
    icon.ellipse((9, 44, 35, 71))
    icon.ellipse((45, 44, 71, 71))
    icon.line([(40, 38), (40, 43), (22, 43)], 3)
    icon.line([(40, 43), (58, 43)], 3)
    icon.polygon([(34, 22), (40, 15), (46, 22), (40, 29)])
    icon.save("原胚繁育活性")

    icon = Icon()
    icon.line([(14, 32), (14, 19), (60, 19)], 4)
    icon.polygon([(57, 11), (69, 19), (57, 27)])
    icon.line([(66, 48), (66, 61), (20, 61)], 4)
    icon.polygon([(23, 53), (11, 61), (23, 69)])
    crystal(icon, 40, 40, 14)
    icon.save("置换回收效率")

    icon = Icon()
    for y in (31, 40, 49):
        icon.line([(19, y), (40, y + 8), (61, y)], 3)
    icon.line([(40, 7), (40, 23)], 4)
    icon.polygon([(32, 18), (48, 18), (40, 27)])
    icon.line([(40, 73), (40, 61)], 4)
    icon.polygon([(32, 65), (48, 65), (40, 57)])
    icon.save("超级压缩技术")

    icon = Icon()
    icon.ellipse((8, 8, 72, 72))
    lightning(icon, 35, 38)
    icon.line([(58, 34), (58, 57)], 3)
    icon.polygon([(51, 52), (65, 52), (58, 61)])
    icon.save("亚空间存取能效")

    icon = Icon()
    icon.rect((16, 15, 64, 70), 4)
    icon.rect((29, 8, 51, 15), 3)
    lightning(icon, 40, 42)
    icon.save("交互站电力")

    icon = Icon()
    icon.ellipse((14, 14, 66, 66))
    icon.ellipse((28, 14, 52, 66), 2)
    icon.line([(15, 40), (65, 40)], 2)
    icon.line([(5, 24), (5, 8), (28, 8)], 3)
    icon.polygon([(24, 2), (34, 8), (24, 14)])
    icon.line([(75, 56), (75, 72), (52, 72)], 3)
    icon.polygon([(56, 66), (46, 72), (56, 78)])
    icon.save("行星亚空间交互")

    icon = Icon()
    icon.ellipse((6, 20, 30, 44))
    icon.ellipse((50, 36, 74, 60))
    icon.line([(30, 25), (51, 25)], 3)
    icon.polygon([(47, 18), (59, 25), (47, 32)])
    icon.line([(50, 55), (29, 55)], 3)
    icon.polygon([(33, 48), (21, 55), (33, 62)])
    icon.save("星际亚空间交互")

    icon = Icon()
    icon.ellipse((33, 33, 47, 47))
    for offset in (13, 24):
        icon.line([(40 - offset, 22), (29 - offset, 40), (40 - offset, 58)], 3)
        icon.line([(40 + offset, 22), (51 + offset, 40), (40 + offset, 58)], 3)
    icon.save("探索信号增幅")

    icon = Icon()
    icon.ellipse((9, 9, 71, 71))
    icon.polygon([(50, 23), (45, 45), (23, 57), (35, 35)])
    for points in ([(40, 5), (40, 15)], [(40, 65), (40, 75)],
                   [(5, 40), (15, 40)], [(65, 40), (75, 40)]):
        icon.line(points, 3)
    icon.save("终极探索")

    icon = Icon()
    icon.line([(11, 25), (40, 10), (69, 25), (69, 57), (40, 72), (11, 57), (11, 25)], 3)
    icon.line([(11, 25), (40, 40), (69, 25)], 3)
    icon.line([(40, 40), (40, 72)], 3)
    icon.line([(25, 18), (54, 33)], 3)
    icon.save("终极仓储")


if __name__ == "__main__":
    generate()
