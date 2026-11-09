import Foundation
import MurmurCore
import Testing

struct CleanupOptionsTests {
    @Test func defaultPromptHasEveryFixAndKeepsWording() {
        let prompt = CleanupPrompt.system(for: .defaults)
        #expect(prompt.contains("misheard"))
        #expect(prompt.contains("punctuation"))
        #expect(prompt.contains("stutters"))
        #expect(prompt.contains("Keep everything else exactly as spoken"))
        #expect(prompt.contains("<previous>"))
    }

    @Test func disabledFixesAreLeftOut() {
        var options = CleanupOptions.defaults
        options.fixesPunctuation = false
        options.removesStutters = false
        let prompt = CleanupPrompt.system(for: options)
        #expect(prompt.contains("misheard"))
        #expect(!prompt.contains("Fix punctuation"))
        #expect(!prompt.contains("stutters"))
    }

    @Test func casualAsksForLowercaseAndNoCommas() {
        var options = CleanupOptions.defaults
        options.style = .casual
        let prompt = CleanupPrompt.system(for: options)
        #expect(prompt.contains("lowercase"))
        #expect(prompt.contains("no commas"))
        #expect(!prompt.contains("Style: formal"))
    }

    @Test func formalAllowsRephrasingButNotNewContent() {
        var options = CleanupOptions.defaults
        options.style = .formal
        let prompt = CleanupPrompt.system(for: options)
        #expect(prompt.contains("Style: formal"))
        #expect(prompt.contains("add nothing new"))
        #expect(!prompt.contains("Keep everything else exactly as spoken"))
    }

    @Test func styleAloneStillNeedsClaude() {
        var options = CleanupOptions.defaults
        options.fixesMisheardWords = false
        options.fixesPunctuation = false
        options.removesStutters = false
        #expect(!options.hasWork)
        options.style = .casual
        #expect(options.hasWork)
        #expect(!CleanupPrompt.system(for: options).contains("Make these fixes"))
    }

    @Test func missingKeysDecodeToDefaults() throws {
        let options = try JSONDecoder().decode(CleanupOptions.self, from: Data(#"{"style":"formal"}"#.utf8))
        var expected = CleanupOptions.defaults
        expected.style = .formal
        #expect(options == expected)
    }
}
