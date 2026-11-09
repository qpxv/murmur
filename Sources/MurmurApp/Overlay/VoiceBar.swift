import MurmurCore
import SwiftUI

/// Bars that follow the microphone level, newest on the right.
struct VoiceBar: View {
    let levels: [Float]

    var body: some View {
        HStack(spacing: Theme.VoiceBar.barSpacing) {
            ForEach(levels.indices, id: \.self) { index in
                Capsule()
                    .fill(.primary)
                    .frame(width: Theme.VoiceBar.barWidth, height: height(for: levels[index]))
            }
        }
        .animation(.linear(duration: Theme.VoiceBar.levelAnimation), value: levels)
    }

    private func height(for level: Float) -> CGFloat {
        let range = Theme.VoiceBar.maxBarHeight - Theme.VoiceBar.minBarHeight
        return Theme.VoiceBar.minBarHeight + range * CGFloat(level)
    }
}
