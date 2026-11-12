#!/usr/bin/env python3
"""Preview sheet for the task 151 icon redesign, for Aidan to judge before shipping.

Run from anywhere, after tools/icons/render.py:

    python3 tools/icons/preview_151.py

Writes docs/ui-mockups/awb-icons-151-preview.png. It shows

  1. every ribbon icon at 16 and 32 px, light and dark, at true size and enlarged;
  2. the AWB Hole icon at true size 16, 24 (16 at 150 percent), 32 and 48 (32 at 150 percent) px on both ribbon
     backgrounds, drawn straight from the vector sources at each size (no bitmap is stretched), and the shipped
     16 and 32 px PNGs enlarged 8 times with nearest-neighbour sampling;
  3. the window's icon buttons, not chosen, mouse over, chosen (Outline), chosen with the focus ring, and disabled,
     light and dark, at 100 and 150 percent, drawn from the geometry in UI/Theme/Styles.xaml and the colour tokens.

The window buttons are a Pillow approximation of the WPF template (same geometry, colours, line weights and hatch),
not a screenshot of it.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
import render  # noqa: E402

REPO = render.REPO
ICON_DIR = render.ICON_DIR
OUT = REPO / "docs" / "ui-mockups" / "awb-icons-151-preview.png"
STYLES = REPO / "src" / "InventorAddin" / "UI" / "Theme" / "Styles.xaml"

RIBBON = {"Light": "#F3F4F6", "Dark": "#3A4150"}
TEXT = {"Light": "#1B2029", "Dark": "#E8EBF0"}
MUTED = {"Light": "#55606F", "Dark": "#A9B2C1"}

# The window's colour tokens (Colors.Light.xaml, Colors.Dark.xaml).
TOKENS = {
    "Light": dict(window="#F3F4F6", input="#FFFFFF", input_border="#8D96A5", ink="#1B3F73", material="#87A3CB",
                  chosen="#1B3F73", accent="#0F6CB0"),
    "Dark": dict(window="#2B303B", input="#1F232C", input_border="#56617A", ink="#D6E4F7", material="#5F7EAD",
                 chosen="#7DB6F2", accent="#0F6CB0"),
}


def font(size):
    return ImageFont.load_default(size=size)


def on(bg: str, icon: Image.Image) -> Image.Image:
    patch = Image.new("RGBA", icon.size, bg)
    patch.alpha_composite(icon)
    return patch.convert("RGB")


def nearest(img: Image.Image, k: int) -> Image.Image:
    return img.resize((img.width * k, img.height * k), Image.NEAREST)


def rgb(colour: str):
    return render._rgb(colour)


# --------------------------------------------------------------------------
# The vector sources drawn at any size
# --------------------------------------------------------------------------

def scaled_ops(ops, k: float):
    out = []
    for op in ops:
        subpaths = [([(x * k, y * k) for x, y in pts], closed) for pts, closed in op.subpaths]
        out.append(op._replace(subpaths=subpaths, width=op.width * k, crisp=False))
    return out


def vector_icon(name: str, set_name: str, source_px: int, out_px: int) -> Image.Image:
    """The icon's source drawing for source_px, scaled as a vector to out_px and drawn there."""
    ops = render.load_ops(name, source_px, set_name)
    resolve = render.colour_resolver(set_name, render.has_own_dark_source(name), name)
    if out_px == source_px:
        return render.render_icon(ops, out_px, resolve, name)
    return render.render_icon(scaled_ops(ops, out_px / source_px), out_px, resolve, name)


# --------------------------------------------------------------------------
# The window's icon buttons
# --------------------------------------------------------------------------

def read_geometries():
    text = STYLES.read_text(encoding="utf-8")
    geoms = {}
    for name, part, data in re.findall(r'<Geometry x:Key="HoleIcon\.(\w+)\.(Material|Line|Thick)">(.*?)</Geometry>', text):
        geoms.setdefault(name, {})[part] = render._path_subpaths(data.strip())
    hatch = re.search(r'<Geometry x:Key="HoleIconHatchGeometry">(.*?)</Geometry>', text).group(1)
    return geoms, render._path_subpaths(hatch.strip())


