import Foundation

/// Maps raw microphone loudness to a 0...1 level for the voice bar.
public enum AudioLevel {
    // decibels of an amplitude ratio are 20 * log10(ratio)
    private static let amplitudeDecibelFactor: Float = 20

    /// Root mean square of a block of samples in -1...1.
    public static func rms(of samples: UnsafeBufferPointer<Float>) -> Float {
        guard !samples.isEmpty else { return 0 }
        let sumOfSquares = samples.reduce(Float(0)) { $0 + $1 * $1 }
        return (sumOfSquares / Float(samples.count)).squareRoot()
    }

    /// Loudness on a decibel scale, so quiet speech still visibly moves the bar.
    public static func normalized(
        rms: Float,
        floor: Float = AppConstants.Audio.silenceFloorDecibels,
        ceiling: Float = AppConstants.Audio.fullScaleDecibels
    ) -> Float {
        guard rms > 0 else { return 0 }
        let decibels = amplitudeDecibelFactor * log10(rms)
        let level = (decibels - floor) / (ceiling - floor)
        return min(max(level, 0), 1)
    }
}
