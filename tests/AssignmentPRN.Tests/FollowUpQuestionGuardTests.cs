using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Tests;

/// <summary>
/// The last check before a probe is read aloud. A probe that gives the expected answer
/// away, or asks the same thing twice, must be dropped; an ordinary probe must not be.
/// </summary>
public sealed class FollowUpQuestionGuardTests
{
    private const string Question = "Lock trong C# dùng để làm gì?";
    private const string Expected = "Lock đảm bảo chỉ một luồng được vào vùng găng tại một thời điểm, tránh tranh chấp dữ liệu.";

    private static FollowUpContext Context(string? transcript = "Em nghĩ là để khoá lại.",
        string? expected = Expected, params string[] previous) =>
        new(Question, expected, [], transcript, previous, transcript);

    [Fact]
    public void A_probe_that_reads_out_the_expected_answer_is_dropped()
    {
        var probe = "Em có biết là chỉ một luồng được vào vùng găng tại một thời điểm không?";

        Assert.False(FollowUpQuestionGuard.IsUsable(probe, Context()));
    }

    [Fact]
    public void An_ordinary_probe_is_kept()
    {
        var probe = "Em giải thích rõ hơn khi nhiều luồng cùng chạy thì lock giúp gì được không?";

        Assert.True(FollowUpQuestionGuard.IsUsable(probe, Context()));
    }

    [Fact]
    public void Quoting_what_the_student_already_said_gives_nothing_away()
    {
        // The student said the key phrase themselves; asking them to expand on it is fair.
        var transcript = "Em nghĩ là chỉ một luồng được vào vùng găng thôi ạ.";
        var probe = "Em vừa nói chỉ một luồng được vào vùng găng, vậy luồng còn lại làm gì?";

        Assert.True(FollowUpQuestionGuard.IsUsable(probe, Context(transcript)));
    }

    [Fact]
    public void Reusing_the_wording_of_the_question_gives_nothing_away()
    {
        var context = new FollowUpContext("Giải thích cơ chế garbage collection trong .NET hoạt động thế nào?",
            "Garbage collection trong .NET hoạt động theo thế hệ 0, 1 và 2.", [], "Nó dọn bộ nhớ.", []);

        Assert.True(FollowUpQuestionGuard.IsUsable(
            "Em nói cụ thể garbage collection trong .NET hoạt động thế nào được không?", context));
    }

    [Fact]
    public void A_short_expected_answer_is_given_away_whole()
    {
        var context = new FollowUpContext("Từ khoá nào khoá một đối tượng trong C#?", "lock", [], "Em không nhớ.", []);

        Assert.False(FollowUpQuestionGuard.IsUsable("Em đã nghe tới từ khoá lock chưa?", context));
    }

    [Fact]
    public void A_short_expected_answer_the_student_said_is_fair_to_repeat()
    {
        var context = new FollowUpContext("Từ khoá nào khoá một đối tượng trong C#?", "lock", [], "Dạ là lock ạ.", []);

        Assert.True(FollowUpQuestionGuard.IsUsable("Em dùng lock trên đối tượng nào?", context));
    }

    [Fact]
    public void A_one_syllable_expected_answer_is_too_common_to_judge()
    {
        var context = new FollowUpContext("Interface có được chứa field không?", "Không", [], "Có ạ.", []);

        Assert.True(FollowUpQuestionGuard.IsUsable("Em chắc chắn không, vì sao?", context));
    }

    [Fact]
    public void Without_an_expected_answer_nothing_can_leak()
    {
        Assert.True(FollowUpQuestionGuard.IsUsable("Em cho một ví dụ cụ thể được không?", Context(expected: null)));
    }

    [Fact]
    public void The_same_probe_asked_twice_is_dropped_even_reworded_slightly()
    {
        var context = Context(previous: "Em cho một ví dụ cụ thể được không?");

        Assert.False(FollowUpQuestionGuard.IsUsable("Em cho một ví dụ cụ thể, được không ạ?", context));
        Assert.True(FollowUpQuestionGuard.IsUsable("Nếu không dùng lock thì chuyện gì xảy ra hả em?", context));
    }

    [Fact]
    public void Wording_from_an_earlier_exchange_counts_as_known()
    {
        var context = new FollowUpContext(Question, Expected, [], "Dạ em không rõ.", ["Em nói lại được không?"],
            "Để tránh tranh chấp dữ liệu ạ.",
            [new FollowUpExchange("Em nói lại được không?", "Dạ em không rõ.")]);

        Assert.True(FollowUpQuestionGuard.IsUsable("Lúc đầu em nói để tránh tranh chấp dữ liệu, tranh chấp đó là gì?", context));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_probe_is_never_usable(string probe)
    {
        Assert.False(FollowUpQuestionGuard.IsUsable(probe, Context()));
    }

    [Fact]
    public void Tokens_ignore_case_and_punctuation()
    {
        Assert.Equal(["lock", "là", "gì"], FollowUpQuestionGuard.Tokens("LOCK, là... gì?"));
    }
}
