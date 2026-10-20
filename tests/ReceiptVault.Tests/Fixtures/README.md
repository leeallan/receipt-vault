Each fixture is the raw Apple Vision text output for one receipt photo: the image's pixel
size plus every recognised text run with its normalised bounding box and top corners,
captured with `VNRecognizeTextRequest` (accurate, language correction on), the same as
`Platforms/iOS/Services/OcrService.cs`.

To add a fixture from a receipt photo (macOS, runs the same Vision request as the app):

    swiftc -O tests/ReceiptVault.Tests/Tools/vision-dump.swift -o /tmp/vision-dump
    /tmp/vision-dump receipt.jpg > tests/ReceiptVault.Tests/Fixtures/<name>.json

Photos taken in the app can be pulled off a connected phone with
`xcrun devicectl device copy from --domain-type appDataContainer --domain-identifier com.farabove.receiptvault --source Library/receipt_images/<file>.jpg ...`.

`freshway-tilted.json` is the exception: its photo was generated for the promo videos
(`marketing/promo-videos/capture/receipts/make_receipts.py`), not taken on a phone.

`ikea-mlkit-*.json` are Android captures from Google ML Kit, in the same format. Debug
Android builds write one per scan (`Platforms/Android/Services/OcrService.cs`); pull it with

    adb exec-out run-as com.farabove.receiptvault ls cache/ocr-dumps
    adb exec-out run-as com.farabove.receiptvault cat cache/ocr-dumps/<file>.json > tests/ReceiptVault.Tests/Fixtures/<name>.json

ML Kit differs from Vision in ways the parser has to absorb: it reads rotated text (so
sideways photos arrive at 90°), and it glues IKEA's VAT-code digit onto the price.
