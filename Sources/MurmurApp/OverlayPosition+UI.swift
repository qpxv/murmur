import MurmurCore
import SwiftUI

extension OverlayPosition {
    var title: String {
        switch self {
        case .topLeft: "Top Left"
        case .topCenter: "Top Center"
        case .topRight: "Top Right"
        case .middleLeft: "Middle Left"
        case .middleCenter: "Middle Center"
        case .middleRight: "Middle Right"
        case .bottomLeft: "Bottom Left"
        case .bottomCenter: "Bottom Center"
        case .bottomRight: "Bottom Right"
        }
    }

    /// Where the pill sits inside its cell, so it hugs the edge it's anchored to and grows away from it.
    var alignment: Alignment {
        let horizontal: HorizontalAlignment =
            switch horizontal {
            case .leading: .leading
            case .center: .center
            case .trailing: .trailing
            }
        let vertical: VerticalAlignment =
            switch vertical {
            case .top: .top
            case .middle: .center
            case .bottom: .bottom
            }
        return Alignment(horizontal: horizontal, vertical: vertical)
    }
}
