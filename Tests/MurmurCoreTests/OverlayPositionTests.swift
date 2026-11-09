import CoreGraphics
import MurmurCore
import Testing

struct OverlayPositionTests {
    private let screen = CGRect(x: 0, y: 0, width: 1000, height: 600)
    private let pill = CGSize(width: 100, height: 40)
    private let margin: CGFloat = 20

    @Test(arguments: [
        (OverlayPosition.bottomLeft, CGPoint(x: 20, y: 20)),
        (.bottomCenter, CGPoint(x: 450, y: 20)),
        (.bottomRight, CGPoint(x: 880, y: 20)),
        (.middleLeft, CGPoint(x: 20, y: 280)),
        (.middleCenter, CGPoint(x: 450, y: 280)),
        (.middleRight, CGPoint(x: 880, y: 280)),
        (.topLeft, CGPoint(x: 20, y: 540)),
        (.topCenter, CGPoint(x: 450, y: 540)),
        (.topRight, CGPoint(x: 880, y: 540)),
    ])
    func placesPillInItsCell(position: OverlayPosition, expected: CGPoint) {
        #expect(position.origin(for: pill, in: screen, margin: margin) == expected)
    }

    @Test func respectsOffsetScreenFrame() {
        let secondScreen = CGRect(x: 1000, y: 50, width: 800, height: 500)
        let origin = OverlayPosition.bottomRight.origin(for: pill, in: secondScreen, margin: margin)
        #expect(origin == CGPoint(x: 1680, y: 70))
    }
}
