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
