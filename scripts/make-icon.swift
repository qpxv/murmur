// renders the app icon into an .iconset folder. run via scripts/make-icon.sh, not by hand.
import AppKit

let outputDirectory = URL(fileURLWithPath: CommandLine.arguments[1])
let canvas: CGFloat = 1024
// apple's macos icon grid: an 824pt body with a ~185pt corner radius on a 1024pt canvas
let bodyInset: CGFloat = 100
let cornerRadius: CGFloat = 185
let glyphPointSize: CGFloat = 440
let iconSizes: [Int] = [16, 32, 128, 256, 512]

func renderMaster() -> NSImage {
    NSImage(size: NSSize(width: canvas, height: canvas), flipped: false) { rect in
        let body = rect.insetBy(dx: bodyInset, dy: bodyInset)
        let shape = NSBezierPath(roundedRect: body, xRadius: cornerRadius, yRadius: cornerRadius)
        let gradient = NSGradient(
            starting: NSColor(srgbRed: 0.42, green: 0.36, blue: 0.98, alpha: 1),
            ending: NSColor(srgbRed: 0.16, green: 0.10, blue: 0.45, alpha: 1)
        )
        gradient?.draw(in: shape, angle: -90)

        let config = NSImage.SymbolConfiguration(pointSize: glyphPointSize, weight: .semibold)
            .applying(.init(paletteColors: [.white]))
        guard
            let glyph = NSImage(systemSymbolName: "waveform", accessibilityDescription: nil)?
                .withSymbolConfiguration(config)
        else { return false }
        let glyphRect = NSRect(
            x: body.midX - glyph.size.width / 2,
            y: body.midY - glyph.size.height / 2,
            width: glyph.size.width,
            height: glyph.size.height
        )
        glyph.draw(in: glyphRect)
        return true
    }
}

func writePNG(_ image: NSImage, pixels: Int, to url: URL) throws {
    guard
        let bitmap = NSBitmapImageRep(
            bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels, bitsPerSample: 8,
            samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB,
            bytesPerRow: 0, bitsPerPixel: 0
        )
    else { throw CocoaError(.fileWriteUnknown) }
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: bitmap)
    image.draw(in: NSRect(x: 0, y: 0, width: pixels, height: pixels))
    NSGraphicsContext.restoreGraphicsState()
    guard let data = bitmap.representation(using: .png, properties: [:]) else {
        throw CocoaError(.fileWriteUnknown)
    }
    try data.write(to: url)
}

let master = renderMaster()
try FileManager.default.createDirectory(at: outputDirectory, withIntermediateDirectories: true)
for size in iconSizes {
    try writePNG(master, pixels: size, to: outputDirectory.appendingPathComponent("icon_\(size)x\(size).png"))
    try writePNG(master, pixels: size * 2, to: outputDirectory.appendingPathComponent("icon_\(size)x\(size)@2x.png"))
}
