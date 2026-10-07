#!/usr/bin/env python3
"""Render the AWB Addin ribbon icons from their SVG sources.

Needs: Python 3.8 or later and Pillow 10.1 or later, built with FreeType (for
the preview labels). Nothing else: no ImageMagick, Inkscape or browser.

Run from anywhere:

    python3 tools/icons/render.py

It reads src/InventorAddin/UI/Icons/{Name}.svg (the 32 px drawing) and
{Name}.16.svg (the 16 px drawing), writes {Name}.{Light|Dark}.{16|32}.png next
to them, and writes the preview to docs/ui-mockups/ribbon-icons.png. The output
depends only on the sources and the Pillow version, so running it twice gives
identical files.

The SVG files are the only place the shapes are defined. This script reads a
small subset of SVG (see README.md) and draws it itself: it supersamples each
icon, fills the stroke outlines, and averages back down to the target size.
Any stroke or fill colour in a source is replaced by the colour of the set
being rendered.
"""

from __future__ import annotations

import math
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import NamedTuple

from PIL import Image, ImageChops, ImageDraw, ImageFont

REPO = Path(__file__).resolve().parents[2]
ICON_DIR = REPO / "src" / "InventorAddin" / "UI" / "Icons"
PREVIEW = REPO / "docs" / "ui-mockups" / "ribbon-icons.png"

# File stem (IconNames in Core) and the command name shown in the preview.
ICONS = [
    ("PartProperties", "Part Properties"),
    ("Settings", "Settings"),
    ("About", "About"),
    ("ExportModelData", "Export Model Data"),
    ("ExportLibraries", "Export Libraries"),
]

# Stroke colour per set. "Light" is for Inventor's light theme (dark strokes).
SETS = {"Light": "#3B4350", "Dark": "#E3E7EE"}
SIZES = (16, 32)

SUPERSAMPLE = 16  # samples per pixel along each axis
CURVE_STEP = math.radians(3.75)  # flattening step for circles and arcs
MITER_LIMIT = 4.0  # SVG default

SVG_NS = "{http://www.w3.org/2000/svg}"


# --------------------------------------------------------------------------
# SVG subset reader
# --------------------------------------------------------------------------

class SourceError(Exception):
    pass


def _num(el: ET.Element, name: str, default: float | None = None) -> float:
    value = el.get(name)
    if value is None:
        if default is None:
            raise SourceError(f"<{_tag(el)}> needs '{name}'")
        return default
    return float(value)


def _tag(el: ET.Element) -> str:
    return el.tag.replace(SVG_NS, "")


def _arc_points(cx, cy, rx, ry, t0, dt):
    steps = max(1, math.ceil(abs(dt) / CURVE_STEP))
    return [
        (cx + rx * math.cos(t0 + dt * i / steps), cy + ry * math.sin(t0 + dt * i / steps))
        for i in range(1, steps + 1)
    ]


def _svg_arc(x1, y1, rx, ry, rot, large, sweep, x2, y2):
    """Points along an SVG elliptical arc (SVG 1.1 appendix F.6.5), excluding the start."""
    if rot != 0:
        raise SourceError("rotated arcs are not supported")
    if (x1, y1) == (x2, y2):
        return []
    rx, ry = abs(rx), abs(ry)
    if rx == 0 or ry == 0:
        return [(x2, y2)]
    xp, yp = (x1 - x2) / 2, (y1 - y2) / 2
    lam = xp * xp / (rx * rx) + yp * yp / (ry * ry)
    if lam > 1:
        rx, ry = rx * math.sqrt(lam), ry * math.sqrt(lam)
    num = rx * rx * ry * ry - rx * rx * yp * yp - ry * ry * xp * xp
    den = rx * rx * yp * yp + ry * ry * xp * xp
    coef = math.sqrt(max(0.0, num / den))
    if large == sweep:
        coef = -coef
    cxp, cyp = coef * rx * yp / ry, -coef * ry * xp / rx
    cx, cy = cxp + (x1 + x2) / 2, cyp + (y1 + y2) / 2
    t0 = math.atan2((yp - cyp) / ry, (xp - cxp) / rx)
    t1 = math.atan2((-yp - cyp) / ry, (-xp - cxp) / rx)
    dt = t1 - t0
    if sweep and dt < 0:
        dt += 2 * math.pi
    elif not sweep and dt > 0:
        dt -= 2 * math.pi
    pts = _arc_points(cx, cy, rx, ry, t0, dt)
    pts[-1] = (x2, y2)
    return pts


