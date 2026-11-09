import CoreGraphics

/// Where the overlay pill sits on screen: one cell of a 3x3 grid.
public enum OverlayPosition: String, Codable, CaseIterable, Sendable {
    case topLeft, topCenter, topRight
    case middleLeft, middleCenter, middleRight
    case bottomLeft, bottomCenter, bottomRight

    public enum Horizontal: Sendable { case leading, center, trailing }
    public enum Vertical: Sendable { case top, middle, bottom }

    public var horizontal: Horizontal {
        switch self {
        case .topLeft, .middleLeft, .bottomLeft: .leading
        case .topCenter, .middleCenter, .bottomCenter: .center
        case .topRight, .middleRight, .bottomRight: .trailing
        }
    }

    public var vertical: Vertical {
        switch self {
        case .topLeft, .topCenter, .topRight: .top
        case .middleLeft, .middleCenter, .middleRight: .middle
        case .bottomLeft, .bottomCenter, .bottomRight: .bottom
        }
    }

    /// Origin of a pill of `size` inside `bounds` (the screen's visible frame,
    /// bottom-left origin like AppKit), kept `margin` away from the edges.
    public func origin(for size: CGSize, in bounds: CGRect, margin: CGFloat) -> CGPoint {
        let pill = CGRect(origin: .zero, size: size)
        let x: CGFloat =
            switch horizontal {
            case .leading: bounds.minX + margin
            case .center: bounds.midX - pill.midX
            case .trailing: bounds.maxX - margin - size.width
            }
        let y: CGFloat =
            switch vertical {
            case .bottom: bounds.minY + margin
            case .middle: bounds.midY - pill.midY
            case .top: bounds.maxY - margin - size.height
            }
        return CGPoint(x: x, y: y)
    }
}
