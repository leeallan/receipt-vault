#!/usr/bin/env python3
"""Render fictional receipt "photos" for the promo recordings.

Usage: make_receipts.py <out-dir>   (writes the UK and US sets)

Each receipt is typeset with ImageMagick in a thermal-printer style, then laid on a
table top with a slight tilt and perspective so it reads as a hand-held photo. All shops,
addresses and VAT numbers are made up.
"""
import os
import subprocess
import sys

MONO = "/System/Library/Fonts/Menlo.ttc"
BOLD = "/System/Library/Fonts/Supplemental/Courier New Bold.ttf"
W = 900          # paper width in px
PAD = 60
SIZE = 30        # body text size
LINE = 46


def money(v):
    return f"{v:.2f}" if v >= 0 else f"-{-v:.2f}"


def build(lines):
    """lines: list of (kind, left, right). kind in logo|centre|row|rule|gap."""
    draw, y = [], PAD
    for kind, left, right in lines:
        if kind == "logo":
            draw += ["-font", BOLD, "-pointsize", "92", "-gravity", "North",
                     "-annotate", f"+0+{y}", left]
            y += 120
        elif kind == "centre":
            draw += ["-font", MONO, "-pointsize", str(SIZE), "-gravity", "North",
                     "-annotate", f"+0+{y}", left]
            y += LINE
        elif kind == "big":
            draw += ["-font", BOLD, "-pointsize", "44", "-gravity", "NorthWest",
                     "-annotate", f"+{PAD}+{y}", left,
                     "-gravity", "NorthEast", "-annotate", f"+{PAD}+{y}", right]
            y += 64
        elif kind == "row":
            draw += ["-font", MONO, "-pointsize", str(SIZE), "-gravity", "NorthWest",
                     "-annotate", f"+{PAD}+{y}", left]
            if right:
                draw += ["-gravity", "NorthEast", "-annotate", f"+{PAD}+{y}", right]
            y += LINE
        elif kind == "rule":
            draw += ["-font", MONO, "-pointsize", str(SIZE), "-gravity", "North",
                     "-annotate", f"+0+{y}", "-" * 40]
            y += LINE
        elif kind == "gap":
            y += LINE // 2
    return draw, y + PAD


def receipt_lines(shop, items, footer):
    lines = [("logo", shop["name"], None)]
    lines += [("centre", t, None) for t in shop["header"]]
    lines += [("rule", None, None)]
    subtotal, savings = 0.0, 0.0
    for it in items:
        desc, price = it[0], it[1]
        lines.append(("row", desc, money(price)))
        subtotal += price
        if len(it) > 2:
            lines.append(("row", f"   {it[2][0]}", money(-it[2][1])))
            savings += it[2][1]
    symbol = shop.get("symbol", "£")
    total = round(subtotal - savings, 2)
    lines += [("rule", None, None), ("row", f"{len(items)} ITEMS", None)]
    lines += [("row", "SUBTOTAL", money(subtotal))]
    if savings:
        lines += [("row", "TOTAL SAVINGS", money(-savings))]
    if shop.get("sales_tax"):
        # US sales tax is added on top of the shelf prices.
        tax = round(total * shop["sales_tax"] / 100, 2)
        lines += [("row", f"SALES TAX {shop['sales_tax']}%", money(tax))]
        total = round(total + tax, 2)
    lines += [("big", "TOTAL", f"{symbol}{total:.2f}"), ("gap", None, None)]
    if shop.get("vat"):
        vat = round(total / 6, 2)
        lines += [("row", "VAT 20% INCLUDED", money(vat)), ("row", "NET", money(total - vat))]
    lines += [("row", shop.get("card", "VISA CONTACTLESS"), money(total)), ("gap", None, None)]
    lines += [("centre", t, None) for t in footer]
    return lines, total


def render(name, lines, out_dir, tilt, bg, shop_fill=0.80):
    draw, height = build(lines)
    paper = os.path.join(out_dir, f"_{name}_paper.png")
    photo = os.path.join(out_dir, f"{name}.jpg")
    # Thermal paper: warm off-white, slightly grey ink, faint grain.
    subprocess.run(["magick", "-size", f"{W}x{height}", "xc:#FBF9F3", "-fill", "#2B2B2E",
                    *draw,
                    "-attenuate", "0.25", "+noise", "Gaussian", "-blur", "0x0.6", paper], check=True)

    # Table top, then the receipt dropped on it with a shadow, tilt and mild perspective.
    cw, ch = 1500, 2000
    scale = min((cw * shop_fill) / W, (ch * 0.92) / height)
    pw, ph = int(W * scale), int(height * scale)
    dx = int(pw * 0.03)
    subprocess.run([
        "magick", "-size", f"{cw}x{ch}", f"gradient:{bg[0]}-{bg[1]}",
        "(", "-size", f"{cw}x{ch}", "xc:", "+noise", "Random", "-colorspace", "Gray",
        "-motion-blur", "0x40+90", "-evaluate", "multiply", "0.5", ")",
        "-compose", "Overlay", "-composite",
        "(", paper, "-background", "none", "-resize", f"{pw}x{ph}!", "-alpha", "set", "-virtual-pixel", "transparent",
        "-distort", "Perspective",
        f"0,0 {dx},0  {pw},0 {pw - dx // 2},{dx // 2}  0,{ph} 0,{ph}  {pw},{ph} {pw},{ph - dx // 3}",
        "-rotate", str(tilt),
        "(", "+clone", "-background", "black", "-shadow", "55x18+14+22", ")",
        "+swap", "-background", "none", "-layers", "merge", "+repage", ")",
        "-gravity", "center", "-compose", "Over", "-composite",
        # A little phone-camera softness and vignette.
        "(", "-size", f"{cw}x{ch}", "radial-gradient:white-#B4B4B4", ")",
        "-compose", "Multiply", "-composite", "-blur", "0x0.4", "-quality", "90", photo], check=True)
    os.remove(paper)
    return photo


