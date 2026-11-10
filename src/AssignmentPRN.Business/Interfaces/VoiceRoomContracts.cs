namespace AssignmentPRN.Business.Interfaces;

/// <param name="FollowUpId">
/// Set when the open item is a probe the AI examiner asked on top of the question, not
/// the question itself. The client still posts <c>ExamQuestionId</c> so audio keeps
/// landing beside the answer it belongs to.
/// </param>
/// <param name="FollowUpReason">Why the probe was asked, for the student to see.</param>
/// <param name="FollowUpNumber">1 for the first probe on this question, 2 for the next; 0 for the question itself.</param>
/// <param name="FollowUpLimit">
/// The most probes this question can have, counting those already asked, given both the
/// per-question and the per-slot budget. An upper bound: the examiner may ask fewer, and
/// asks none once too little time is left.
/// </param>
public sealed record VoiceQuestion(int ExamQuestionId, int OrderNo, string Text, DateTimeOffset AskedAt,
    DateTimeOffset Deadline, string? Transcript, bool HasAudio, int? FollowUpId = null,
    FollowUpReason? FollowUpReason = null, int FollowUpNumber = 0, int FollowUpLimit = 0);

/// <param name="FollowUps">Probes asked on this answer, oldest first.</param>
public sealed record VoiceSavedAnswer(int ExamQuestionId, int OrderNo, string Text, string? Transcript, bool HasAudio,
    IReadOnlyList<VoiceSavedFollowUp>? FollowUps = null);

public sealed record VoiceSavedFollowUp(int FollowUpId, string Text, string? Transcript, bool HasAudio,
    FollowUpReason Reason);

/// <param name="AwaitingFollowUp">
/// The answer has been handed in and the AI examiner is deciding whether to ask one more
/// thing. The next question deliberately stays closed meanwhile, so its clock does not
/// start while the student is waiting. It clears by itself if the decision never arrives.
/// </param>
/// <param name="AwaitingReady">
/// The student is in the room but has not finished the microphone check. No question is
/// open and no question clock runs until <see cref="IVoiceRoomService.ReadyAsync"/>.
/// </param>
public sealed record VoiceRoomState(int CandidateId, int ExamId, string ExamName, DateTimeOffset ServerNow,
    DateTimeOffset SlotEnd, bool Completed, int CompletedCount, int TotalCount, VoiceQuestion? Question,
    IReadOnlyList<VoiceSavedAnswer>? SavedAnswers = null, bool AwaitingFollowUp = false, bool AwaitingReady = false);

/// <param name="FollowUpId">
/// Set when answering a probe. It must be the probe that is currently open; anything
/// else is refused.
/// </param>
public sealed record VoiceAnswerRequest(int ExamQuestionId, string? Transcript = null, bool Submit = false,
    int? FollowUpId = null);

public sealed record VoiceAudioDownload(Stream Content, string ContentType);
public interface IVoiceRoomNotifier
{
    Task ChangedAsync(int candidateId, int studentId, CancellationToken ct);
}

public interface IVoiceRoomService
{
    Task<bool> IsVoiceAsync(int candidateId, int studentId, CancellationToken ct = default);
    Task<ServiceResponse<bool>> CanSubscribeAsync(int candidateId, int studentId, CancellationToken ct = default);
    Task<ServiceResponse<VoiceRoomState>> EnterAsync(int candidateId, int studentId, CancellationToken ct = default);
    /// <summary>
    /// Opens the first question once the student is through the microphone check. Safe to
    /// repeat: a room that is already under way is returned as it is.
    /// </summary>
    Task<ServiceResponse<VoiceRoomState>> ReadyAsync(int candidateId, int studentId, CancellationToken ct = default);
    Task<ServiceResponse<VoiceRoomState>> SyncAsync(int candidateId, int studentId, VoiceAnswerRequest? answer = null, CancellationToken ct = default);
    Task<ServiceResponse<VoiceRoomState>> UploadAsync(int candidateId, int studentId, int questionId,
        string contentType, long length, double durationSeconds, Stream content, CancellationToken ct = default);
    /// <param name="followUpId">Set to fetch the recording of a probe on that question rather than of the answer.</param>
    Task<ServiceResponse<VoiceAudioDownload>> DownloadAsync(int candidateId, int studentId, int questionId,
        int? followUpId = null, CancellationToken ct = default);
}
