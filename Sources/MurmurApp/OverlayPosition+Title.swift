import MurmurCore

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
}
