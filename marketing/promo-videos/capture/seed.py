#!/usr/bin/env python3
"""Seed the Receipt Vault simulator database with fictional demo data for promo recordings.

Usage: seed.py <app-data-container> <mode> [uk|us]
  mode "light" - a handful of receipts this month; one is scanned on camera (Store 1).
  mode "full"  - a year of personal and business receipts, a few itemised, with budgets.
  region "uk" (default) writes pounds with 20% VAT; "us" writes dollars with sales tax.

The app must have been launched once (so the tables exist) and then terminated.
Every shop, amount and item is made up.
"""
import os
import random
import shutil
import sqlite3
import sys
from datetime import date, datetime, timedelta

EPOCH = datetime(1, 1, 1)
TODAY = date.today()
HERE = os.path.dirname(os.path.abspath(__file__))


def ticks(d):
    if isinstance(d, date) and not isinstance(d, datetime):
        d = datetime(d.year, d.month, d.day)
    return int((d - EPOCH).total_seconds()) * 10_000_000


# merchant, category, business, typical spend (low, high), visits per month
REGULARS = [
    ("Freshway", "Groceries", False, (18, 74), 5),
    ("Bean & Barrel", "Dining", False, (3.2, 9.8), 4),
    ("Riverside Fuel", "Transport", False, (38, 66), 2),
    ("Cornerstone Pharmacy", "Health", False, (4, 22), 0.6),
    ("Lumen Energy", "Utilities", False, (82, 118), 1),
    ("Harbour & Co", "Shopping", False, (15, 90), 0.8),
    ("Riverside Cinema", "Entertainment", False, (11, 32), 0.6),
    ("Brickwell", "Office", True, (12, 85), 1.2),
    ("Northline Rail", "Travel", True, (24, 96), 1.5),
    ("The Copper Kettle", "Meals & Ents", True, (16, 64), 1.2),
    ("PixelCloud", "Software", True, (12, 12), 1),
    ("Inkwell Print", "Marketing", True, (28, 140), 0.4),
    ("Hartley & Moss", "Professional", True, (180, 180), 0.25),
]

# Recent receipts shown on the dashboard in both modes (days ago, merchant, total, category, business)
RECENT = [
    (0, "Bean & Barrel", 4.60, "Dining", False),
    (1, "Northline Rail", 38.40, "Travel", True),
    (2, "Freshway", 46.12, "Groceries", False),
    (4, "PixelCloud", 12.00, "Software", True),
    (6, "Riverside Fuel", 58.30, "Transport", False),
]

FRESHWAY_ITEMS = [
    ("SOURDOUGH LOAF", 2.10, 0), ("FREE RANGE EGGS 12", 3.45, 0), ("SEMI SKIMMED MILK 4PT", 1.65, 0),
    ("MATURE CHEDDAR 400G", 3.75, 0.75), ("BANANAS LOOSE", 0.98, 0), ("OLIVE OIL 500ML", 5.20, 0),
    ("GREEK YOGURT 500G", 2.40, 0), ("COFFEE BEANS 1KG", 11.50, 1.50), ("CHERRY TOMATOES", 1.85, 0),
    ("PASTA FUSILLI 1KG", 1.60, 0), ("AVOCADOS 2PK", 2.20, 0), ("ORANGE JUICE 1L", 1.95, 0),
]

BRICKWELL_ITEMS = [
    ("TIMBER 47X100 2.4M", 14.40, 0), ("SCREWS 5X60 BOX 200", 8.95, 0), ("WOOD GLUE 1L", 6.49, 0),
    ("MASKING TAPE 3PK", 5.25, 0), ("DUST SHEET 12X9", 7.99, 0), ("SANDPAPER ASSORTED", 4.60, 0),
]


BRICKWELL_ITEMS_US = [
    ("2X4X8 STUD", 18.84, 0), ("WOOD SCREWS #8 1LB", 9.97, 0), ("WOOD GLUE", 7.48, 0),
    ("PAINTERS TAPE", 12.98, 0), ("CANVAS DROP CLOTH 9X12", 14.97, 0), ("SANDPAPER ASSORTED", 6.47, 0),
]

FRESHWAY_ITEMS_US = [
    ("SOURDOUGH LOAF", 4.49, 0), ("LARGE EGGS 12CT", 3.99, 0), ("2% MILK 1 GAL", 3.79, 0),
    ("SHARP CHEDDAR", 4.29, 1.00), ("BANANAS", 1.47, 0), ("OLIVE OIL", 8.99, 0),
    ("GREEK YOGURT", 5.49, 0), ("COFFEE BEANS 2LB", 17.99, 2.00), ("CHERRY TOMATOES", 3.49, 0),
    ("PENNE PASTA 1LB", 1.79, 0), ("AVOCADOS 2CT", 2.99, 0), ("ORANGE JUICE 52OZ", 4.29, 0),
]

