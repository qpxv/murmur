import SwiftUI

/// One permission or system setting Murmur depends on, with a shortcut to fix it.
struct PermissionRow: View {
    let title: String
    let isGranted: Bool
    let action: @MainActor () -> Void

    var body: some View {
        HStack {
            Image(systemName: isGranted ? "checkmark.circle.fill" : "exclamationmark.circle.fill")
                .foregroundStyle(isGranted ? .green : .orange)
            Text(title)
            Spacer()
            if !isGranted {
                Button("Open Settings…", action: action)
            }
        }
    }
}
