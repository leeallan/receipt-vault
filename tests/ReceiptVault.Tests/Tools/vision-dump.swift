import Vision
import AppKit
// Dumps Vision text observations as JSON, in the same shape ReceiptTextParser consumes.
let url = URL(fileURLWithPath: CommandLine.arguments[1])
let img = NSImage(contentsOf: url)!
var r = CGRect(origin: .zero, size: img.size)
let cg = img.cgImage(forProposedRect: &r, context: nil, hints: nil)!
let req = VNRecognizeTextRequest(); req.recognitionLevel = .accurate; req.usesLanguageCorrection = true
try VNImageRequestHandler(cgImage: cg).perform([req])
var toks: [[String: Any]] = []
for o in req.results ?? [] {
  guard let t = o.topCandidates(1).first?.string else { continue }
  let b = o.boundingBox
  toks.append(["text": t, "x": b.minX, "y": b.minY, "width": b.width, "height": b.height,
               "topLeftX": o.topLeft.x, "topLeftY": o.topLeft.y, "topRightX": o.topRight.x, "topRightY": o.topRight.y])
}
let out: [String: Any] = ["pixelWidth": cg.width, "pixelHeight": cg.height, "tokens": toks]
print(String(data: try JSONSerialization.data(withJSONObject: out, options: [.prettyPrinted, .sortedKeys]), encoding: .utf8)!)
