Each fixture is the raw Apple Vision text output for one receipt photo: the image's pixel
size plus every recognised text run with its normalised bounding box and top corners,
captured with `VNRecognizeTextRequest` (accurate, language correction on), the same as
`Platforms/iOS/Services/OcrService.cs`.

To add a fixture from a receipt photo (macOS, runs the same Vision request as the app):

    swiftc -O tests/ReceiptVault.Tests/Tools/vision-dump.swift -o /tmp/vision-dump
    /tmp/vision-dump receipt.jpg > tests/ReceiptVault.Tests/Fixtures/<name>.json

Photos taken in the app can be pulled off a connected phone with
`xcrun devicectl device copy from --domain-type appDataContainer --domain-identifier com.farabove.receiptvault --source Library/receipt_images/<file>.jpg ...`.
