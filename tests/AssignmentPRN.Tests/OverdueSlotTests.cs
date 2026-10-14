using AssignmentPRN.Business;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Tests;

public class OverdueSlotTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0);
    private const int Minutes = 30;

    private static IReadOnlyDictionary<int, CandidateStatus> Close(DateTime now, params SlotState[] slots) =>
        ExamResultService.CloseOverdueSlots(slots, Minutes, now);

    [Fact]
    public void A_no_show_is_marked_absent_once_the_slot_is_over()
    {
        var changes = Close(Start.AddMinutes(31), new SlotState(1, CandidateStatus.Waiting, Start));

        Assert.Equal(CandidateStatus.Absent, changes[1]);
    }

    [Fact]
    public void A_waiting_student_is_left_alone_while_the_slot_runs()
    {
        Assert.Empty(Close(Start.AddMinutes(30), new SlotState(1, CandidateStatus.Waiting, Start)));
    }

    [Fact]
    public void An_open_paper_is_closed_only_after_the_submit_grace()
    {
        var slot = new SlotState(1, CandidateStatus.InProgress, Start);
        var end = Start.AddMinutes(Minutes);

        Assert.Empty(Close(end + ExamSessionRules.SubmitGrace, slot));
        Assert.Equal(CandidateStatus.Completed, Close(end + ExamSessionRules.SubmitGrace + TimeSpan.FromSeconds(1), slot)[1]);
    }

    [Theory]
    [InlineData(CandidateStatus.Completed)]
    [InlineData(CandidateStatus.Absent)]
    [InlineData(CandidateStatus.Cancelled)]
    public void Settled_slots_are_never_touched(CandidateStatus status)
    {
        Assert.Empty(Close(Start.AddDays(1), new SlotState(1, status, Start)));
    }

    [Fact]
    public void A_slot_without_a_time_is_skipped()
    {
        Assert.Empty(Close(Start.AddDays(1), new SlotState(1, CandidateStatus.Waiting, null)));
    }

    [Fact]
    public void Each_slot_is_judged_on_its_own_time()
    {
        var changes = Close(
            Start.AddMinutes(45),
            new SlotState(1, CandidateStatus.Waiting, Start),
            new SlotState(2, CandidateStatus.Waiting, Start.AddMinutes(30)));

        Assert.Equal(CandidateStatus.Absent, Assert.Single(changes).Value);
        Assert.True(changes.ContainsKey(1));
    }

    [Fact]
    public void Average_score_counts_completed_papers_only()
    {
        var sheet = new SessionResultResponse
        {
            Candidates =
            [
                new CandidateResultItemResponse { Status = CandidateStatus.Completed, Score = 8m },
                new CandidateResultItemResponse { Status = CandidateStatus.Completed, Score = 5m },
                new CandidateResultItemResponse { Status = CandidateStatus.Absent }
            ]
        };

        Assert.Equal(6.5m, sheet.AverageScore);
        Assert.Equal(2, sheet.CompletedCount);
        Assert.Equal(1, sheet.AbsentCount);
    }

    [Fact]
    public void Average_score_is_empty_before_anyone_finishes()
    {
        Assert.Null(new SessionResultResponse().AverageScore);
    }
}
