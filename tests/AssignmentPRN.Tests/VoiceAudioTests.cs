using AssignmentPRN.Business.Policies;
using AssignmentPRN.Business.Services;
using AssignmentPRN.DataAccess.Repositories;
using AssignmentPRN.DataAccess.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssignmentPRN.Tests;

public sealed class VoiceAudioTests
{
    private sealed class AudioStore : IVoiceAudioStore
    {
        public int Saves;
        public string Key = "1/1/same.webm";
        public Task<string> SaveAsync(int candidateId, int questionId, string contentType, Stream content, CancellationToken ct)
        { Saves++; return Task.FromResult(Key); }
        public Task<StoredVoiceAudio> OpenAsync(string key, CancellationToken ct) =>
            Task.FromResult(new StoredVoiceAudio(new MemoryStream([1, 2, 3]), "audio/webm"));
    }

    [Fact]
    public async Task Retry_reuses_answer_and_only_owner_can_download()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await VoiceRoomTests.SeedAsync(clock);
        var store = new AudioStore();
        var service = new VoiceRoomService(new VoiceRoomRepository(db, new ExamStatePolicy()), null!, clock, store);
        var state = (await service.SyncAsync(1, 2)).Data!;
        var id = state.Question!.ExamQuestionId;
        clock.At = clock.At.AddSeconds(5);
        Assert.True((await service.UploadAsync(1, 2, id, "audio/webm", 12, 4, Stream.Null)).Success);
        Assert.True((await service.UploadAsync(1, 2, id, "audio/webm", 12, 4, Stream.Null)).Success);
        Assert.Single(await db.Answers.ToListAsync());
        Assert.False((await service.DownloadAsync(1, 1, id)).Success);
        var download = await service.DownloadAsync(1, 2, id);
        Assert.True(download.Success, download.Error);
        await download.Data!.Content.DisposeAsync();
        Assert.True((await service.SyncAsync(1, 2, new(id, "spoken", true))).Success);
        Assert.True((await service.UploadAsync(1, 2, id, "audio/webm", 12, 4, Stream.Null)).Success);
        store.Key = "different.webm";
        Assert.False((await service.UploadAsync(1, 2, id, "audio/webm", 12, 4, Stream.Null)).Success);
        Assert.Equal("1/1/same.webm", (await db.Answers.SingleAsync()).AudioPath);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("question")]
    [InlineData("size")]
    [InlineData("duration")]
    [InlineData("nan")]
    public async Task Invalid_upload_is_rejected_before_storage(string scenario)
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await VoiceRoomTests.SeedAsync(clock);
        var store = new AudioStore();
        var service = new VoiceRoomService(new VoiceRoomRepository(db, new ExamStatePolicy()), null!, clock, store);
        var state = (await service.SyncAsync(1, 2)).Data!;
        var result = await service.UploadAsync(1, scenario == "owner" ? 1 : 2,
            scenario == "question" ? 999 : state.Question!.ExamQuestionId, "audio/webm",
            scenario == "size" ? LocalVoiceAudioStore.MaxBytes + 1 : 12,
            scenario == "nan" ? double.NaN : scenario == "duration" ? 1800 : 1, Stream.Null);
        Assert.False(result.Success);
        Assert.Equal(0, store.Saves);
        Assert.Empty(await db.Answers.ToListAsync());
    }

    [Fact]
    public async Task Timeout_transfer_grace_does_not_reopen_question_or_change_transcript()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await VoiceRoomTests.SeedAsync(clock);
        var service = new VoiceRoomService(new VoiceRoomRepository(db, new ExamStatePolicy()), null!, clock, new AudioStore());
        var state = (await service.SyncAsync(1, 2)).Data!;
        var id = state.Question!.ExamQuestionId;
        Assert.True((await service.SyncAsync(1, 2, new(id, "draft before timeout"))).Success);
        clock.At = state.Question.Deadline.AddSeconds(1);
        var uploaded = await service.UploadAsync(1, 2, id, "audio/webm", 12, 29, Stream.Null);
        Assert.True(uploaded.Success, uploaded.Error);
        Assert.Equal(2, uploaded.Data!.Question!.OrderNo);
        var first = await db.Answers.SingleAsync();
        Assert.Equal("draft before timeout", first.Transcript);
        Assert.Equal(state.Question.Deadline.LocalDateTime, first.FinishedAt);
    }

    [Fact]
    public async Task Store_uses_safe_keys_and_retries_without_overwriting()
    {
        var root = Path.Combine(Path.GetTempPath(), "aives-voice-" + Guid.NewGuid().ToString("N"));
        var store = new LocalVoiceAudioStore(root);
        byte[] wav = new byte[44];
        "RIFF"u8.CopyTo(wav); "WAVE"u8.CopyTo(wav.AsSpan(8));
        var key = await store.SaveAsync(1, 2, "audio/wav", new MemoryStream(wav), default);
        Assert.Equal(key, await store.SaveAsync(1, 2, "audio/wav", new MemoryStream(wav), default));
        Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        var audio = await store.OpenAsync(key, default);
        await using (audio.Content) Assert.Equal(44, audio.Content.Length);
        await Assert.ThrowsAsync<ArgumentException>(() => store.OpenAsync("../secret", default));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(1, 2, "text/html", new MemoryStream(wav), default));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(1, 2, "audio/webm", new MemoryStream(wav), default));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(1, 2, "audio/wav", Stream.Null, default));
        // Keep test artifacts: no cleanup/deletion is performed without user approval.
    }
}
