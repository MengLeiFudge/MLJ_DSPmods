"""保留原版分馏塔轮廓，仅按建筑定义中的主题色重绘金属外壳。"""
from pathlib import Path
import colorsys
import re
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
DEFINITIONS = ROOT.parent / "FractionateEverything/src/Logic/Fractionation/Fractionators/Definitions"
TOWERS = {
    "BasicAnalysisTower": "deconstruction-tower",
    "BasicMineralTower": "mineral-copy-tower",
    "AdvancedMineralTower": "advanced-mineral-tower",
    "AdvancedInteractionTower": "interaction-tower",
    "AdvancedConversionTower": "conversion-tower",
}


def recolor(definition, filename):
    source = (DEFINITIONS / f"{definition}.cs").read_text(encoding="utf-8-sig")
    match = re.search(r"public static Color color = new\(([^)]+)\)", source)
    if match is None:
        raise ValueError(f"找不到建筑主题色：{definition}")
    rgb = tuple(float(value.strip().removesuffix("f")) for value in match[1].split(","))
    target_hue, target_saturation, target_value = colorsys.rgb_to_hsv(*rgb)
    image = Image.open(ROOT / "tools/assets/fractionator-template.png").convert("RGBA")
    pixels = []
    for red, green, blue, alpha in (image.getpixel((x, y)) for y in range(image.height) for x in range(image.width)):
        hue, saturation, value = colorsys.rgb_to_hsv(red / 255, green / 255, blue / 255)
        # 源图绿色外壳改色；低饱和金属高光、透明度和青蓝玻璃保留。
        if alpha > 0 and saturation > 0.12 and 0.17 < hue < 0.46:
            tint = colorsys.hsv_to_rgb(target_hue, saturation * target_saturation,
                                      value * (0.65 + 0.35 * target_value))
            red, green, blue = (round(channel * 255) for channel in tint)
        pixels.append((red, green, blue, alpha))
    image.putdata(pixels)
    image.save(ROOT / f"Icons/{filename}.png")


if __name__ == "__main__":
    for definition, filename in TOWERS.items():
        recolor(definition, filename)