# US: prices run higher in dollars, and it's gas, not fuel.
US_SCALE = 1.25
US_NAMES = {"Riverside Fuel": "Riverside Gas"}


def main(container, mode, region="uk"):
    us = region == "us"
    currency = "USD" if us else "GBP"
    freshway_items = FRESHWAY_ITEMS_US if us else FRESHWAY_ITEMS
    brickwell_items = BRICKWELL_ITEMS_US if us else BRICKWELL_ITEMS
    suffix = "_us" if us else ""

    db = sqlite3.connect(os.path.join(container, "Library", "receiptvault.db3"))
    for t in ("Receipts", "LineItems", "BudgetItems"):
        db.execute(f"DELETE FROM {t}")
    db.execute("DELETE FROM sqlite_sequence")

    images = os.path.join(container, "Library", "receipt_images")
    shutil.rmtree(images, ignore_errors=True)
    os.makedirs(images)

    rng = random.Random(7)
    rows = []  # (date, merchant, total, category, business, image, items)

    for days, merchant, total, cat, biz in RECENT:
        if us:
            merchant, total = US_NAMES.get(merchant, merchant), round(total * US_SCALE, 2)
        rows.append((TODAY - timedelta(days=days), merchant, total, cat, biz, None, None))

    if mode == "full":
        start = TODAY - timedelta(days=365)
        for merchant, cat, biz, (lo, hi), per_month in REGULARS:
            d = start
            while d < TODAY - timedelta(days=10):
                d += timedelta(days=max(1, int(rng.expovariate(per_month / 30.0))))
                if d >= TODAY - timedelta(days=10):
                    break
                total = round(rng.uniform(lo, hi) * (US_SCALE if us else 1), 2)
                rows.append((d, US_NAMES.get(merchant, merchant) if us else merchant, total, cat, biz, None, None))

        # Itemised receipts: a Freshway shop and a Brickwell trade run, both with photos.
        fw = rng.sample(freshway_items, 10)
        rows.append((TODAY - timedelta(days=9), "Freshway", round(sum(p - s for _, p, s in fw), 2),
                     "Groceries", False, f"freshway{suffix}.jpg", fw))
        bw_total = sum(p for _, p, _ in brickwell_items) * (1.0825 if us else 1)
        rows.append((TODAY - timedelta(days=8), "Brickwell", round(bw_total, 2),
                     "Office", True, f"brickwell{suffix}.jpg", brickwell_items))

        for cat, limit, biz in [("Groceries", 320, False), ("Dining", 60, False), ("Transport", 150, False),
                                ("Travel", 200, True), ("Software", 40, True)]:
            db.execute("INSERT INTO BudgetItems (Category, MonthlyLimit, IsBusinessBudget) VALUES (?,?,?)",
                       (cat, round(limit * (US_SCALE if us else 1)), int(biz)))

    rows.sort(key=lambda r: r[0])
    for d, merchant, total, cat, biz, image, items in rows:
        stamp = datetime(d.year, d.month, d.day, rng.randint(8, 19), rng.randint(0, 59))
        if image:
            shutil.copy(os.path.join(HERE, "receipts", image), os.path.join(images, image))
        # Business receipts carry tax: 20% VAT included in UK totals, 8.25% sales tax added
        # on top in the US.
        tax = (round(total - total / 1.0825, 2) if us else round(total / 6, 2)) if biz else None
        cur = db.execute(
            "INSERT INTO Receipts (Merchant, Total, Tax, Currency, Date, Category, IsBusinessExpense, ImagePath,"
            " CreatedAt, UpdatedAt) VALUES (?,?,?,?,?,?,?,?,?,?)",
            (merchant, total, tax, currency, ticks(d), cat, int(biz), image, ticks(stamp), ticks(stamp)))
        for desc, price, saving in items or []:
            db.execute("INSERT INTO LineItems (ReceiptId, Description, Quantity, Price, Savings, Currency)"
                       " VALUES (?,?,?,?,?,?)", (cur.lastrowid, desc, 1, price, saving, currency))

    db.commit()
    print(f"seeded {len(rows)} receipts ({mode}, {region})")


if __name__ == "__main__":
    main(*sys.argv[1:4])
