from pathlib import Path
from shutil import copy2
import sys

from PIL import Image


PROJECT = Path(r"E:\OfficeHell")
SOURCE = PROJECT / "testAssets"
ROWS = PROJECT / "Temp" / "OfficeHellArtExport"
ART = PROJECT / "Assets" / "_Game" / "Art"
RESOURCES = ART / "Resources" / "OfficeHellArt"

FRAME_COUNTS = {
    "player": 3,
    "deadline": 5,
    "mail": 6,
    "ppt": 4,
    "bug": 6,
    "report": 4,
    "veteran": 6,
    "leader": 4,
    "boss": 4,
}

ROW_TOPS = {
    "player": 335,
    "deadline": 526,
    "mail": 731,
    "ppt": 919,
    "bug": 1107,
    "report": 1315,
    "veteran": 1489,
    "leader": 1686,
    "boss": 1889,
}

# These delivered rows replace their PSD layers. Their frames have deliberately different widths,
# so they must be separated by fully transparent column gaps instead of by width / frame count.
DIRECT_ROWS = {
    "mail": "\u90ae\u4ef6.png",
    "report": "\u5468\u62a5.png",
}


def find_source(name: str) -> Path:
    path = SOURCE / name
    if not path.exists():
        raise FileNotFoundError(path)
    return path


def export_animation_rows() -> None:
    from psd_tools import PSDImage

    psd = PSDImage.open(find_source("序列帧.psd"))
    layers = list(psd.descendants())
    ROWS.mkdir(parents=True, exist_ok=True)

    for key, target_top in ROW_TOPS.items():
        candidates = [
            layer
            for layer in layers
            if layer.kind != "group"
            and layer.width > 50
            and layer.height > 50
            and abs(layer.top - target_top) <= 6
        ]
        if len(candidates) != 1:
            raise RuntimeError(
                f"expected one animation layer for {key} at y={target_top}, found {len(candidates)}"
            )

        image = candidates[0].topil()
        if image is None:
            raise RuntimeError(f"could not composite animation layer {key}")
        image.convert("RGBA").save(ROWS / f"{key}.png", optimize=True)

def export_direct_rows() -> None:
    ROWS.mkdir(parents=True, exist_ok=True)
    for key, filename in DIRECT_ROWS.items():
        image = Image.open(find_source(filename)).convert("RGBA")
        image.save(ROWS / f"{key}.png", optimize=True)


def split_disconnected_row(source: Image.Image, key: str, count: int) -> list[Image.Image]:
    alpha = source.getchannel("A")
    occupied = [
        alpha.crop((x, 0, x + 1, source.height)).getbbox() is not None
        for x in range(source.width)
    ]

    spans = []
    start = None
    for x, has_content in enumerate(occupied + [False]):
        if has_content and start is None:
            start = x
        elif not has_content and start is not None:
            spans.append((start, x))
            start = None

    if len(spans) != count:
        raise RuntimeError(
            f"animation row {key} has {len(spans)} visible column groups, expected {count}"
        )

    return [source.crop((left, 0, right, source.height)) for left, right in spans]


def split_row(key: str, count: int) -> None:
    source = Image.open(ROWS / f"{key}.png").convert("RGBA")
    alpha_box = source.getchannel("A").getbbox()
    if alpha_box is None:
        raise RuntimeError(f"animation row {key} is empty")

    left, top, right, bottom = alpha_box
    cells = split_disconnected_row(source, key, count) if key in DIRECT_ROWS else []
    cell_width = (right - left) / count
    crops = []
    for index in range(count):
        if cells:
            cell = cells[index]
        else:
            cell_left = round(left + cell_width * index)
            cell_right = round(left + cell_width * (index + 1))
            cell = source.crop((cell_left, top, cell_right, bottom))
        content_box = cell.getchannel("A").getbbox()
        if content_box is None:
            raise RuntimeError(f"animation row {key} frame {index} is empty")
        crops.append(cell.crop(content_box))

    max_width = max(image.width for image in crops)
    max_height = max(image.height for image in crops)
    padding = 4
    output_dir = RESOURCES / "Characters" / key
    output_dir.mkdir(parents=True, exist_ok=True)

    for index, crop in enumerate(crops):
        canvas = Image.new(
            "RGBA",
            (max_width + padding * 2, max_height + padding * 2),
            (0, 0, 0, 0),
        )
        x = (canvas.width - crop.width) // 2
        y = canvas.height - crop.height - padding
        canvas.alpha_composite(crop, (x, y))
        canvas.save(output_dir / f"{index:02d}.png", optimize=True)


def export_branding() -> None:
    logo = Image.open(find_source("logo.psd")).convert("RGB")
    logo.thumbnail((2048, 2048), Image.Resampling.LANCZOS)
    branding = RESOURCES / "Branding"
    branding.mkdir(parents=True, exist_ok=True)
    logo.save(branding / "LogoMain.png", optimize=True)

    original = Image.open(find_source("logo.psd")).convert("RGB")
    crop = original.crop((650, 250, 4450, 2500))
    crop.thumbnail((480, 480), Image.Resampling.LANCZOS)
    icon = Image.new("RGB", (512, 512), (111, 188, 225))
    icon.paste(crop, ((icon.width - crop.width) // 2, (icon.height - crop.height) // 2))
    icon_path = ART / "Branding"
    icon_path.mkdir(parents=True, exist_ok=True)
    icon.save(icon_path / "AppIcon.png", optimize=True)


def copy_static_art() -> None:
    environment = RESOURCES / "Environment"
    effects = RESOURCES / "Effects"
    environment.mkdir(parents=True, exist_ok=True)
    effects.mkdir(parents=True, exist_ok=True)
    copy2(find_source("地图.jpg"), environment / "OfficeMap.jpg")
    copy2(find_source("饼.png"), effects / "Pie.png")


def main() -> None:
    if sys.argv[1:] == ["--direct-rows"]:
        export_direct_rows()
        for key, filename in DIRECT_ROWS.items():
            split_row(key, FRAME_COUNTS[key])
        print(f"prepared {len(DIRECT_ROWS)} direct character rows")
        return
    if sys.argv[1:]:
        raise RuntimeError("supported argument: --direct-rows")

    export_animation_rows()
    export_direct_rows()

    for key, count in FRAME_COUNTS.items():
        split_row(key, count)

    export_branding()
    copy_static_art()

    total = sum(FRAME_COUNTS.values())
    print(f"prepared {total} character frames plus branding, map and pie")


if __name__ == "__main__":
    main()