RECEIPTS = {
    # Store 1: a business purchase — one-tap "Business" on camera.
    "brickwell": (
        {"name": "BRICKWELL", "vat": True,
         "header": ["BUILD & TRADE SUPPLIES", "Unit 4, Canal Wharf, Riverside",
                    "VAT No. 418 2207 61", "15/10/2026  10:24   TILL 3"]},
        [("TIMBER 47X100 2.4M", 14.40), ("SCREWS 5X60 BOX 200", 8.95),
         ("WOOD GLUE 1L", 6.49), ("MASKING TAPE 3PK", 5.25),
         ("DUST SHEET 12X9", 7.99), ("SANDPAPER ASSORTED", 4.60)],
        ["TRADE ACCOUNT  #20931", "THANK YOU FOR SHOPPING"],
        -2.5, ("#5A4030", "#3B2A1F"),
    ),
    # Store 3: a grocery shop with savings — every item and saving read back.
    "freshway": (
        {"name": "FRESHWAY", "vat": False,
         "header": ["FOOD & HOME", "12 High Street, Riverside", "15/10/2026  18:07"]},
        [("SOURDOUGH LOAF", 2.10), ("FREE RANGE EGGS 12", 3.45),
         ("SEMI SKIMMED MILK 4PT", 1.65), ("MATURE CHEDDAR 400G", 3.75, ("SAVING", 0.75)),
         ("BANANAS LOOSE", 0.98), ("OLIVE OIL 500ML", 5.20),
         ("GREEK YOGURT 500G", 2.40), ("COFFEE BEANS 1KG", 11.50, ("MEMBER PRICE", 1.50)),
         ("CHERRY TOMATOES", 1.85), ("PASTA FUSILLI 1KG", 1.60)],
        ["THANK YOU FOR SHOPPING", "AT FRESHWAY"],
        2.0, ("#C9C4BC", "#9D968D"),
    ),
}

# US versions for the US store listing: dollars, sales tax, month-first dates. The shop
# names match the UK ones so the Maestro flows wait on the same text.
RECEIPTS_US = {
    "brickwell_us": (
        {"name": "BRICKWELL", "symbol": "$", "fill": 0.88, "sales_tax": 8.25, "card": "VISA CREDIT",
         "header": ["BUILDING SUPPLY", "1420 Canal St, Riverside", "10/15/2026  10:24   REG 3"]},
        [("2X4X8 STUD", 18.84), ("WOOD SCREWS #8 1LB", 9.97),
         ("WOOD GLUE", 7.48), ("PAINTERS TAPE", 12.98),
         ("CANVAS DROP CLOTH 9X12", 14.97), ("SANDPAPER ASSORTED", 6.47)],
        ["PRO ACCOUNT  #20931", "THANK YOU FOR SHOPPING"],
        -2.5, ("#5A4030", "#3B2A1F"),
    ),
    "freshway_us": (
        {"name": "FRESHWAY", "symbol": "$", "fill": 0.88, "card": "VISA DEBIT",
         "header": ["MARKET", "212 Main Street, Riverside", "10/15/2026  6:07 PM"]},
        [("SOURDOUGH LOAF", 4.49), ("LARGE EGGS 12CT", 3.99),
         ("2% MILK 1 GAL", 3.79), ("SHARP CHEDDAR", 4.29, ("SAVINGS", 1.00)),
         ("BANANAS", 1.47), ("OLIVE OIL", 8.99),
         ("GREEK YOGURT", 5.49), ("COFFEE BEANS 2LB", 17.99, ("MEMBER PRICE", 2.00)),
         ("CHERRY TOMATOES", 3.49), ("PENNE PASTA 1LB", 1.79)],
        ["THANK YOU FOR SHOPPING", "AT FRESHWAY"],
        2.0, ("#C9C4BC", "#9D968D"),
    ),
}

if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, (shop, items, footer, tilt, bg) in {**RECEIPTS, **RECEIPTS_US}.items():
        lines, total = receipt_lines(shop, items, footer)
        print(render(name, lines, out, tilt, bg, shop.get("fill", 0.80)), f"total {shop.get('symbol', '£')}{total:.2f}")
