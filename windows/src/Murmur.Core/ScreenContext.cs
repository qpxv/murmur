namespace Murmur.Core;

/// <summary>
/// What was on screen when a dictation started: the text Claude gets as context, and the unusual
/// names in it that Deepgram boosts as key terms.
/// </summary>
public sealed class ScreenContext
{
    private const int ShortestTerm = 2;
    private const int LongestTerm = 40;
    private const int MinimumDomainPart = 2;
    private const string UrlScheme = "://";
    private static readonly char[] SentenceEnds = ['.', '!', '?', ':', '…'];
    private static readonly string[] Possessives = ["'s", "’s"];

    private ScreenContext(string text, IReadOnlyList<string> terms)
    {
        Text = text;
        Terms = terms;
    }

    public static ScreenContext Empty { get; } = new("", []);

    /// <summary>The end of the screen text (the newest part of a chat sits at the bottom), capped.</summary>
    public string Text { get; }

    /// <summary>Names and terms worth boosting, most frequent first, capped.</summary>
    public IReadOnlyList<string> Terms { get; }

    /// <summary>
    /// <paramref name="lines"/> are the texts of the window's elements, top to bottom. A plain
    /// capitalized word only counts as a name in English and when it doesn't start a sentence (in
    /// German every noun is capitalized; the mac app asks its dictionary instead).
    /// </summary>
    public static ScreenContext From(IEnumerable<string> lines, IReadOnlyList<string> vocabulary, DictationLanguage language)
    {
        var kept = lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        var joined = string.Join('\n', kept);
        var text = joined.Length > AppConstants.Screen.MaxCharacters ? joined[^AppConstants.Screen.MaxCharacters..] : joined;
        var taken = new HashSet<string>(vocabulary, StringComparer.OrdinalIgnoreCase);
        var terms = kept
            .SelectMany(line => Candidates(line, language))
            // most frequent first; ties keep the order they appeared in, one spelling per word (the first seen)
            .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .Where(word => !taken.Contains(word))
            .Take(AppConstants.Screen.MaxKeyterms)
            .ToList();
        return new ScreenContext(text, terms);
    }

    private static IEnumerable<string> Candidates(string line, DictationLanguage language)
    {
        var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < tokens.Length; index++)
        {
            var startsSentence = index == 0 || SentenceEnds.Contains(tokens[index - 1][^1]);
            var word = Cleaned(tokens[index]);
            if (word.Length is >= ShortestTerm and <= LongestTerm && IsTerm(word, startsSentence, language))
            {
                yield return word;
            }
        }
    }

    // "(blogr.ai)," -> "blogr.ai", "https://blogr.ai/pricing" -> "blogr.ai", "Mia's" -> "Mia"
    private static string Cleaned(string token)
    {
        var schemeAt = token.IndexOf(UrlScheme, StringComparison.Ordinal);
        var host = schemeAt < 0 ? token : token[(schemeAt + UrlScheme.Length)..].Split('/')[0];
        var start = 0;
        var end = host.Length;
        while (start < end && IsWrapping(host[start]))
        {
            start++;
        }
        while (end > start && IsWrapping(host[end - 1]))
        {
            end--;
        }
        var trimmed = host[start..end];
        var possessive = Possessives.FirstOrDefault(ending => trimmed.EndsWith(ending, StringComparison.Ordinal));
        return possessive is null ? trimmed : trimmed[..^possessive.Length];
    }

    // c# and c++ keep their sign
    private static bool IsWrapping(char character) =>
        (char.IsPunctuation(character) || char.IsSymbol(character)) && character is not ('+' or '#');

    private static bool IsTerm(string word, bool startsSentence, DictationLanguage language)
    {
        if (!word.Any(char.IsLetter))
        {
            return false;
        }
        if (IsDomain(word) || HasInnerCapital(word) || MixesLettersAndDigits(word) || IsAcronym(word))
        {
            return true;
        }
        return IsCapitalized(word) && language == DictationLanguage.English && !startsSentence;
    }

    // blogr.ai, but not e.g or a sentence glued to the next one ("done.Then" has a capital)
    private static bool IsDomain(string word)
    {
        var parts = word.Split('.');
        return parts.Length > 1
            && parts.All(part => part.Length >= MinimumDomainPart && part.All(character => char.IsLetterOrDigit(character) || character == '-'))
            && !word.Any(char.IsUpper);
    }

    // iPhone, GitHub, McKenzie
    private static bool HasInnerCapital(string word) => word.Skip(1).Any(char.IsUpper) && word.Any(char.IsLower);

    // GPT4, M3, 4K, but not 10am or 2nd
    private static bool MixesLettersAndDigits(string word) =>
        word.Any(char.IsDigit) && word.Any(char.IsUpper) && word.All(char.IsLetterOrDigit);

    // API, AWS
    private static bool IsAcronym(string word) => word.All(character => char.IsLetter(character) && char.IsUpper(character));

    private static bool IsCapitalized(string word) =>
        char.IsUpper(word[0]) && word.Skip(1).All(character => char.IsLower(character) || character == '-');
}
