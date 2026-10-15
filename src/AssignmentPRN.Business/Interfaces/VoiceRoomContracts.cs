namespace AssignmentPRN.Business.Interfaces;

public sealed record VoiceQuestion(int ExamQuestionId, int OrderNo, string Text, DateTimeOffset AskedAt,
    DateTimeOffset Deadline, string? Transcript, bool HasAudio);
public sealed record VoiceRoomState(int CandidateId, int ExamId, string ExamName, DateTimeOffset ServerNow,
    DateTimeOffset SlotEnd, bool Completed, int CompletedCount, int TotalCount, VoiceQuestion? Question);
public sealed record VoiceAnswerRequest(int ExamQuestionId, string? Transcript = null, bool Submit = false);

public interface IVoiceRoomService
{
    Task<ServiceResponse<VoiceRoomState>> EnterAsync(int candidateId, int studentId, CancellationToken ct = default);
    Task<ServiceResponse<VoiceRoomState>> SyncAsync(int candidateId, int studentId, VoiceAnswerRequest? answer = null, CancellationToken ct = default);
}
