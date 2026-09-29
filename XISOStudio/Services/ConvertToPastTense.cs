namespace XISOStudio.Services;

/// <summary>
/// Converts verbs to their past-tense form for user-facing status messages.
/// </summary>
public static class ConvertToPastTense
{
    /// <summary>
    /// Returns the past-tense form of the specified verb using standard English rules.
    /// </summary>
    /// <param name="verb">Verb to convert (for example, "move" or "test").</param>
    /// <returns>The past-tense form of <paramref name="verb"/>.</returns>
    public static string GetPastTense(string verb)
    {
        var lower = verb.ToLowerInvariant();

        return lower switch
        {
            "conversion" => "converted",
            "test" => "tested",
            _ => ApplyStandardRules(lower)
        };
    }

    private static string ApplyStandardRules(string verb)
    {
        if (verb.Length == 0) return "ed";

        // Verbs ending in "e" → just add "d" (move → moved, create → created)
        if (verb.EndsWith('e'))
            return verb + "d";

        // Verbs ending in consonant + "y" → change "y" to "ied" (copy → copied, carry → carried)
        if (verb.EndsWith('y') && verb.Length >= 2 && IsConsonant(verb[^2]))
            return verb[..^1] + "ied";

        // Default: append "ed"
        return verb + "ed";
    }

    private static bool IsConsonant(char c)
    {
        return c is not ('a' or 'e' or 'i' or 'o' or 'u');
    }
}