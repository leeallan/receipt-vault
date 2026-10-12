# Receipt Vault — App Store Listing Copy

Ready-to-paste copy for App Store Connect. Character limits noted in (brackets).

---

## Search fields — v1.0.1 (ASO rework, 2026-10-11)
Apple indexes **title + subtitle + keywords** together per localisation, so never repeat a
word across the three (wasted characters). Plurals are handled automatically.
Each storefront reads its own localisation: UK store → English (UK); US store → English (US)
**and** Spanish (Mexico), which gives US a second 100-char keyword field.

| Field | English (UK) | English (US) | Spanish (Mexico) — US extra keywords |
|---|---|---|---|
| App Name (30) | `Receipt Vault: Receipt Scanner` | same | same |
| Subtitle (30) | `Expense Tracker for Tax & VAT` | `Expense Tracker & Organizer` | `Paperless Expense Reports` |

Keywords (100, comma-separated, no spaces):

English (UK) (99):
```
organiser,keeper,ocr,itemised,hmrc,self,assessment,bookkeeping,warranty,sole,trader,spending,budget
```
English (US) (97):
```
organizer,keeper,ocr,itemized,taxes,write,off,bookkeeping,warranty,freelancer,spending,budget,irs
```
Spanish (Mexico) (97):
```
small,business,expenses,reimbursement,claim,accountant,cpa,deduction,1099,shoebox,digital,storage
```

<details><summary>v1.0 (superseded)</summary>

- Name: `Receipt Vault: Scan & Export` · Subtitle: `Snap, sort & export receipts`
- Keywords: `expense,tax,VAT,spending,budget,scanner,tracker,business,invoice,itemised,itemized,export,finance`
- Problem: ranked outside the top ~180 on every generic term (receipt scanner/tracker/organizer,
  expense tracker, scan receipts, vat receipts) in both UK and US; ~19 other apps share the
  "Receipt Vault" name so the brand term alone is worth little.
</details>

## Promotional Text (170) — editable anytime without review
```
Scan receipts in a tap — every line item captured, all on your device. Export a plain CSV free, or unlock formatted spreadsheets, PDFs & tax reports with one purchase.
```

## Keywords (100, comma-separated, no spaces)
```
expense,tax,VAT,spending,budget,scanner,tracker,business,invoice,itemised,itemized,export,finance
```

## Description (4000)
```
Receipt Vault turns the pile of paper receipts in your bag, pocket and glovebox
into a tidy, searchable record — and it all happens on your device.

Point your camera at a receipt and Receipt Vault reads the merchant, total and
date for you. No typing, no filing, no lost paper. It even captures the individual
items you bought, so you know exactly where your money went.

SCAN ANYTHING
• On-device text recognition — receipts are never uploaded to be read
• Auto-captures merchant, total and date
• Handles long, multi-column supermarket receipts

STAY ORGANISED
• Tag receipts as business or personal, add categories and notes
• Search across everything in a tap
• A clean dashboard of your spending, month by month

PRIVATE BY DESIGN
• Everything stays on your device — no account, no sign-up, nothing uploaded to us
• We run no servers and never see your receipts

EXPORT YOUR WAY
• Free: export a plain CSV for any date range
• Premium: a colour-formatted Excel spreadsheet with itemised line-item rows,
  polished PDF exports, and business-expense & tax summary reports — ready for
  bookkeeping, expense claims or your accountant

ONE SIMPLE PRICE
Scanning and organising are free forever. Unlock Premium with a single one-time
purchase — no subscription, no ads, ever. Buy once, keep every premium feature,
including future additions.

Receipt Vault is not accounting or tax advice — always check the scanned details.
```

## What's New (version 1.0.1)
```
Small improvements and fixes. Enjoying Receipt Vault? A quick rating really helps an independent app get found — thank you!
```

---

## In-App Purchase metadata (product: com.farabove.receiptvault.premium.lifetime)
- **Reference Name** (internal): `Premium — Lifetime Unlock`
- **Display Name** (shown to users): `Receipt Vault Premium`
- **Description**:
```
Unlock automatic item scanning, colour-formatted spreadsheet & PDF export, and tax & expense reports. One-time purchase, no subscription.
```
- **Type**: Non-Consumable
- **Price**: £4.99 (UK) / equivalent tier in other regions

---

## App information / URLs
| Field | Value |
|---|---|
| Primary category | Finance |
| Secondary category | Productivity |
| Age rating | 4+ |
| Privacy Policy URL | https://receiptvault.farabove.uk/privacy |
| Support URL | https://receiptvault.farabove.uk/help |
| Marketing URL | https://receiptvault.farabove.uk |
| Copyright | © 2026 Far Above Studios |
| App Privacy (data collection) | **Data Not Collected** — nothing leaves the device; Apple handles purchases |
| Export compliance | Standard/exempt encryption only → answer "No" to non-exempt encryption (or set `ITSAppUsesNonExemptEncryption=false` in Info.plist) |

---

## Screenshot headlines (for reference — baked into the framed images)
**Upload order matters:** only the first 3 show in search results. Upload `framed/01-title.png`
first, then 02 → 07 in filename order. (v1.0 went live in the wrong order: 04, 03, 02, 07, 06, 01, 05.)

1. **Title/hero:** Receipt Vault · "Scan · Organise · Export" · "Never lose a receipt again"
2. **Dashboard:** Never lose / a receipt
3. **Items:** Every item, / captured
4. **Export:** Ready for your / accountant
5. **Receipts:** Organised & / searchable
6. **Budget:** Stay on / budget
7. **Premium:** One price. / No subscription.

Assets:
- Icon: `store-assets/AppStore-icon-1024.png`
- iPhone 6.9" framed: `store-assets/screenshots/framed/`
- iPad 12.9" framed: `store-assets/screenshots-ipad/framed/`
