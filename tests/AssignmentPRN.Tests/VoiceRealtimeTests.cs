using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Business.Services;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssignmentPRN.Tests;

public sealed class VoiceRealtimeTests
{
    private sealed class Notifier : IVoiceRoomNotifier
    {
        public int Calls;
        public Task ChangedAsync(int candidateId, int studentId, CancellationToken ct)
        {
            Assert.Equal(1, candidateId); Assert.Equal(2, studentId);
            Calls++;
            throw new IOException("Simulated delivery failure after commit");
        }
    }

    [Fact]
    public async Task Failed_delivery_preserves_commit_and_polls_do_not_broadcast_again()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await VoiceRoomTests.SeedAsync(clock);
        var notifier = new Notifier();
        var service = new VoiceRoomService(new VoiceRoomRepository(db, new ExamStatePolicy()), null!, clock, null, notifier);
        Assert.True((await service.CanSubscribeAsync(1, 2)).Success);
        Assert.False((await service.CanSubscribeAsync(1, 1)).Success);
        var state = (await service.SyncAsync(1, 2)).Data!;
        var answer = new VoiceAnswerRequest(state.Question!.ExamQuestionId, "accepted", true);
        var submitted = await service.SyncAsync(1, 2, answer);
        Assert.True(submitted.Success, submitted.Error);
        Assert.Equal(1, notifier.Calls);
        Assert.Equal("accepted", (await db.Answers.SingleAsync()).Transcript);
        Assert.True((await service.SyncAsync(1, 2, answer)).Success);
        Assert.True((await service.SyncAsync(1, 2)).Success);
        Assert.Equal(1, notifier.Calls);
    }
}
