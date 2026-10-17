# Receipt Vault promo videos

Twenty-seven video files built in Remotion from real screen recordings of the app: 3 store videos in three sizes each, and 6 social clips with three hooks each. All shops, people and amounts are fictional demo data.

| Video | What it shows | Length | Files |
|---|---|---|---|
| Store 1: Snap → done | Pick a receipt photo; shop, total and date are read automatically; file it as a business expense; the dashboard updates | 17.7s | AppStore / Social / Landscape |
| Store 2: A year of receipts | The full list, totals by month, budgets, business filter, search, the original photo | 18.8s | AppStore / Social / Landscape |
| Store 3: Premium | Every item and saving read off a receipt, itemised spreadsheet export, tax & expense report | 16.9s | AppStore / Social / Landscape |
| S1: POV | Work messages asking for receipts pile up, then search finds it | 12s | hooks A/B/C |
| S2: Fading receipt | A thermal receipt fades to blank, then a scan keeps it | 11.4s | hooks A/B/C |
| S3: Things nobody tells you | Record-keeping rules for the self-employed (source shown on screen), then the vault | 14.4s | hooks A/B/C |
| S4: Guess the number | The demo account's grocery total, then the tax report that adds it up | 12.4s | hooks A/B/C |
| S5: Group chat | "Who paid for what?" on a group holiday, then search | 12.6s | hooks A/B/C |
| S6: Tax season | Export a date range, the tax & expense report, every business expense listed | 14.2s | hooks A/B/C |

The finished files are in `out/store` and `out/social`.

## Where each file goes

- **App Store app previews:** `receiptvault-Store1-AppStore.mp4`, and Store 2 and 3 if you want all three slots. These are 886×1920, 30fps, 15–30s long, with a silent stereo track.
  - They contain only footage from the app and show no prices, because prices vary by storefront.
  - Pick a poster frame that shows the app, not the end card.
  - Previews can only be added to a new app version, so they ship with the next update.
- **Google Play:** upload `receiptvault-Store1-Landscape.mp4` to YouTube (unlisted is fine) and paste the link in Play Console under Main store listing → Video.
- **TikTok / Reels / Shorts:** the `S*` files plus the `Store*-Social` cuts.
  - They're silent on purpose. Add a trending sound in the app when you post.
  - Post the A/B/C hooks of the same clip a few days apart. After about 48 hours, compare how many viewers stayed past 3 seconds and how many watched to the end, and make more hooks like the winner.
- **S6** is seasonal: post it from late December to mid January, before the 31 January Self Assessment deadline. A US "Tax Day, April 15" version is a hook change away.

## Re-making the footage

The footage and generated receipt photos are not in git, because they're large and can be regenerated. Any app UI change means re-recording.

```bash
# 0. Generate the fictional receipt photos and add them to the simulator's library
#    (Freshway must be the 1st photo in the picker, Brickwell the 2nd)
SIM=86764DF5-79EF-4020-8D7B-894C581FF504   # iPhone 17 Pro Max
python3 -I marketing/promo-videos/capture/receipts/make_receipts.py marketing/promo-videos/capture/receipts
xcrun simctl addmedia $SIM marketing/promo-videos/capture/receipts/{freshway,brickwell}.jpg

# 1. Build a Release simulator app and install it
dotnet build ReceiptVault.csproj -f net10.0-ios -c Release -p:RuntimeIdentifier=iossimulator-arm64
xcrun simctl install $SIM bin/Release/net10.0-ios/iossimulator-arm64/ReceiptVault.app
xcrun simctl launch $SIM com.farabove.receiptvault   # once, so it creates its database

# 2. Record each flow (needs Maestro: brew install mobile-dev-inc/tap/maestro)
cd marketing/promo-videos/capture
./record.sh store1 light
./record.sh store2 full
./record.sh store3 full premium
./record.sh social_tax full premium

# 3. Convert to constant 30fps for Remotion
cd ../studio
for n in store1 store2 store3 social_tax; do
  mv public/footage/$n.mp4 ../capture/raw/$n.mp4
  npx remotion ffmpeg -y -i ../capture/raw/$n.mp4 -r 30 -fps_mode cfr -c:v libx264 -crf 18 -pix_fmt yuv420p -an public/footage/$n.mp4
done
```

- `capture/seed.py` writes the demo data: a handful of recent receipts (`light`), or a year of personal and business receipts with VAT, two itemised receipts with photos, and budgets (`full`).
- `capture/reset.sh` loads that data, sets Free or Premium, suppresses the rating prompt and cleans the status bar.
- The receipt photos are real input: the app's on-device text recognition reads them on camera. They're tuned to pass the app's "is this a receipt?" check.
- The scene timings (`trim` in `studio/src/videos/*.tsx`) are seconds into each recording, found from exact frames (`npx remotion ffmpeg -ss <t> -i … -frames:v 1`). If you re-record, check them again.
- `studio/public/tax-report.png` is page 1 of the tax report the app exported, rendered with `qlmanage -t -s 2400` from the app's `Library/Caches/exports` folder. S4 and S6 show it because the Preview footage is too small to read on a phone.

## Editing and rendering

```bash
cd studio
npm run dev        # Remotion Studio: preview and tweak
./render.sh        # render all 27 files to ../out
```

- Captions: wrap words in `*asterisks*` to highlight them.
- Hook variants: change the `hook` texts in `src/Root.tsx`.

## Gotchas

- **Release builds only on the simulator.** A stale incremental Release build can crash at launch in `load_aot_module`. Rebuild from scratch (see the deploy notes) if the app dies straight away.
- **Maestro on iOS:**
  - Tap percentages must be whole numbers; use points (`"220,789"`, screen is 440×956) for anything finer.
  - The share sheet's "Preview" label can't be tapped by text; tap it by point.
  - A tap on the iOS photo picker or share sheet can't be followed by text checks, so the flows wait on what the app shows next.
- **The "◀ Preview" link:** opening an export in Preview leaves a back link in the status bar until Preview is closed. `reset.sh` closes it.
- **The XLSX share sheet** is only on screen for about half a second in `store3`; Store 3 plays it in slow motion.
- **Sources:** the S3 record-keeping lines come from GOV.UK, "Business records if you're self-employed" (keep records 5 years after the 31 January deadline; paper or digital; penalties for unreadable records). Keep the source line if you reuse them. S2 makes no numeric claim about fading, because no solid study gives a timeline.
- **Licence:** Remotion is free for individuals and companies with up to 3 people.
