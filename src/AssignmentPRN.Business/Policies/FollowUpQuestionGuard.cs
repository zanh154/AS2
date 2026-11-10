using System.Globalization;
using System.Text;
using AssignmentPRN.Business.Interfaces;

namespace AssignmentPRN.Business.Policies;

/// <summary>
/// Last check on a probe the model wrote, before it is read aloud to the student.
/// <para>
/// The prompt already tells the model not to give the answer away and not to repeat
/// itself, but a small local model does not always listen. A probe that slips the
/// expected answer to the student, or asks the very same thing twice, is worse than no
/// probe: the first hands out marks, the second burns the student's time. Either way
/// the room simply moves on, exactly as when the model is down.
/// </para>
/// <para>
/// Pure: takes strings, touches no repository and no model, so it is tested on its own.
/// </para>
/// </summary>
public static class FollowUpQuestionGuard
{
    /// <summary>
    /// How many consecutive words a probe may borrow from the expected answer before it
    /// counts as giving it away. Vietnamese words are mostly one syllable, so five
    /// tokens is a phrase of two or three words: long enough not to trip on ordinary
    /// glue ("của các đối tượng"), short enough to catch a definition read out.
    /// </summary>
    internal const int LeakRunLength = 5;

    /// <summary>
    /// Word overlap above which two probes are the same question in other words.
    /// </summary>
    internal const double RepeatSimilarity = 0.8;

    /// <summary>Whether the probe may be asked: it neither gives the answer away nor repeats itself.</summary>
    public static bool IsUsable(string probe, FollowUpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(probe)) return false;

        // Wording the student already heard or said is fair to reuse: quoting the
        // question back, or "em vừa nói X, ...", gives nothing away.
        var known = new List<string?> { context.QuestionText, context.Transcript, context.OriginalAnswer };
        known.AddRange(context.PreviousProbes);
        foreach (var exchange in context.Exchanges ?? [])
        {
            known.Add(exchange.Question);
            known.Add(exchange.Transcript);
        }

        return !RevealsExpectedAnswer(probe, context.ExpectedAnswer, known)
            && !RepeatsEarlierProbe(probe, context.PreviousProbes);
    }

    /// <summary>
    /// True when the probe contains a run of the expected answer that the student has
    /// neither heard in the question nor said themselves.
    /// </summary>
    public static bool RevealsExpectedAnswer(string probe, string? expectedAnswer, IEnumerable<string?> known)
    {
        var expected = Tokens(expectedAnswer);
        if (expected.Count == 0) return false;

        // A short expected answer ("lock", "Dictionary") is given away whole or not at
        // all. A single short token is too common to judge by ("có", "là") and is left
        // to the prompt.
        var run = Math.Min(LeakRunLength, expected.Count);
        if (run == 1 && expected[0].Length < 4) return false;

        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var text in known) allowed.UnionWith(Grams(Tokens(text), run));

        var asked = Grams(Tokens(probe), run);
        return Grams(expected, run).Any(gram => asked.Contains(gram) && !allowed.Contains(gram));
    }

    /// <summary>True when the probe asks what an earlier probe on the same question already asked.</summary>
    public static bool RepeatsEarlierProbe(string probe, IEnumerable<string> earlier)
    {
        var words = new HashSet<string>(Tokens(probe), StringComparer.Ordinal);
        if (words.Count == 0) return false;

        foreach (var previous in earlier)
        {
            var other = new HashSet<string>(Tokens(previous), StringComparer.Ordinal);
            if (other.Count == 0) continue;
            var shared = words.Count(other.Contains);
            var union = words.Count + other.Count - shared;
            if ((double)shared / union >= RepeatSimilarity) return true;
        }

        return false;
    }

    /// <summary>
    /// Lower-case words with punctuation stripped. Diacritics are kept because in
    /// Vietnamese they tell different words apart ("bán" / "bạn"); both sides go
    /// through the same Unicode normalisation, so typing differences do not matter.
    /// </summary>
    internal static List<string> Tokens(string? text)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return tokens;

        var word = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(c)) word.Append(char.ToLower(c, CultureInfo.InvariantCulture));
            else if (word.Length > 0) { tokens.Add(word.ToString()); word.Clear(); }
        }

        if (word.Length > 0) tokens.Add(word.ToString());
        return tokens;
    }

    private static HashSet<string> Grams(IReadOnlyList<string> tokens, int size)
    {
        var grams = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i + size <= tokens.Count; i++)
            grams.Add(string.Join(' ', tokens.Skip(i).Take(size)));
        return grams;
    }
}