GEOMS, HATCH = read_geometries()


def _layer(ops, px, name):
    return render.render_icon(ops, px, lambda c: rgb(c), name)


def hole_icon(name: str, theme: str, px: int, disabled: bool = False) -> Image.Image:
    """One window icon (a 20 by 20 drawing) at px square, in the look of the HoleIcon template."""
    t = TOKENS[theme]
    k = px / 20
    g = GEOMS[name]

    def sc(subpaths):
        return [([(x * k, y * k) for x, y in pts], closed) for pts, closed in subpaths]

    # Drawing is clipped to the 20 by 20 box by the canvas; the frame check would reject the hatch, so draw it
    # with a frame check off by padding: render on a larger canvas and crop.
    pad = 2
    big = px + 2 * pad
    k2 = k

    def shift(subpaths):
        return [([(x + pad, y + pad) for x, y in pts], closed) for pts, closed in subpaths]

    mat = shift(sc(g.get("Material", [])))
    base = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    if mat:
        base.alpha_composite(_layer([render.Op("fill", mat, False, colour=t["material"])], big, name))
        mask = _layer([render.Op("fill", mat, False, colour="#FFFFFF")], big, name).getchannel("A")
        hatch_ops = []
        for pts, closed in HATCH:
            sp = [([(x * k2 + pad, y * k2 + pad) for x, y in pts], closed)]
            hatch_ops.append(render.Op("stroke", sp, False, 0.7 * k2, "butt", "miter", t["ink"]))
        # the hatch runs outside the frame by design (it is clipped), so clamp it with a wide scratch canvas
        wide = big + 80
        hatch = Image.new("RGBA", (wide, wide), (0, 0, 0, 0))
        for op in hatch_ops:
            moved = [([(x + 40, y + 40) for x, y in pts], c) for pts, c in op.subpaths]
            hatch.alpha_composite(_layer([op._replace(subpaths=moved)], wide, name))
        hatch = hatch.crop((40, 40, 40 + big, 40 + big))
        alpha = ImageChops.multiply(hatch.getchannel("A").point(lambda v: int(v * 0.55)), mask)
        hatch.putalpha(alpha)
        base.alpha_composite(hatch)
        base.alpha_composite(_layer([render.Op("stroke", mat, False, 1.3 * k, "butt", "round", t["ink"])], big, name))
    if g.get("Line"):
        base.alpha_composite(_layer([render.Op("stroke", shift(sc(g["Line"])), False, 1.1 * k, "round", "round", t["ink"])], big, name))
    if g.get("Thick"):
        base.alpha_composite(_layer([render.Op("stroke", shift(sc(g["Thick"])), False, 2.4 * k, "round", "miter", t["ink"])], big, name))
    img = base.crop((pad, pad, pad + px, pad + px))
    if disabled:
        img.putalpha(img.getchannel("A").point(lambda v: int(v * 0.4)))
    return img