_PATH_TOKEN = re.compile(r"[MmLlHhVvAaZz]|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")
_PATH_ARGS = {"M": 2, "L": 2, "H": 1, "V": 1, "A": 7, "Z": 0}


def _path_subpaths(d: str):
    """Parse path data (M L H V A Z, absolute or relative) into (points, closed) pairs."""
    tokens = _PATH_TOKEN.findall(d)
    subpaths = []
    pts: list = []
    x = y = sx = sy = 0.0
    cmd = None
    i = 0

    def finish(closed):
        nonlocal pts
        if len(pts) > 1:
            subpaths.append((pts, closed))
        pts = []

    while i < len(tokens):
        if tokens[i].isalpha():
            cmd = tokens[i]
            i += 1
        elif cmd is None:
            raise SourceError(f"path data must start with a command: {d!r}")
        up = cmd.upper()
        if up not in _PATH_ARGS:
            raise SourceError(f"path command {cmd!r} is not supported")
        if up == "Z":
            finish(True)
            x, y = sx, sy
            cmd = None
            continue
        n = _PATH_ARGS[up]
        args = [float(t) for t in tokens[i:i + n]]
        if len(args) != n:
            raise SourceError(f"path command {cmd!r} has too few numbers: {d!r}")
        i += n
        rel = cmd.islower()
        if up != "M" and not pts:
            pts = [(x, y)]  # drawing on after a Z starts from the subpath start
        if up == "M":
            finish(False)
            x, y = (x + args[0], y + args[1]) if rel else (args[0], args[1])
            sx, sy = x, y
            pts = [(x, y)]
            cmd = "l" if rel else "L"  # further pairs are line-tos
        elif up == "L":
            x, y = (x + args[0], y + args[1]) if rel else (args[0], args[1])
            pts.append((x, y))
        elif up == "H":
            x = x + args[0] if rel else args[0]
            pts.append((x, y))
        elif up == "V":
            y = y + args[0] if rel else args[0]
            pts.append((x, y))
        elif up == "A":
            ex, ey = (x + args[5], y + args[6]) if rel else (args[5], args[6])
            pts.extend(_svg_arc(x, y, args[0], args[1], args[2], bool(args[3]), bool(args[4]), ex, ey))
            x, y = ex, ey
    finish(False)
    return subpaths


def _shape_subpaths(el: ET.Element):
    tag = _tag(el)
    if tag == "line":
        return [([(_num(el, "x1"), _num(el, "y1")), (_num(el, "x2"), _num(el, "y2"))], False)]
    if tag in ("polyline", "polygon"):
        nums = [float(t) for t in re.findall(r"[-+]?(?:\d+\.?\d*|\.\d+)", el.get("points", ""))]
        if len(nums) % 2:
            raise SourceError(f"<{tag}> has an odd number of coordinates")
        return [(list(zip(nums[0::2], nums[1::2])), tag == "polygon")]
    if tag == "rect":
        x, y, w, h = _num(el, "x", 0), _num(el, "y", 0), _num(el, "width"), _num(el, "height")
        r = min(_num(el, "rx", 0), w / 2, h / 2)
        if r <= 0:
            return [([(x, y), (x + w, y), (x + w, y + h), (x, y + h)], True)]
        pts = [(x + r, y), (x + w - r, y)]
        pts += _arc_points(x + w - r, y + r, r, r, -math.pi / 2, math.pi / 2)
        pts.append((x + w, y + h - r))
        pts += _arc_points(x + w - r, y + h - r, r, r, 0, math.pi / 2)
        pts.append((x + r, y + h))
        pts += _arc_points(x + r, y + h - r, r, r, math.pi / 2, math.pi / 2)
        pts.append((x, y + r))
        pts += _arc_points(x + r, y + r, r, r, math.pi, math.pi / 2)
        return [(pts, True)]
    if tag == "circle":
        cx, cy, r = _num(el, "cx", 0), _num(el, "cy", 0), _num(el, "r")
        pts = _arc_points(cx, cy, r, r, 0, 2 * math.pi)[:-1]
        return [([(cx + r, cy)] + pts, True)]
    if tag == "path":
        return _path_subpaths(el.get("d", ""))
    raise SourceError(f"element <{tag}> is not supported")


