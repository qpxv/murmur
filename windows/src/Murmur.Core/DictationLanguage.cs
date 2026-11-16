namespace Murmur.Core;

/// <summary>The language the user speaks, which picks Deepgram's model language.</summary>
public enum DictationLanguage
{
    English,
    German,
}

public static class DictationLanguages
{
    public static string DeepgramCode(this DictationLanguage language) => language switch
    {
        DictationLanguage.German => AppConstants.Deepgram.GermanLanguageCode,
        _ => AppConstants.Deepgram.EnglishLanguageCode,
    };

    public static string Title(this DictationLanguage language) => language switch
    {
        DictationLanguage.German => "German",
        _ => "English",
    };
}
