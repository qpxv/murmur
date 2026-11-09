import SwiftUI

/// One dashboard number with its label.
struct StatCard: View {
    let title: String
    let value: String
    let symbol: String

    var body: some View {
        VStack(alignment: .leading, spacing: Theme.StatCard.valueSpacing) {
            Label(title, systemImage: symbol)
                .font(.subheadline)
                .foregroundStyle(.secondary)
            Text(value)
                .font(.title.weight(.semibold))
                .monospacedDigit()
                .contentTransition(.numericText())
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Theme.StatCard.padding)
        .background(
            .quaternary.opacity(Theme.StatCard.backgroundOpacity),
            in: .rect(cornerRadius: Theme.StatCard.cornerRadius)
        )
    }
}