INHERITED = ("stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "fill", "shape-rendering")


class Op(NamedTuple):
    """One thing to draw: a fill or a stroke of some subpaths."""
    kind: str  # "fill" or "stroke"
    subpaths: list  # [(points, closed)]
    crisp: bool  # shape-rendering="crispEdges": no anti-aliasing
    width: float = 0.0
    cap: str = "butt"
    join: str = "miter"


def read_svg(path: Path, size: int):
    """Return the drawing operations of one source, checked against the expected size."""
    root = ET.parse(path).getroot()
    if _tag(root) != "svg":
        raise SourceError(f"{path.name}: root element is not <svg>")
    if root.get("viewBox", "").split() != ["0", "0", str(size), str(size)]:
        raise SourceError(f"{path.name}: viewBox must be '0 0 {size} {size}'")
    ops = []

    def walk(el, style):
        style = dict(style)
        for key in INHERITED:
            if el.get(key) is not None:
                style[key] = el.get(key)
        tag = _tag(el)
        if tag in ("svg", "g"):
            if el.get("transform"):
                raise SourceError(f"{path.name}: transforms are not supported")
            for child in el:
                walk(child, style)
            return
        if tag in ("title", "desc", "metadata"):
            return
        if el.get("transform"):
            raise SourceError(f"{path.name}: transforms are not supported")
        subpaths = _shape_subpaths(el)
        crisp = style.get("shape-rendering") == "crispEdges"
        if style.get("fill", "black") != "none":
            ops.append(Op("fill", subpaths, crisp))
        if style.get("stroke", "none") != "none":
            ops.append(Op(
                "stroke",
                subpaths,
                crisp,
                float(style.get("stroke-width", "1")),
                style.get("stroke-linecap", "butt"),
                style.get("stroke-linejoin", "miter"),
            ))

    try:
        walk(root, {})
    except SourceError as e:
        raise SourceError(f"{path.name}: {e}") from None
    return ops


# --------------------------------------------------------------------------
# Stroking and rasterising
# --------------------------------------------------------------------------

def _disc(c, r, n=48):
    return [(c[0] + r * math.cos(2 * math.pi * i / n), c[1] + r * math.sin(2 * math.pi * i / n)) for i in range(n)]


def _dedupe(pts, closed):
    out = []
    for p in pts:
        if not out or math.dist(p, out[-1]) > 1e-9:
            out.append(p)
    if closed and len(out) > 1 and math.dist(out[0], out[-1]) <= 1e-9:
        out.pop()
    return out


def stroke_outline(pts, closed, width, cap, join):
    """Convex polygons whose union is the stroke of one subpath."""
    pts = _dedupe(pts, closed)
    h = width / 2
    polys = []
    if len(pts) < 2:
        return polys
    segs = list(zip(pts, pts[1:] + pts[:1])) if closed else list(zip(pts, pts[1:]))
    dirs = []
    for a, b in segs:
        length = math.dist(a, b)
        d = ((b[0] - a[0]) / length, (b[1] - a[1]) / length)
        n = (-d[1] * h, d[0] * h)
        dirs.append(d)
        polys.append([(a[0] + n[0], a[1] + n[1]), (b[0] + n[0], b[1] + n[1]),
                      (b[0] - n[0], b[1] - n[1]), (a[0] - n[0], a[1] - n[1])])

    # Joins between segment k-1 and segment k, at the start point of segment k.
    joins = range(len(segs)) if closed else range(1, len(segs))
    for k in joins:
        p = segs[k][0]
        d1, d2 = dirs[k - 1], dirs[k]
        cross = d1[0] * d2[1] - d1[1] * d2[0]
        dot = d1[0] * d2[0] + d1[1] * d2[1]
        if abs(cross) < 1e-12 and dot > 0:
            continue
        if join == "round":
            polys.append(_disc(p, h))
            continue
        s = -1.0 if cross > 0 else 1.0
        o1 = (-d1[1] * s, d1[0] * s)
        o2 = (-d2[1] * s, d2[0] * s)
        a = (p[0] + o1[0] * h, p[1] + o1[1] * h)
        b = (p[0] + o2[0] * h, p[1] + o2[1] * h)
        odot = o1[0] * o2[0] + o1[1] * o2[1]
        if join == "miter" and odot > -1 + 1e-9 and math.sqrt(2 / (1 + odot)) <= MITER_LIMIT:
            f = h / (1 + odot)
            tip = (p[0] + (o1[0] + o2[0]) * f, p[1] + (o1[1] + o2[1]) * f)
            polys.append([p, a, tip, b])
        else:
            polys.append([p, a, b])

    if not closed:
        for p, d in ((pts[0], (-dirs[0][0], -dirs[0][1])), (pts[-1], dirs[-1])):
            if cap == "round":
                polys.append(_disc(p, h))
            elif cap == "square":
                n = (-d[1] * h, d[0] * h)
                e = (p[0] + d[0] * h, p[1] + d[1] * h)
                polys.append([(p[0] + n[0], p[1] + n[1]), (e[0] + n[0], e[1] + n[1]),
                              (e[0] - n[0], e[1] - n[1]), (p[0] - n[0], p[1] - n[1])])
    return polys


def fill_rings(mask: bytearray, side: int, rings, scale: float):
    """Fill the even-odd area of the rings into mask, sampling at sample centres."""
    edges = []
    for ring in rings:
        sp = [(x * scale, y * scale) for x, y in ring]
        for (x0, y0), (x1, y1) in zip(sp, sp[1:] + sp[:1]):
            if y0 != y1:
                edges.append((x0, y0, x1, y1))
    if not edges:
        return
    top = max(0, math.floor(min(min(e[1], e[3]) for e in edges) - 0.5))
    bottom = min(side - 1, math.ceil(max(max(e[1], e[3]) for e in edges)))
    for row in range(top, bottom + 1):
        yc = row + 0.5
        xs = sorted(
            x0 + (yc - y0) * (x1 - x0) / (y1 - y0)
            for x0, y0, x1, y1 in edges
            if min(y0, y1) <= yc < max(y0, y1)
        )
        for xa, xb in zip(xs[0::2], xs[1::2]):
            c0 = max(0, math.ceil(xa - 0.5))
            c1 = min(side, math.ceil(xb - 0.5))
            if c1 > c0:
                start = row * side
                mask[start + c0:start + c1] = b"\xff" * (c1 - c0)


def render_icon(ops, size: int, colour: str, name: str) -> Image.Image:
    side = size * SUPERSAMPLE
    smooth, crisp = bytearray(side * side), bytearray(side * side)
    for op in ops:
        if op.kind == "fill":
            groups = [[pts for pts, _ in op.subpaths]]
        else:
            groups = [[poly] for pts, closed in op.subpaths
                      for poly in stroke_outline(pts, closed, op.width, op.cap, op.join)]
        for rings in groups:
            for x, y in (p for ring in rings for p in ring):
                if x < -1e-6 or y < -1e-6 or x > size + 1e-6 or y > size + 1e-6:
                    raise SourceError(f"{name}: drawing reaches outside the {size} px frame at ({x:.2f}, {y:.2f})")
            fill_rings(crisp if op.crisp else smooth, side, rings, SUPERSAMPLE)
    alpha = Image.frombytes("L", (side, side), bytes(smooth)).reduce(SUPERSAMPLE)
    # crispEdges: a pixel is either on (at least half covered) or off.
    hard = Image.frombytes("L", (side, side), bytes(crisp)).reduce(SUPERSAMPLE).point(lambda v: 255 if v >= 128 else 0)
    alpha = ImageChops.lighter(alpha, hard)
    rgb = Image.new("RGB", (size, size), colour)
    return Image.merge("RGBA", (*rgb.split(), alpha))


def source_for(name: str, size: int) -> Path:
    if size == 32:
        return ICON_DIR / f"{name}.svg"
    return ICON_DIR / f"{name}.{size}.svg"


def load_ops(name: str, size: int):
    path = source_for(name, size)
    if path.exists():
        return read_svg(path, size)
    if size == 16:
        # No separate 16 px drawing: halve the 32 px one, with a 1 px stroke.
        ops = read_svg(source_for(name, 32), 32)
        half = []
        for op in ops:
            subpaths = [([(x / 2, y / 2) for x, y in pts], closed) for pts, closed in op.subpaths]
            half.append(op._replace(subpaths=subpaths, width=1.0 if op.kind == "stroke" else 0.0))
        return half
    raise SourceError(f"missing source {path.name}")


# --------------------------------------------------------------------------
# Preview
# --------------------------------------------------------------------------

PREVIEW_SCALE = 4
PANELS = [
    # set, title, background, tile, text, muted text
    ("Light", "Light theme", "#F3F4F6", "#DDE1E7", "#1B2029", "#55606F"),
    ("Dark", "Dark theme", "#2B303B", "#3A4150", "#E8EBF0", "#A9B2C1"),
]


def _on(bg: str, icon: Image.Image, scale: int) -> Image.Image:
    patch = Image.new("RGBA", icon.size, bg)
    patch.alpha_composite(icon)
    return patch.convert("RGB").resize((icon.width * scale, icon.height * scale), Image.NEAREST)


def render_preview(icons) -> Image.Image:
    s = PREVIEW_SCALE
    font = ImageFont.load_default(size=26)
    title_font = ImageFont.load_default(size=32)
    small = ImageFont.load_default(size=20)
    margin, label_w, gap = 36, 250, 44
    tile = 36 * s
    row_h = tile + 28
    col16 = margin + label_w
    col32 = col16 + 16 * s + gap
    coltile = col32 + 32 * s + gap
    width = coltile + tile + margin
    head_h = 120
    panel_h = head_h + row_h * len(ICONS) + margin - 28
    img = Image.new("RGB", (width, panel_h * len(PANELS)), "#000000")
    for p, (set_name, title, bg, tile_bg, text, muted) in enumerate(PANELS):
        top = p * panel_h
        draw = ImageDraw.Draw(img)
        draw.rectangle([0, top, width - 1, top + panel_h - 1], fill=bg)
        draw.text((margin, top + 28), f"{title}  ({SETS[set_name]})", font=title_font, fill=text)
        for x, label, w in ((col16, "16 px", 16 * s), (col32, "32 px", 32 * s), (coltile, "32 px on tile", tile)):
            draw.text((x + w / 2, top + head_h - 22), label, font=small, fill=muted, anchor="ms")
        for i, (name, command) in enumerate(ICONS):
            y = top + head_h + i * row_h
            mid = y + tile // 2
            draw.text((margin, mid), command, font=font, fill=text, anchor="lm")
            img.paste(_on(bg, icons[(name, set_name, 16)], s), (col16, mid - 8 * s))
            img.paste(_on(bg, icons[(name, set_name, 32)], s), (col32, mid - 16 * s))
            draw.rounded_rectangle([coltile, y, coltile + tile - 1, y + tile - 1], radius=6 * s, fill=tile_bg)
            img.paste(_on(tile_bg, icons[(name, set_name, 32)], s), (coltile + 2 * s, y + 2 * s))
    return img


# --------------------------------------------------------------------------

def save_png(img: Image.Image, path: Path):
    # A fresh image carries no metadata, colour profile or dpi, so none is written.
    img.save(path, "PNG", optimize=True)


def main() -> int:
    icons = {}
    try:
        for name, _ in ICONS:
            for size in SIZES:
                ops = load_ops(name, size)
                for set_name, colour in SETS.items():
                    icons[(name, set_name, size)] = render_icon(ops, size, colour, name)
    except SourceError as e:
        print(f"error: {e}", file=sys.stderr)
        return 1
    for (name, set_name, size), img in icons.items():
        save_png(img, ICON_DIR / f"{name}.{set_name}.{size}.png")
    save_png(render_preview(icons), PREVIEW)
    print(f"wrote {len(icons)} icons to {ICON_DIR.relative_to(REPO)} and {PREVIEW.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
