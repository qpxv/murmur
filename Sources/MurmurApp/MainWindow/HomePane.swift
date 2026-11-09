import MurmurCore
import MurmurServices
import SwiftData
import SwiftUI

/// Dashboard: totals, time saved against the user's typing speed, and the latest dictations.
struct HomePane: View {
    @Bindable var settingsStore: SettingsStore
    @Query(sort: \DictationRecord.date, order: .reverse) private var records: [DictationRecord]

    var body: some View {
        let stats = DictationStats(
            summaries: records.map(\.summary),
            typingWordsPerMinute: settingsStore.settings.typingWordsPerMinute,
            now: .now,
            calendar: .current
        )
        ScrollView {
            VStack(alignment: .leading, spacing: Theme.Window.sectionSpacing) {
                statsGrid(stats)
                typingSpeed
                recent
            }
            .padding(Theme.Window.contentPadding)
        }
        .navigationTitle("Home")
    }

    private func statsGrid(_ stats: DictationStats) -> some View {
        LazyVGrid(
            columns: [GridItem(.adaptive(minimum: Theme.StatCard.minWidth), spacing: Theme.StatCard.spacing)],
            spacing: Theme.StatCard.spacing
        ) {
            StatCard(title: "Words today", value: stats.wordsToday.formatted(), symbol: "sun.max")
            StatCard(title: "Words in total", value: stats.totalWords.formatted(), symbol: "text.word.spacing")
            StatCard(title: "Time saved", value: Self.duration(stats.timeSavedSeconds), symbol: "hourglass")
            StatCard(
                title: "Speaking speed",
                value: stats.speakingWordsPerMinute.map { "\($0) WPM" } ?? "–",
                symbol: "speedometer"
            )
            StatCard(title: "Dictations", value: stats.dictationCount.formatted(), symbol: "waveform")
            StatCard(title: "Day streak", value: stats.streakDays.formatted(), symbol: "flame")
        }
    }

    private var typingSpeed: some View {
        HStack {
            Text("Your typing speed")
            Spacer()
            TextField(
                "WPM",
                value: $settingsStore.settings.typingWordsPerMinute,
                format: .number
            )
            .frame(width: Theme.Typing.fieldWidth)
            .multilineTextAlignment(.trailing)
            Stepper("", value: $settingsStore.settings.typingWordsPerMinute, in: Theme.Typing.speedRange)
                .labelsHidden()
            Text("WPM").foregroundStyle(.secondary)
        }
        .help("Used for “Time saved”: how long typing the same words would have taken")
    }

    @ViewBuilder private var recent: some View {
        Text("Recent").font(.headline)
        if records.isEmpty {
            ContentUnavailableView(
                "No dictations yet",
                systemImage: "waveform",
                description: Text("Hold fn and start talking. Your dictations show up here.")
            )
            .frame(maxWidth: .infinity)
        } else {
            VStack(alignment: .leading) {
                ForEach(records.prefix(Theme.Window.recentCount)) { record in
                    DictationRow(record: record)
                    Divider()
                }
            }
        }
    }

    static func duration(_ seconds: Double) -> String {
        Duration.seconds(seconds).formatted(
            .units(
                allowed: [.hours, .minutes, .seconds],
                width: .abbreviated,
                maximumUnitCount: Theme.Window.durationUnitCount
            )
        )
    }
}