def icon_button(name: str, theme: str, state: str, scale: float) -> Image.Image:
    """A 40 by 28 icon button (at scale) on the window colour, in one state."""
    t = TOKENS[theme]
    s = scale
    w, h = round(40 * s), round(28 * s)
    margin = round(6 * s)
    img = Image.new("RGB", (w + 2 * margin, h + 2 * margin), t["window"])
    d = ImageDraw.Draw(img)
    box = [margin, margin, margin + w - 1, margin + h - 1]
    radius = round(4 * s)
    border = None
    bw = 1
    fill = None
    if state == "hover":
        border = t["input_border"]
    if state in ("chosen", "focus"):
        fill, border, bw = t["input"], t["chosen"], round(2 * s)
    if state == "focus":
        ring = [margin - round(3 * s) - round(2 * s), margin - round(3 * s) - round(2 * s),
                margin + w - 1 + round(3 * s) + round(2 * s), margin + h - 1 + round(3 * s) + round(2 * s)]
        d.rounded_rectangle(ring, radius=radius + round(3 * s), outline=t["accent"], width=round(2 * s))
    if border:
        d.rounded_rectangle(box, radius=radius, fill=fill, outline=border, width=bw)
    px = round(24 * s)
    icon = hole_icon(name, theme, px, disabled=(state == "disabled"))
    patch = Image.new("RGBA", icon.size, fill or t["window"])
    patch.alpha_composite(icon)
    img.paste(patch.convert("RGB"), (margin + (w - px) // 2, margin + (h - px) // 2))
    return img


# --------------------------------------------------------------------------
# The sheet
# --------------------------------------------------------------------------

def build() -> Image.Image:
    W = 1760
    pad = 28
    f_title, f_head, f_text, f_small = font(30), font(22), font(17), font(14)
    blocks = []  # (height, draw function)

    def ribbon_section(y0, img, draw):
        draw.text((pad, y0), "1. Ribbon icons, 16 and 32 px (true size, then 4x and 2x enlarged without smoothing)", font=f_head, fill="#1B2029")
        y0 += 44
        col_w = (W - 3 * pad) // 2
        row_h = 74
        for c, set_name in enumerate(render.SETS):
            x0 = pad + c * (col_w + pad)
            bg = RIBBON[set_name]
            draw.rectangle([x0, y0, x0 + col_w, y0 + 58 + row_h * len(render.ICONS)], fill=bg)
            draw.text((x0 + 12, y0 + 6), f"{set_name} set on the {set_name.lower()} ribbon ({bg})", font=f_text, fill=TEXT[set_name])
            for j, label in enumerate(("16", "32", "16 x4", "32 x2")):
                cx = (x0 + 250, x0 + 310, x0 + 380, x0 + 480)[j]
                draw.text((cx, y0 + 38), label, font=f_small, fill=MUTED[set_name])
            for i, (name, command) in enumerate(render.ICONS):
                y = y0 + 58 + i * row_h
                draw.text((x0 + 12, y + row_h // 2), command, font=f_small, fill=TEXT[set_name], anchor="lm")
                i16 = Image.open(ICON_DIR / f"{name}.{set_name}.16.png")
                i32 = Image.open(ICON_DIR / f"{name}.{set_name}.32.png")
                img.paste(on(bg, i16), (x0 + 250, y + 28))
                img.paste(on(bg, i32), (x0 + 310, y + 20))
                img.paste(nearest(on(bg, i16), 4), (x0 + 380, y + 5))
                img.paste(nearest(on(bg, i32), 2), (x0 + 480, y + 5))
        return y0 + 58 + row_h * len(render.ICONS) + 40

    def hole_section(y0, img, draw):
        draw.text((pad, y0), "2. AWB Hole (7j) at true size, and the shipped 16 and 32 px PNGs enlarged 8x (nearest neighbour)", font=f_head, fill="#1B2029")
        y0 += 34
        draw.text((pad, y0), "16 and 32 are the shipped PNGs. 24 is the 16 px drawing and 48 the 32 px drawing, each drawn at 150 percent "
                  "straight from the vector source (no bitmap stretched). Inventor may scale its bitmaps differently.", font=f_small, fill="#55606F")
        y0 += 32
        col_w = (W - 3 * pad) // 2
        h = 470
        for c, set_name in enumerate(render.SETS):
            x0 = pad + c * (col_w + pad)
            bg = RIBBON[set_name]
            draw.rectangle([x0, y0, x0 + col_w, y0 + h], fill=bg)
            draw.text((x0 + 12, y0 + 8), f"{set_name} ribbon ({bg})", font=f_text, fill=TEXT[set_name])
            sizes = [(16, 16, "16"), (24, 16, "24 (16 at 150%)"), (32, 32, "32"), (48, 32, "48 (32 at 150%)")]
            x = x0 + 14
            for out_px, src_px, label in sizes:
                if out_px == src_px:
                    icon = Image.open(ICON_DIR / f"AwbHole.{set_name}.{out_px}.png")
                else:
                    icon = vector_icon("AwbHole", set_name, src_px, out_px)
                img.paste(on(bg, icon), (x, y0 + 56))
                draw.text((x, y0 + 56 + 54), label, font=f_small, fill=MUTED[set_name])
                x += max(out_px, 150) + 8
            # 8x
            x = x0 + 14
            y8 = y0 + 130
            for px in (16, 32):
                icon = Image.open(ICON_DIR / f"AwbHole.{set_name}.{px}.png")
                big = nearest(on(bg, icon), 8)
                img.paste(big, (x, y8))
                draw.text((x, y8 + big.height + 4), f"{px} px x8", font=f_small, fill=MUTED[set_name])
                x += big.width + 60
        return y0 + h + 40

    def window_section(y0, img, draw):
        draw.text((pad, y0), "3. Window icon buttons: not chosen, mouse over, chosen (Outline), chosen with keyboard focus, disabled", font=f_head, fill="#1B2029")
        y0 += 34
        draw.text((pad, y0), "Drawn with Pillow from the Styles.xaml geometry and the colour tokens, not a WPF screenshot. 100 percent is 40 x 28 buttons, 150 percent 60 x 42.",
                  font=f_small, fill="#55606F")
        y0 += 32
        col_w = (W - 3 * pad) // 2
        states = ("rest", "hover", "chosen", "focus", "disabled")
        labels = ("Not chosen", "Mouse over", "Chosen", "Chosen, focus", "Disabled")
        row_icons = ("SeatNone", "Counterbore", "Countersink", "Spotface")
        heights = []
        for c, theme in enumerate(("Light", "Dark")):
            x0 = pad + c * (col_w + pad)
            t = TOKENS[theme]
            y = y0
            top = y
            # draw content onto a temp image to know the height
            tmp_items = []
            for scale in (1.0, 1.5):
                tmp_items.append((f"{theme}, {round(scale * 100)}%: the states of Counterbore", [
                    (labels[i], icon_button("Counterbore", theme, s, scale)) for i, s in enumerate(states)]))
                tmp_items.append((f"{theme}, {round(scale * 100)}%: the Seat row, Counterbore chosen", [
                    (n, icon_button(n, theme, "chosen" if n == "Counterbore" else "rest", scale)) for n in row_icons]))
            total = 32 + sum(30 + max(im.height for _, im in items) + 8 for _, items in tmp_items)
            draw.rectangle([x0, y0, x0 + col_w, y0 + total], fill=t["window"])
            yy = y0 + 8
            for caption, items in tmp_items:
                draw.text((x0 + 12, yy), caption, font=f_small, fill=TEXT[theme])
                yy += 22
                x = x0 + 12
                for label, im in items:
                    img.paste(im, (x, yy))
                    draw.text((x + im.width // 2, yy + im.height + 2), label, font=font(12), fill=MUTED[theme], anchor="ma")
                    x += max(im.width, 92) + 8
                yy += max(im.height for _, im in items) + 8 + 14
            heights.append(total)
        return y0 + max(heights) + 40

    # First pass at a tall canvas, then crop.
    img = Image.new("RGB", (W, 4600), "#E9EBEF")
    draw = ImageDraw.Draw(img)
    draw.text((pad, 20), "Task 151 icon redesign: preview for Aidan", font=f_title, fill="#1B2029")
    y = 76
    y = ribbon_section(y, img, draw)
    y = hole_section(y, img, draw)
    y = window_section(y, img, draw)
    if y > img.height:
        raise SystemExit("the sheet is taller than its canvas")
    return img.crop((0, 0, W, y))


def main() -> int:
    img = build()
    img.save(OUT, "PNG", optimize=True)
    print(f"wrote {OUT.relative_to(REPO)} ({img.width} x {img.height})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
