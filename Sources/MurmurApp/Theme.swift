import SwiftUI

/// Single source of truth for sizes and timings of the app's own UI.
enum Theme {
    enum Overlay {
        /// The invisible, click-through canvas the pill sits in; large enough for its widest state.
        static let canvasWidth: CGFloat = 380
        static let canvasHeight: CGFloat = 64
        static let canvasSize = CGSize(width: canvasWidth, height: canvasHeight)
        /// Distance from the screen's visible edges (menu bar and Dock excluded).
        static let screenMargin: CGFloat = 12
        static let idleWidth: CGFloat = 46
        static let idleHeight: CGFloat = 24
        static let idleIconSize: CGFloat = 11
        static let pillHeight: CGFloat = 30
        // dynamic island look: near-solid black, a hairline edge so it still reads on dark wallpapers
        static let backgroundOpacity: Double = 0.94
        static let edgeOpacity: Double = 0.12
        static let background = Color.black.opacity(backgroundOpacity)
        static let foreground = Color.white
        static let edge = Color.white.opacity(edgeOpacity)
        // a full point: a 0.5pt hairline falls between pixels on 1x displays and partly vanishes
        static let edgeWidth: CGFloat = 1
        static let horizontalPadding: CGFloat = 14
        static let contentSpacing: CGFloat = 8
        static let morphDuration: Double = 0.28
    }

    enum VoiceBar {
        static let barWidth: CGFloat = 2
        static let barSpacing: CGFloat = 2
        static let minBarHeight: CGFloat = 2
        static let maxBarHeight: CGFloat = 15
        static let levelAnimation: Double = 0.08
    }

    enum Settings {
        static let windowWidth: CGFloat = 460
    }
}
