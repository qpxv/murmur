import MurmurCore
import Testing

struct AudioLevelTests {
    @Test func rmsOfConstantSignalIsItsMagnitude() {
        let samples: [Float] = [0.5, -0.5, 0.5, -0.5]
        let rms = samples.withUnsafeBufferPointer { AudioLevel.rms(of: $0) }
        #expect(rms == 0.5)
    }

    @Test func rmsOfNoSamplesIsZero() {
        let rms = [Float]().withUnsafeBufferPointer { AudioLevel.rms(of: $0) }
        #expect(rms == 0)
    }

    @Test func detectsClippingOnEitherSide() {
        let clean: [Float] = [0.2, -0.5, 0.9]
        let positive: [Float] = [0.2, 1.0, 0.1]
        let negative: [Float] = [-0.995, 0.1]
        #expect(!clean.withUnsafeBufferPointer { AudioLevel.isClipped($0, threshold: 0.99) })
        #expect(positive.withUnsafeBufferPointer { AudioLevel.isClipped($0, threshold: 0.99) })
        #expect(negative.withUnsafeBufferPointer { AudioLevel.isClipped($0, threshold: 0.99) })
    }

    @Test func silenceIsEmptyAndLoudIsFull() {
        #expect(AudioLevel.normalized(rms: 0, floor: -60, ceiling: -20) == 0)
        #expect(AudioLevel.normalized(rms: 0.0001, floor: -60, ceiling: -20) == 0)
        #expect(AudioLevel.normalized(rms: 1, floor: -60, ceiling: -20) == 1)
    }

    @Test func midpointOfDecibelRangeIsHalf() {
        // -40 dBFS is halfway between -60 and -20
        let level = AudioLevel.normalized(rms: 0.01, floor: -60, ceiling: -20)
        #expect(abs(level - 0.5) < 0.0001)
    }
}
