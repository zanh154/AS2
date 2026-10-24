using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;

using QuestionPickRequest = AssignmentPRN.Business.Interfaces.QuestionPickRequest;
using QuestionOptionInput = AssignmentPRN.Business.Interfaces.QuestionOptionInput;
using ExamQuestionAssignmentRequest = AssignmentPRN.Business.Interfaces.ExamQuestionAssignmentRequest;
using ExamQuestionAssignmentResult = AssignmentPRN.Business.Interfaces.ExamQuestionAssignmentResult;
using ExamPaperItem = AssignmentPRN.Business.Interfaces.ExamPaperItem;

namespace AssignmentPRN.Business.Services;

/// <summary>
/// Owns the question bank. A lecturer may only touch the questions of the courses they
/// teach, which is checked here rather than in the controller.
/// </summary>
public class QuestionService(
    IQuestionRepository questionRepository,
    ICatalogRepository catalogRepository,
    ICourseMaterialRepository courseMaterialRepository, IExamRealtimeNotifier? realtime = null, IExamSessionRepository? sessionRepository = null,
    GradingQueue? grading = null) : IQuestionService
{
    public Task<ServiceResponse> SaveDraftAsync(int candidateId, int studentId, IReadOnlyDictionary<int, int?> answers, CancellationToken cancellationToken = default) =>
        ServiceExecutor.RunAsync(async () => {
            ArgumentNullException.ThrowIfNull(answers);
            if (answers.Count > 50) throw new BusinessValidationException("Số đáp án không hợp lệ.");
            // Freeze the request; validate against data read inside the save transaction.
            var snapshot = answers.ToDictionary(x => x.Key, x => x.Value);
            await questionRepository.SaveDraftAsync(candidateId,
                state => ValidateDraft(state, studentId, snapshot, DateTime.Now), cancellationToken);
        }, "Không thể lưu tạm đáp án. Hãy thử lại.");
    public Task<ServiceResponse<QuestionPickRequest>> GetExamConfigurationAsync(int examId, CancellationToken cancellationToken = default) =>
        ServiceExecutor.RunAsync(async () => {
            var config = await questionRepository.GetExamConfigurationAsync(examId, cancellationToken);
            return new QuestionPickRequest { CourseId = config.CourseId, Count = config.Count,
                MaterialIds = config.MaterialIds, Difficulties = config.Difficulties.Select(x => x.ToBusiness()).ToList() };
        }, "Không tải được cấu hình đề thi.");
    /// <summary>Longest question body, comfortably above the 65 000-byte TEXT limit is checked separately.</summary>
    public const int MaxQuestionTextLength = 4000;

    public const int MaxExpectedAnswerLength = 4000;

    public const int MaxOptionTextLength = 500;

    public const int MinOptions = 2;

    public const int MaxOptions = 8;

    public Task<ServiceResponse<IReadOnlyList<QuestionListItemResponse>>> ListAsync(
        int? lecturerId,
        int? courseId = null,
        int? materialId = null,
        QuestionDifficulty? difficulty = null,
        BloomLevel? bloomLevel = null,
        string? term = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<IReadOnlyList<QuestionListItemResponse>>(
            async () =>
            {
                var allowedCourseIds = await VisibleCourseIdsAsync(lecturerId, cancellationToken);
                if (allowedCourseIds.Count == 0)
                {
                    return (IReadOnlyList<QuestionListItemResponse>)Array.Empty<QuestionListItemResponse>();
                }

                if (courseId is int requested && !allowedCourseIds.Contains(requested))
                {
                    throw new BusinessValidationException("Bạn không có quyền xem câu hỏi của môn học này.");
                }

                var effectiveCourseId = courseId ?? (allowedCourseIds.Count == 1 ? allowedCourseIds[0] : null);

                // An admin sees every course, so no course filter is applied unless one was chosen.
                var items = await questionRepository.ListAsync(
                    new QuestionQuery
                    {
                        CourseId = effectiveCourseId,
                        MaterialId = materialId,
                        Difficulty = difficulty?.ToDataAccess(),
                        BloomLevel = bloomLevel?.ToDataAccess(),
                        Term = term,
                        IncludeArchived = includeArchived
                    },
                    cancellationToken);

                return items
                    .Where(item => allowedCourseIds.Contains(item.CourseId))
                    .Select(item => new QuestionListItemResponse
                    {
                        QuestionId = item.QuestionId,
                        CourseId = item.CourseId,
                        CourseCode = item.CourseCode,
                        MaterialId = item.MaterialId,
                        MaterialName = item.MaterialName,
                        QuestionText = item.QuestionText,
                        Difficulty = item.Difficulty.ToBusiness(),
                        BloomLevel = item.BloomLevel.ToBusiness(),
                        QuestionType = item.QuestionType.ToBusiness(),
                        Status = item.Status.ToBusiness(),
                        AuthorName = item.AuthorName,
                        CreatedAt = item.CreatedAt
                    })
                    .ToList();
            },
            "Không thể tải ngân hàng câu hỏi.");
    }

    public Task<ServiceResponse<QuestionResponse>> GetAsync(
        int questionId,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var detail = await questionRepository.GetDetailAsync(questionId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy câu hỏi.");

                await EnsureCourseVisibleAsync(detail.CourseId, lecturerId, cancellationToken);
                return MapQuestion(detail);
            },
            "Không thể tải câu hỏi.");
    }

    public Task<ServiceResponse<QuestionResponse>> SaveAsync(
        QuestionSaveRequest request,
        int lecturerId,
        int? ownerLecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(request);

                var courseId = BusinessValidation.PositiveId(request.CourseId, "môn học");
                if (!await catalogRepository.CourseExistsAsync(courseId, cancellationToken))
                {
                    throw new BusinessValidationException("Môn học không tồn tại hoặc đã ngừng hoạt động.");
                }

                var body = BusinessValidation.RequiredText(request.QuestionText, "nội dung câu hỏi", MaxQuestionTextLength);
                var expectedAnswer = BusinessValidation.OptionalText(
                    request.ExpectedAnswer,
                    "đáp án mong đợi",
                    MaxExpectedAnswerLength);
                var options = NormaliseOptions(request.Options, expectedAnswer);

                // A follow-up is picked by topic, so without one it could never be asked.
                if (request.QuestionType == QuestionType.FollowUp && request.MaterialId is null)
                {
                    throw new BusinessValidationException("Câu hỏi đào sâu phải gắn với một chủ đề (tài liệu).");
                }

                if (request.QuestionId != 0)
                {
                    var existing = await questionRepository.GetDetailAsync(request.QuestionId, cancellationToken)
                        ?? throw new BusinessValidationException("Không tìm thấy câu hỏi.");
                    await EnsureCourseVisibleAsync(existing.CourseId, ownerLecturerId, cancellationToken);

                    // Past papers were dealt by round; flipping the type would rewrite that history.
                    if (existing.QuestionType.ToBusiness() != request.QuestionType
                        && await questionRepository.IsAssignedToExamAsync(existing.QuestionId, cancellationToken))
                    {
                        throw new BusinessValidationException(
                            "Câu hỏi đã được phát cho lượt thi, không thể đổi loại câu.");
                    }
                }

                if (request.MaterialId is int materialId)
                {
                    await EnsureMaterialBelongsToCourseAsync(materialId, courseId, cancellationToken);
                }

                // Checked last, so a lecture that only mistypes the text still gets the
                // more specific message about the topic.
                await EnsureNotDuplicateAsync(
                    courseId,
                    body,
                    request.QuestionId,
                    cancellationToken);

                var saved = await questionRepository.SaveAsync(
                    new QuestionUpsertInput
                    {
                        QuestionId = request.QuestionId,
                        CourseId = courseId,
                        MaterialId = request.MaterialId,
                        QuestionText = body,
                        ExpectedAnswer = expectedAnswer,
                        BloomLevel = request.BloomLevel.ToDataAccess(),
                        Difficulty = request.Difficulty.ToDataAccess(),
                        QuestionType = request.QuestionType.ToDataAccess(),
                        Options = options.Select(item => new AssignmentPRN.DataAccess.Contracts.QuestionOptionInput
                        {
                            Text = item.Text,
                            IsCorrect = item.IsCorrect
                        }).ToList()
                    },
                    lecturerId,
                    cancellationToken);

                await NotifyCourseChangedAsync(saved.CourseId, lecturerId, "questions", cancellationToken);

                return MapQuestion(saved);
            },
            "Không thể lưu câu hỏi.");
    }

    public Task<ServiceResponse> ArchiveAsync(
        int questionId,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var detail = await questionRepository.GetDetailAsync(questionId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy câu hỏi.");
                await EnsureCourseVisibleAsync(detail.CourseId, lecturerId, cancellationToken);
                await questionRepository.ArchiveAsync(questionId, cancellationToken);
                await NotifyCourseChangedAsync(detail.CourseId, lecturerId, "questions", cancellationToken);
            },
            "Không thể ẩn câu hỏi.");
    }

    public async Task<ServiceResponse<QuestionImportResult>> ImportCsvAsync(
        int courseId,
        string csvContent,
        IReadOnlyDictionary<string, int> materialIdsByFileName,
        int lecturerId,
        int? ownerLecturerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(csvContent);
        ArgumentNullException.ThrowIfNull(materialIdsByFileName);

        var rows = ParseCsv(csvContent, materialIdsByFileName, out var parseErrors);
        var result = await ImportAsync(courseId, rows, lecturerId, ownerLecturerId, cancellationToken);
        if (!result.Success || result.Data is null || parseErrors.Count == 0)
        {
            return result;
        }

        // One list, so the lecturer sees every skipped line in one place.
        return ServiceResponse<QuestionImportResult>.Ok(new QuestionImportResult
        {
            Imported = result.Data.Imported,
            Errors = [.. result.Data.Errors, .. parseErrors]
        });
    }

    public Task<ServiceResponse<QuestionImportResult>> ImportAsync(
        int courseId,
        IReadOnlyList<QuestionImportRow> rows,
        int lecturerId,
        int? ownerLecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(rows);

                var targetCourseId = BusinessValidation.PositiveId(courseId, "môn học");
                await EnsureCourseVisibleAsync(targetCourseId, ownerLecturerId, cancellationToken);

                if (!await catalogRepository.CourseExistsAsync(targetCourseId, cancellationToken))
                {
                    throw new BusinessValidationException("Môn học không tồn tại hoặc đã ngừng hoạt động.");
                }

                if (rows.Count == 0)
                {
                    throw new BusinessValidationException("File không có câu hỏi nào để nhập.");
                }

                var errors = new List<string>();
                var imported = 0;
                var affectedCourseIds = new HashSet<int>();

                // Loaded once and kept up to date as rows go in, so a file that repeats a
                // question, or repeats itself, is caught without a query per line.
                var known = new List<QuestionTextMatch>(
                    await questionRepository.ListTextMatchesAsync(targetCourseId, cancellationToken));

                for (var index = 0; index < rows.Count; index++)
                {
                    var row = rows[index];

                    try
                    {
                        var body = BusinessValidation.RequiredText(
                            row.QuestionText,
                            "nội dung câu hỏi",
                            MaxQuestionTextLength);
                        var expectedAnswer = BusinessValidation.OptionalText(
                            row.ExpectedAnswer,
                            "đáp án mong đợi",
                            MaxExpectedAnswerLength);
                        var options = NormaliseOptions(row.Options, expectedAnswer);

                        if (row.MaterialId is int materialId)
                        {
                            await EnsureMaterialBelongsToCourseAsync(materialId, targetCourseId, cancellationToken);
                        }

                        EnsureNotDuplicate(known, body, editingQuestionId: 0);

                        var saved = await questionRepository.SaveAsync(
                            new QuestionUpsertInput
                            {
                                CourseId = targetCourseId,
                                MaterialId = row.MaterialId,
                                QuestionText = body,
                                ExpectedAnswer = expectedAnswer,
                                BloomLevel = row.BloomLevel.ToDataAccess(),
                                Difficulty = row.Difficulty.ToDataAccess(),
                                Options = options.Select(item => new AssignmentPRN.DataAccess.Contracts.QuestionOptionInput
                                {
                                    Text = item.Text,
                                    IsCorrect = item.IsCorrect
                                }).ToList()
                            },
                            lecturerId,
                            cancellationToken);

                        known.Add(new QuestionTextMatch
                        {
                            QuestionId = saved.QuestionId,
                            QuestionText = body,
                            Status = saved.Status,
                            MaterialId = row.MaterialId
                        });

                        imported++;
                        affectedCourseIds.Add(saved.CourseId);
                    }
                    catch (Exception exception) when (exception is BusinessValidationException or ArgumentException
                        or KeyNotFoundException)
                    {
                        // The row carries the file line it came from, so the message points
                        // at the source even when the failure happens here rather than
                        // while parsing.
                        errors.Add($"Dòng {row.SourceLine}: {exception.Message}");
                    }
                }

                foreach (var affectedCourseId in affectedCourseIds)
                {
                    await NotifyCourseChangedAsync(affectedCourseId, lecturerId, "questions", cancellationToken);
                }

                return new QuestionImportResult
                {
                    Imported = imported,
                    Errors = errors
                };
            },
            "Không thể nhập câu hỏi từ file.");
    }

    public Task<ServiceResponse<QuestionAvailabilityResponse>> CheckAvailabilityAsync(
        QuestionPickRequest request,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var count = BusinessValidation.InRange(request.Count, 1, 50, "Số câu hỏi mỗi sinh viên");
                var courseId = BusinessValidation.PositiveId(request.CourseId, "môn học");
                var poolRequest = BuildPoolRequest(courseId, count, request);
                var pool = await questionRepository.ListPoolIdsAsync(poolRequest, cancellationToken);
                var sources = await questionRepository.ListPoolSourcesAsync(poolRequest, cancellationToken);

                return new QuestionAvailabilityResponse
                {
                    Requested = count,
                    Available = CountAvailableQuestions(pool, request.TakenQuestionIds),
                    AvailableManual = sources.Count(source => source == AssignmentPRN.DataAccess.Enums.QuestionSource.Manual),
                    AvailableAi = sources.Count(source => source == AssignmentPRN.DataAccess.Enums.QuestionSource.AI)
                };
            },
            "Không thể kiểm tra số câu hỏi của ngân hàng.");
    }

    public Task<ServiceResponse<IReadOnlyList<int>>> PickAsync(
        QuestionPickRequest request,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<IReadOnlyList<int>>(
            async () =>
            {
                var courseId = BusinessValidation.PositiveId(request.CourseId, "môn học");
                var count = BusinessValidation.InRange(request.Count, 1, 50, "Số câu hỏi mỗi sinh viên");
                var pool = await questionRepository.ListPoolIdsAsync(
                    BuildPoolRequest(courseId, count, request),
                    cancellationToken);

                var picked = PickQuestions(pool, count, request.TakenQuestionIds);
                if (picked.Count < count)
                {
                    throw new BusinessValidationException(
                        $"Ngân hàng câu hỏi của môn này chỉ còn {picked.Count}/{count} câu chưa dùng. "
                        + "Hãy bổ sung câu hỏi trước khi tạo lịch thi.");
                }

                return picked;
            },
            "Không thể rút câu hỏi cho sinh viên.");
    }

    public Task<ServiceResponse<ExamQuestionAssignmentResult>> AssignToExamAsync(
        ExamQuestionAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(request);

                var examId = BusinessValidation.PositiveId(request.ExamId, "lịch thi");
                var courseId = BusinessValidation.PositiveId(request.CourseId, "môn học");
                var countPerCandidate = BusinessValidation.InRange(
                    request.CountPerCandidate,
                    1,
                    50,
                    "Số câu hỏi mỗi sinh viên");

                var papers = await questionRepository.ListExamPaperAsync(examId, cancellationToken);
                if (papers.Count == 0)
                {
                    throw new BusinessValidationException(
                        "Lịch thi này chưa có sinh viên nào để phân câu hỏi.");
                }

                var filter = BuildPoolRequest(courseId, countPerCandidate, request);
                var candidateCount = 0;
                var rows = new List<ExamQuestionInput>();
                var taken = new HashSet<int>();

                // Decided under the exam lock: a student who entered a moment ago already
                // holds a paper and is skipped, so nobody is dealt twice.
                await questionRepository.DealAsync(
                    examId,
                    async dealt =>
                    {
                        var candidateIds = papers
                            .Select(paper => paper.CandidateId)
                            .Where(candidateId => !dealt.CandidatesWithPaper.Contains(candidateId))
                            .ToList();
                        if (candidateIds.Count == 0)
                        {
                            throw new BusinessValidationException(
                                "Mọi sinh viên của lịch thi này đã có đề. Hãy huỷ đề hiện tại trước khi phát lại.");
                        }

                        taken.UnionWith(dealt.TakenQuestionIds);
                        var needed = candidateIds.Count * countPerCandidate;
                        var wholePool = await questionRepository.ListPoolIdsAsync(filter, cancellationToken);
                        if (CountAvailableQuestions(wholePool, taken) < needed)
                        {
                            throw new BusinessValidationException(
                                $"Ngân hàng câu hỏi của môn này không đủ {needed} câu khác nhau cho "
                                + $"{candidateIds.Count} sinh viên. Hãy bổ sung câu hỏi trước khi phát đề.");
                        }

                        foreach (var candidateId in candidateIds)
                        {
                            var paper = await PickPaperAsync(filter, candidateId, taken, cancellationToken)
                                ?? throw new BusinessValidationException(
                                    $"Ngân hàng câu hỏi đã cạn: sinh viên thứ {rows.Count / countPerCandidate + 1} "
                                    + "không đủ câu chưa dùng. Hãy bổ sung câu hỏi trước khi phát đề.");
                            rows.AddRange(paper);
                        }

                        candidateCount = candidateIds.Count;
                        return rows;
                    },
                    cancellationToken, filter);

                return new ExamQuestionAssignmentResult
                {
                    ExamId = examId,
                    CandidateCount = candidateCount,
                    AssignedCount = rows.Count,
                    TakenQuestionIds = taken.OrderBy(id => id).ToList()
                };
            },
            "Không thể phân câu hỏi cho lịch thi.");
    }

    public Task<ServiceResponse<ExamRoomResponse>> EnterExamAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var candidate = await LoadSittableCandidateAsync(candidateId, studentUserId, cancellationToken);

                await DealToCandidateAsync(candidate, cancellationToken);

                await questionRepository.StartCandidateAsync(candidateId, DateTime.Now, cancellationToken);
                await NotifyProgressAsync(candidate.ExamId, "entered", cancellationToken);

                // Re-read: the slot now carries its paper and its start time.
                var opened = await questionRepository.GetExamRoomCandidateAsync(candidateId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

                return MapRoom(
                    opened,
                    await questionRepository.ListCandidateQuestionsAsync(candidateId, cancellationToken));
            },
            "Không thể vào phòng thi.");
    }

    public Task<ServiceResponse<ExamRoomResponse>> GetExamRoomAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var candidate = await questionRepository.GetExamRoomCandidateAsync(candidateId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

                if (candidate.StudentId != studentUserId)
                {
                    throw new BusinessValidationException("Đây không phải lượt thi của bạn.");
                }

                return MapRoom(
                    candidate,
                    await questionRepository.ListCandidateQuestionsAsync(candidateId, cancellationToken));
            },
            "Không thể mở phòng thi.");
    }

    public Task<ServiceResponse<ExamResultResponse>> GetExamResultAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var candidate = await questionRepository.GetExamRoomCandidateAsync(candidateId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

                if (candidate.StudentId != studentUserId)
                {
                    throw new BusinessValidationException("Đây không phải lượt thi của bạn.");
                }

                if (candidate.CandidateStatus.ToBusiness() != CandidateStatus.Completed)
                {
                    throw new BusinessValidationException("Kết quả chỉ hiển thị sau khi bạn đã nộp bài.");
                }

                return new ExamResultResponse
                {
                    CandidateId = candidate.CandidateId,
                    ExamName = candidate.ExamName,
                    CourseCode = candidate.CourseCode,
                    CourseName = candidate.CourseName,
                    LecturerName = candidate.LecturerName,
                    Questions = (await questionRepository.ListCandidateResultsAsync(
                        candidateId,
                        cancellationToken: cancellationToken))
                        .Select(item => item.ToBusiness())
                        .ToList()
                };
            },
            "Không thể tải kết quả bài thi.");
    }

    public Task<ServiceResponse<ExamRoomResponse>> SubmitExamAsync(
        int candidateId,
        int studentUserId,
        IReadOnlyDictionary<int, int?> selectedOptionByExamQuestion,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(selectedOptionByExamQuestion);

                var candidate = await LoadSubmittableCandidateAsync(candidateId, studentUserId, cancellationToken);

                var paper = await questionRepository.ListCandidateQuestionsAsync(candidateId, cancellationToken);
                if (paper.Count == 0)
                {
                    throw new BusinessValidationException("Bạn chưa vào ca thi này.");
                }

                // Only the round being sat is taken; a handed-in round cannot be changed.
                var inFollowUpRound = paper.Any(question => question.IsFollowUp);
                var openRound = paper.Where(question => question.IsFollowUp == inFollowUpRound).ToList();
                var cleaned = CleanAnswers(openRound, selectedOptionByExamQuestion);

                var now = DateTime.Now;
                IReadOnlyList<ExamQuestionInput> followUps = inFollowUpRound
                    ? Array.Empty<ExamQuestionInput>()
                    : await PlanFollowUpsAsync(candidate, cleaned, paper.Count, now, cancellationToken);

                await questionRepository.SubmitAnswersAsync(
                    candidateId, cleaned, now, followUps, cancellationToken);
                await NotifyProgressAsync(candidate.ExamId, "submitted", cancellationToken);

                var submitted = await questionRepository.GetExamRoomCandidateAsync(candidateId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

                // Nộp bài xong là lượt thi đóng, và chấm điểm phải chạy ngay trong ca thi
                // thay vì đợi vòng quét lúc khởi động app. Đẩy vào hàng đợi, không chấm
                // ở đây: mỗi câu là một lời gọi model, lâu hơn hẳn một request.
                if (grading is not null && submitted.CandidateStatus == DataAccess.Enums.CandidateStatus.Completed)
                {
                    try { await grading.QueueCandidateAsync(candidateId, cancellationToken); }
                    catch (Exception) { /* Vòng quét định kỳ vẫn gom các lượt chưa chấm. */ }
                }

                return MapRoom(
                    submitted,
                    await questionRepository.ListCandidateQuestionsAsync(candidateId, cancellationToken));
            },
            "Không thể nộp bài.");
    }

    /// <summary>
    /// Keeps one answer per question of the round, blank when nothing was posted. Each pick
    /// has to be a choice of the very question it was posted for, so a tampered form cannot
    /// attach someone else's option to a slot.
    /// </summary>
    private static Dictionary<int, int?> CleanAnswers(
        IReadOnlyList<AssignmentPRN.DataAccess.Contracts.ExamRoomQuestion> round,
        IReadOnlyDictionary<int, int?> posted)
    {
        var cleaned = new Dictionary<int, int?>(round.Count);
        foreach (var question in round)
        {
            if (!posted.TryGetValue(question.ExamQuestionId, out var optionId))
            {
                cleaned[question.ExamQuestionId] = null;
                continue;
            }

            if (optionId is int picked && question.Options.All(option => option.OptionId != picked))
            {
                throw new BusinessValidationException("Đáp án gửi lên không thuộc câu hỏi tương ứng.");
            }

            cleaned[question.ExamQuestionId] = optionId;
        }

        return cleaned;
    }

    /// <summary>
    /// Works out the follow-up round from the main answers just handed in. Returns nothing
    /// when the session asks for no follow-ups, too little time is left, or the bank has no
    /// fitting question; the paper then simply closes.
    /// </summary>
    private async Task<IReadOnlyList<ExamQuestionInput>> PlanFollowUpsAsync(
        ExamRoomCandidate candidate,
        IReadOnlyDictionary<int, int?> mainAnswers,
        int paperLength,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (candidate.MaxFollowUpCount <= 0
            || candidate.ScheduledTime is not DateTime scheduled
            || !CanOpenFollowUpRound(now, scheduled.AddMinutes(candidate.TimePerStudent)))
        {
            return Array.Empty<ExamQuestionInput>();
        }

        // The key is read here, server side; the answers themselves are not saved yet.
        var answered = (await questionRepository.ListCandidateResultsAsync(candidate.CandidateId, cancellationToken: cancellationToken))
            .Where(question => !question.IsFollowUp)
            .Select(question => new FollowUpSource(
                question.ExamQuestionId,
                question.OrderNo,
                question.MaterialId,
                question.Difficulty.ToBusiness(),
                IsCorrectChoice(question, mainAnswers)))
            .ToList();

        var topics = answered
            .Where(question => question.MaterialId.HasValue)
            .Select(question => question.MaterialId!.Value)
            .ToHashSet();

        var pool = (await questionRepository.ListFollowUpPoolAsync(candidate.CourseId, topics, cancellationToken))
            .Select(item => new FollowUpCandidate(
                item.QuestionId,
                item.MaterialId,
                item.Difficulty.ToBusiness()))
            .ToList();

        var usedInSession = await questionRepository.ListAssignedQuestionIdsAsync(candidate.ExamId, cancellationToken);

        return PlanFollowUps(answered, pool, candidate.MaxFollowUpCount, usedInSession)
            .Select((pick, index) => new ExamQuestionInput
            {
                CandidateId = candidate.CandidateId,
                QuestionId = pick.QuestionId,
                ParentExamQuestionId = pick.ParentExamQuestionId,
                OrderNo = paperLength + index + 1
            })
            .ToList();
    }

    private static bool IsCorrectChoice(
        AssignmentPRN.DataAccess.Contracts.ExamResultQuestion question,
        IReadOnlyDictionary<int, int?> answers) =>
        answers.TryGetValue(question.ExamQuestionId, out var picked)
        && picked is int optionId
        && question.Options.Any(option => option.OptionId == optionId && option.IsCorrect);

    /// <summary>
    /// The gate in front of handing a paper in. Deliberately not the same as the gate in
    /// front of starting one: a student who ran out of time while answering still gets to
    /// hand in what they did, within <see cref="ExamSessionRules.SubmitGrace"/>.
    /// </summary>
    private async Task<ExamRoomCandidate> LoadSubmittableCandidateAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken)
    {
        var candidate = await questionRepository.GetExamRoomCandidateAsync(candidateId, cancellationToken)
            ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

        if (candidate.StudentId != studentUserId)
        {
            throw new BusinessValidationException("Đây không phải lượt thi của bạn.");
        }

        if (candidate.CandidateStatus.ToBusiness() == CandidateStatus.Completed)
        {
            throw new BusinessValidationException("Bạn đã nộp bài cho lượt thi này.");
        }

        if (candidate.CandidateStatus.ToBusiness() != CandidateStatus.InProgress)
        {
            throw new BusinessValidationException("Bạn chưa vào ca thi này.");
        }

        if (candidate.ScheduledTime is not DateTime scheduled)
        {
            throw new BusinessValidationException("Lượt thi này chưa được xếp giờ.");
        }

        var endTime = scheduled.AddMinutes(candidate.TimePerStudent);
        if (!ExamSessionRules.CanSubmit(DateTime.Now, scheduled, endTime))
        {
            throw new BusinessValidationException(
                $"Ca thi của bạn đã kết thúc lúc {endTime:HH:mm}, không nộp bài được nữa.");
        }

        return candidate;
    }

    /// <summary>
    /// The gate in front of the exam room: the slot has to belong to the caller, the
    /// session has to be running, and the clock has to be inside the slot.
    /// </summary>
    private async Task<ExamRoomCandidate> LoadSittableCandidateAsync(
        int candidateId,
        int studentUserId,
        CancellationToken cancellationToken)
    {
        var candidate = await questionRepository.GetExamRoomCandidateAsync(candidateId, cancellationToken)
            ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

        if (candidate.StudentId != studentUserId)
        {
            throw new BusinessValidationException("Đây không phải lượt thi của bạn.");
        }

        if (candidate.CandidateStatus.ToBusiness() is CandidateStatus.Cancelled or CandidateStatus.Absent)
        {
            throw new BusinessValidationException("Lượt thi này đã bị huỷ hoặc bạn được ghi nhận vắng thi.");
        }

        if (candidate.CandidateStatus.ToBusiness() == CandidateStatus.Completed)
        {
            throw new BusinessValidationException("Bạn đã hoàn thành lượt thi này.");
        }

        if (!ExamSessionRules.CanSit(candidate.SessionStatus.ToBusiness()))
        {
            throw new BusinessValidationException(
                "Phiên thi chưa mở hoặc đã kết thúc, chưa thể vào thi.");
        }

        if (candidate.ScheduledTime is not DateTime scheduled)
        {
            throw new BusinessValidationException("Lượt thi này chưa được xếp giờ.");
        }

        var now = DateTime.Now;
        if (!ExamSessionRules.IsSlotOpen(now, scheduled, candidate.TimePerStudent))
        {
            throw new BusinessValidationException(
                now < scheduled
                    ? $"Chưa đến giờ thi. Ca của bạn bắt đầu lúc {scheduled:HH:mm} ngày {scheduled:dd/MM}."
                    : $"Ca thi của bạn đã kết thúc lúc {scheduled.AddMinutes(candidate.TimePerStudent):HH:mm}.");
        }

        return candidate;
    }

    /// <summary>
    /// Deals one student's paper, unless they already hold one (a refresh, a double click or
    /// a paper the lecturer dealt ahead). It runs under the exam lock, so everything already
    /// handed out in this session is excluded even when two students enter at once.
    /// </summary>
    private async Task DealToCandidateAsync(
        ExamRoomCandidate candidate,
        CancellationToken cancellationToken)
    {
        await questionRepository.DealAsync(
            candidate.ExamId,
            async dealt =>
            {
                var filter = dealt.Configuration;
                var count = BusinessValidation.InRange(filter.Count, 1, 50, "Số câu hỏi mỗi sinh viên");
                if (dealt.CandidatesWithPaper.Contains(candidate.CandidateId))
                {
                    return Array.Empty<ExamQuestionInput>();
                }

                return await PickPaperAsync(
                        filter,
                        candidate.CandidateId,
                        dealt.TakenQuestionIds.ToHashSet(),
                        cancellationToken)
                    ?? throw new BusinessValidationException(
                        $"Ngân hàng câu hỏi của môn {candidate.CourseCode} không còn đủ {count} câu chưa dùng "
                        + "cho lượt thi này. Hãy báo giảng viên bổ sung câu hỏi.");
            },
            cancellationToken);
    }

    /// <summary>
    /// Draws one student's paper from the pool <paramref name="filter"/> describes, leaving
    /// out <paramref name="taken"/> and adding the drawn questions to it, so calling this for
    /// each student in turn never gives two of them the same question. Returns null when
    /// the pool can no longer fill a whole paper.
    /// </summary>
    private async Task<IReadOnlyList<ExamQuestionInput>?> PickPaperAsync(
        AssignmentPRN.DataAccess.Contracts.QuestionPickRequest filter,
        int candidateId,
        HashSet<int> taken,
        CancellationToken cancellationToken)
    {
        var pool = await questionRepository.ListPoolIdsAsync(
            new AssignmentPRN.DataAccess.Contracts.QuestionPickRequest
            {
                CourseId = filter.CourseId,
                Count = filter.Count,
                MaterialIds = filter.MaterialIds,
                Difficulties = filter.Difficulties,
                TakenQuestionIds = taken
            },
            cancellationToken);

        var picked = PickQuestions(pool, filter.Count, taken);
        if (picked.Count < filter.Count)
        {
            return null;
        }

        taken.UnionWith(picked);
        return picked
            .Select((questionId, index) => new ExamQuestionInput
            {
                CandidateId = candidateId,
                QuestionId = questionId,
                OrderNo = index + 1
            })
            .ToList();
    }

    private static ExamRoomResponse MapRoom(
        ExamRoomCandidate candidate,
        IReadOnlyList<AssignmentPRN.DataAccess.Contracts.ExamRoomQuestion> questions) => new()
    {
        CandidateId = candidate.CandidateId,
        ExamId = candidate.ExamId,
        ExamName = candidate.ExamName,
        CourseCode = candidate.CourseCode,
        CourseName = candidate.CourseName,
        LecturerName = candidate.LecturerName,
        ScheduledTime = candidate.ScheduledTime ?? default,
        EndTime = (candidate.ScheduledTime ?? default).AddMinutes(candidate.TimePerStudent),
        TimePerStudent = candidate.TimePerStudent,
        CandidateStatus = candidate.CandidateStatus.ToBusiness(),
        StartedAt = candidate.StartedAt,
        Questions = questions.Select(item => item.ToBusiness()).ToList(),
        CanAnswer = candidate.CandidateStatus.ToBusiness() == CandidateStatus.InProgress
            && candidate.ScheduledTime is DateTime slot
            && ExamSessionRules.IsSlotOpen(DateTime.Now, slot, candidate.TimePerStudent),
        SecondsRemaining = RemainingSeconds(candidate)
    };

    /// <summary>Whole seconds left in the slot, never negative.</summary>
    private static int RemainingSeconds(ExamRoomCandidate candidate)
    {
        if (candidate.ScheduledTime is not DateTime scheduled)
        {
            return 0;
        }

        var left = scheduled.AddMinutes(candidate.TimePerStudent) - DateTime.Now;
        return left <= TimeSpan.Zero ? 0 : (int)left.TotalSeconds;
    }

    public Task<ServiceResponse<IReadOnlyList<ExamPaperItem>>> GetExamPaperAsync(
        int examId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<IReadOnlyList<ExamPaperItem>>(
            async () =>
            {
                var id = BusinessValidation.PositiveId(examId, "lịch thi");
                return (await questionRepository.ListExamPaperAsync(id, cancellationToken))
                    .Select(item => item.ToBusiness())
                    .ToList();
            },
            "Không thể tải đề thi đã phát.");
    }

    public Task<ServiceResponse> ClearExamAssignmentAsync(
        int examId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var id = BusinessValidation.PositiveId(examId, "lịch thi");

                // Once a student has opened their slot the papers are part of the exam
                // record, so they are frozen rather than reshuffled underneath them.
                if (await questionRepository.HasExamStartedAsync(id, cancellationToken))
                {
                    throw new BusinessValidationException(
                        "Đã có sinh viên vào thi, không thể huỷ đề của lịch thi này.");
                }

                await questionRepository.ClearExamQuestionsAsync(id, cancellationToken);
            },
            "Không thể huỷ đề đã phát.");
    }

    /// <summary>
    /// Copies the filter across and pins the count to the range a request may carry,
    /// so <see cref="QuestionPickRequest.Count"/> cannot smuggle in a negative value.
    /// </summary>
    private static AssignmentPRN.DataAccess.Contracts.QuestionPickRequest BuildPoolRequest(
        int courseId,
        int count,
        QuestionPickRequest request) => new()
    {
        CourseId = courseId,
        Count = count,
        MaterialIds = request.MaterialIds,
        Difficulties = request.Difficulties.Select(item => item.ToDataAccess()).ToList(),
        TakenQuestionIds = request.TakenQuestionIds
    };

    /// <summary>Same filter, applied to the exam assignment request.</summary>
    private static AssignmentPRN.DataAccess.Contracts.QuestionPickRequest BuildPoolRequest(
        int courseId,
        int count,
        ExamQuestionAssignmentRequest request) => new()
    {
        CourseId = courseId,
        Count = count,
        MaterialIds = request.MaterialIds,
        Difficulties = request.Difficulties.Select(item => item.ToDataAccess()).ToList()
    };

    /// <summary>
    /// Trims the choices, drops the blank ones and enforces the multiple-choice shape:
    /// at least two choices and exactly one correct answer.
    /// </summary>
    private async Task EnsureNotDuplicateAsync(
        int courseId,
        string questionText,
        int editingQuestionId,
        CancellationToken cancellationToken)
    {
        var existing = await questionRepository.ListTextMatchesAsync(courseId, cancellationToken);
        EnsureNotDuplicate(existing, questionText, editingQuestionId);
    }

    /// <summary>
    /// Collapses a question to the form used to spot a repeat: lower case, no leading or    /// trailing space and a single space between words. Without this, "  She  goes to
    /// school " would pass as a brand new question.
    /// </summary>
    internal static string FoldForDuplicateCheck(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var character in text.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Refuses a question whose wording already exists in the same course. Editing a
    /// question is allowed, so the question being changed never counts against itself.
    /// </summary>
    private static void EnsureNotDuplicate(
        IReadOnlyList<QuestionTextMatch> existing,
        string questionText,
        int editingQuestionId)
    {
        var folded = FoldForDuplicateCheck(questionText);
        if (folded.Length == 0)
        {
            return;
        }

        var clash = existing.FirstOrDefault(item =>
            item.QuestionId != editingQuestionId
            // Retiring a question and re-adding a better one is allowed, so an archived
            // row never blocks. The repository already leaves those out; the check is
            // repeated here so the rule does not depend on the query.
            && item.Status.ToBusiness() != QuestionStatus.Archived
            && string.Equals(FoldForDuplicateCheck(item.QuestionText), folded, StringComparison.Ordinal));

        if (clash is null)
        {
            return;
        }

        var id = clash.QuestionId;
        throw new BusinessValidationException(
            $"Câu hỏi đã tồn tại trong môn này (mã #{id}, trạng thái {DescribeStatus(clash.Status)}). "
            + "Hãy sửa câu cũ hoặc đổi nội dung câu mới.");
    }

    private static string DescribeStatus(QuestionStatus status) => status switch
    {
        QuestionStatus.Draft => "nháp",
        QuestionStatus.PendingReview => "chờ duyệt",
        QuestionStatus.Approved => "đã duyệt",
        QuestionStatus.Rejected => "bị từ chối",
        QuestionStatus.Archived => "đã lưu trữ",
        _ => status.ToString()
    };

    private static string DescribeStatus(AssignmentPRN.DataAccess.Enums.QuestionStatus status) =>
        DescribeStatus(status.ToBusiness());

    private static IReadOnlyList<QuestionOptionInput> NormaliseOptions(
        IReadOnlyList<QuestionOptionInput>? options,
        string? expectedAnswer)
    {
        options ??= [];
        if (options.Count > MaxOptions)
        {
            throw new BusinessValidationException($"Câu hỏi chỉ được có tối đa {MaxOptions} phương án.");
        }

        var cleaned = new List<QuestionOptionInput>(options.Count);
        foreach (var option in options)
        {
            var text = option.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            if (text.Length > MaxOptionTextLength)
            {
                throw new BusinessValidationException(
                    $"Phương án không được vượt quá {MaxOptionTextLength} ký tự.");
            }

            cleaned.Add(new QuestionOptionInput { Text = text, IsCorrect = option.IsCorrect });
        }

        // No choices at all makes it an oral question: the expected answer is what the
        // AI grades against, so it cannot be left out as well.
        if (cleaned.Count == 0)
        {
            return expectedAnswer is null
                ? throw new BusinessValidationException("Câu hỏi vấn đáp (không có phương án) cần có đáp án mong đợi.")
                : cleaned;
        }

        if (cleaned.Count < MinOptions)
        {
            throw new BusinessValidationException($"Câu hỏi trắc nghiệm cần ít nhất {MinOptions} phương án trả lời.");
        }

        if (cleaned.Count(item => item.IsCorrect) != 1)
        {
            throw new BusinessValidationException("Câu hỏi phải có đúng một phương án đúng.");
        }

        return cleaned;
    }

    private async Task EnsureMaterialBelongsToCourseAsync(
        int materialId,
        int courseId,
        CancellationToken cancellationToken)
    {
        var material = await courseMaterialRepository.GetAsync(materialId, cancellationToken);
        if (material is null || material.CourseId != courseId)
        {
            throw new BusinessValidationException("Tài liệu được chọn không thuộc môn học này.");
        }
    }

    /// <summary>Course ids the caller may act on: all of them for an admin, their own for a lecturer.</summary>
    private async Task<IReadOnlyList<int>> VisibleCourseIdsAsync(
        int? lecturerId,
        CancellationToken cancellationToken)
    {
        var courses = await catalogRepository.ListActiveCoursesAsync(cancellationToken);
        return courses
            .Where(course => !lecturerId.HasValue || course.LecturerId == lecturerId)
            .Select(course => course.CourseId)
            .ToList();
    }

    private async Task EnsureCourseVisibleAsync(
        int courseId,
        int? lecturerId,
        CancellationToken cancellationToken)
    {
        if (!lecturerId.HasValue)
        {
            return;
        }

        var allowed = await VisibleCourseIdsAsync(lecturerId, cancellationToken);
        if (!allowed.Contains(courseId))
        {
            throw new BusinessValidationException("Bạn không có quyền thao tác câu hỏi của môn học này.");
        }
    }

    private static QuestionResponse MapQuestion(QuestionDetail detail) => new()
    {
        QuestionId = detail.QuestionId,
        CourseId = detail.CourseId,
        MaterialId = detail.MaterialId,
        QuestionText = detail.QuestionText,
        ExpectedAnswer = detail.ExpectedAnswer,
        BloomLevel = detail.BloomLevel.ToBusiness(),
        Difficulty = detail.Difficulty.ToBusiness(),
        QuestionType = detail.QuestionType.ToBusiness(),
        Status = detail.Status.ToBusiness(),
        Options = detail.Options
            .Select(option => new QuestionOptionResponse
            {
                Text = option.Text,
                IsCorrect = option.IsCorrect
            })
            .ToList()
    };

    private async Task NotifyCourseChangedAsync(
        int courseId,
        int? actorLecturerId,
        string kind,
        CancellationToken cancellationToken)
    {
        if (realtime is null || courseId <= 0)
        {
            return;
        }

        var lecturerId = actorLecturerId is > 0
            ? actorLecturerId.Value
            : (await catalogRepository.ListActiveCoursesAsync(cancellationToken))
                .FirstOrDefault(course => course.CourseId == courseId)?.LecturerId ?? 0;

        // Do not fabricate an actor for an event. The admins group can still receive
        // system-wide events, but course events need a real course owner/actor.
        if (lecturerId <= 0)
        {
            return;
        }

        await realtime.ChangedAsync(
            new ExamChange(0, lecturerId, Array.Empty<int>(), kind) { CourseId = courseId },
            cancellationToken);
    }

    private async Task NotifyProgressAsync(int examId, string kind, CancellationToken ct)
    {
        if (realtime is null || sessionRepository is null) return;
        var exam = await sessionRepository.GetDetailAsync(examId, ct);
        if (exam is not null) await realtime.ChangedAsync(new(examId, exam.Lecturer.UserId,
            exam.Candidates.Select(x => x.StudentId).ToArray(), kind), ct);
    }

    // ---------------------------------------------------------------------
    // Decisions this service makes on its own: dealing a paper, planning the
    // follow-up round, reading an import file and authorising a draft save.
    // They take every input as an argument and touch no repository, so each one
    // can be exercised directly.
    // ---------------------------------------------------------------------

    /// <summary>
    /// The shuffling half of handing out questions, kept free of any database call so
    /// the "no repeats inside one exam" rule can be tested on its own.
    /// </summary>

    /// <summary>
    /// Takes <paramref name="count"/> questions from <paramref name="pool"/> at random.
    /// Every identifier is returned at most once, so calling this for each student of
    /// the same exam — passing the running total of <paramref name="taken"/> each time —
    /// hands out a different paper to everybody.
    /// </summary>
    internal static IReadOnlyList<int> PickQuestions(
        IReadOnlyList<int> pool,
        int count,
        IReadOnlyCollection<int>? taken = null,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(pool);

        if (count <= 0)
        {
            return Array.Empty<int>();
        }

        var used = taken is null ? new HashSet<int>() : new HashSet<int>(taken);
        var source = random ?? Random.Shared;

        // Copy before shuffling: the caller may hand us a cached list.
        var candidates = pool.Where(id => !used.Contains(id)).ToList();
        ShuffleInPlace(candidates, source);

        return candidates.Take(count).ToList();
    }

    /// <summary>How many questions are still free for an exam that already handed out <paramref name="taken"/>.</summary>
    internal static int CountAvailableQuestions(IReadOnlyList<int> pool, IReadOnlyCollection<int> taken)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(taken);

        var used = new HashSet<int>(taken);
        return pool.Count(id => !used.Contains(id));
    }

    private static void ShuffleInPlace(List<int> values, Random source)
    {
        // Fisher-Yates: unbiased, and unlike OrderBy(_ => random.Next()) it cannot
        // return a different order for the same input.
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = source.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
    /// <summary>
    /// Decides the second round of a paper from how the first one went, kept free of any
    /// database call so the rules can be tested on their own:
    /// <list type="bullet">
    /// <item>a follow-up stays on the topic of the main question it digs into;</item>
    /// <item>a wrong or blank answer gets an easier (or equal) question, to check the basics;</item>
    /// <item>a right answer gets a harder (or equal) question, to see how deep it goes;</item>
    /// <item>wrong answers are served first, and no more than the session's maximum overall;</item>
    /// <item>each main question gets at most one follow-up, never the same question twice.</item>
    /// </list>
    /// </summary>

    /// <summary>
    /// Least time that must be left in the slot to open a second round. Less than this and
    /// the student could not read the questions, so the paper simply closes.
    /// </summary>
    internal static readonly TimeSpan MinimumFollowUpTimeLeft = TimeSpan.FromMinutes(1);

    internal static bool CanOpenFollowUpRound(DateTime now, DateTime slotEnd) => slotEnd - now >= MinimumFollowUpTimeLeft;

    /// <param name="answered">The main questions of the paper with how they were answered.</param>
    /// <param name="pool">Approved follow-up questions of the course.</param>
    /// <param name="maxCount">The session's cap on follow-ups per student.</param>
    /// <param name="usedInSession">
    /// Follow-ups other students of the session already got. They are only avoided, not
    /// banned, so a small bank still serves everybody.
    /// </param>
    internal static IReadOnlyList<FollowUpPick> PlanFollowUps(
        IReadOnlyList<FollowUpSource> answered,
        IReadOnlyList<FollowUpCandidate> pool,
        int maxCount,
        IReadOnlyCollection<int>? usedInSession = null,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(answered);
        ArgumentNullException.ThrowIfNull(pool);

        if (maxCount <= 0 || answered.Count == 0 || pool.Count == 0)
        {
            return Array.Empty<FollowUpPick>();
        }

        var used = usedInSession is null ? new HashSet<int>() : new HashSet<int>(usedInSession);
        var source = random ?? Random.Shared;
        var picked = new HashSet<int>();
        var picks = new List<FollowUpPick>(maxCount);

        var queue = answered
            .OrderBy(item => item.IsCorrect)
            .ThenBy(item => item.OrderNo);

        foreach (var main in queue)
        {
            if (picks.Count == maxCount)
            {
                break;
            }

            if (main.MaterialId is not int materialId)
            {
                continue;
            }

            var questionId = PickFollowUpFor(main, materialId, pool, picked, used, source);
            if (questionId is int id)
            {
                picked.Add(id);
                picks.Add(new FollowUpPick(main.ExamQuestionId, id));
            }
        }

        return picks;
    }

    /// <summary>
    /// The difficulties to try for one main question, nearest first. A wrong answer steps
    /// down towards Easy, a right one steps up towards Hard; the same level comes last.
    /// </summary>
    internal static IReadOnlyList<QuestionDifficulty> FollowUpDifficultyOrder(QuestionDifficulty difficulty, bool isCorrect)
    {
        var level = (int)difficulty;
        var order = new List<QuestionDifficulty>();

        if (isCorrect)
        {
            for (var next = level + 1; next <= (int)QuestionDifficulty.Hard; next++)
            {
                order.Add((QuestionDifficulty)next);
            }
        }
        else
        {
            for (var next = level - 1; next >= (int)QuestionDifficulty.Easy; next--)
            {
                order.Add((QuestionDifficulty)next);
            }
        }

        order.Add(difficulty);
        return order;
    }

    private static int? PickFollowUpFor(
        FollowUpSource main,
        int materialId,
        IReadOnlyList<FollowUpCandidate> pool,
        HashSet<int> picked,
        HashSet<int> used,
        Random source)
    {
        var onTopic = pool
            .Where(item => item.MaterialId == materialId && !picked.Contains(item.QuestionId))
            .ToList();

        foreach (var difficulty in FollowUpDifficultyOrder(main.Difficulty, main.IsCorrect))
        {
            var level = onTopic.Where(item => item.Difficulty == difficulty).ToList();
            if (level.Count == 0)
            {
                continue;
            }

            // Fresh questions first, so two students of one session rarely share a follow-up.
            var fresh = level.Where(item => !used.Contains(item.QuestionId)).ToList();
            var choices = fresh.Count > 0 ? fresh : level;
            return choices[source.Next(choices.Count)].QuestionId;
        }

        return null;
    }
    /// <summary>
    /// Reads the CSV the bank accepts for bulk import. Parsing lives here, apart from the
    /// database, so the column mapping and the error messages can be checked on their own.
    /// </summary>
    /// <remarks>
    /// Expected header row, one column each, separated by <c>;</c> or <c>,</c>:
    /// <c>question_text;difficulty;bloom;material;expected_answer;option_a;option_b;option_c;option_d;correct</c>.
    /// <c>material</c> matches the stored file name of a course material, <c>correct</c> is the
    /// letter of the right choice (<c>A</c>..<c>D</c>, or <c>1</c>..<c>4</c>).
    /// </remarks>

    internal const string QuestionTextColumn = "question_text";

    internal const string DifficultyColumn = "difficulty";

    internal const string BloomColumn = "bloom";

    internal const string MaterialColumn = "material";

    internal const string ExpectedAnswerColumn = "expected_answer";

    internal const string CorrectColumn = "correct";

    private const int MaxOptionColumns = 8;

    /// <summary>
    /// Turns the file body into rows. A row that cannot be read is reported in
    /// <paramref name="errors"/> and skipped, so one bad line does not void the file.
    /// </summary>
    internal static IReadOnlyList<QuestionImportRow> ParseCsv(
        string content,
        IReadOnlyDictionary<string, int> materialIdsByFileName,
        out IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(content);

        var messages = new List<string>();
        var rows = new List<QuestionImportRow>();

        var lines = content
            .Replace("\r\n", "\n")
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
        {
            errors = ["File CSV rỗng."];
            return rows;
        }

        var separator = DetectSeparator(lines[0]);
        var header = SplitRow(lines[0], separator)
            .Select(column => NormaliseHeader(column))
            .ToList();

        if (!header.Contains(QuestionTextColumn))
        {
            errors = [$"File CSV thiếu cột bắt buộc {QuestionTextColumn}."];
            return rows;
        }

        var optionColumns = Enumerable.Range(0, MaxOptionColumns)
            .Select(index => $"option_{(char)('a' + index)}")
            .Where(header.Contains)
            .ToList();

        if (optionColumns.Count < 2)
        {
            errors = ["File CSV cần ít nhất 2 cột phương án (option_a, option_b)."];
            return rows;
        }

        for (var index = 1; index < lines.Count; index++)
        {
            var lineNumber = index + 1;
            var cells = SplitRow(lines[index], separator);

            try
            {
                var row = BuildRow(header, cells, optionColumns, materialIdsByFileName, lineNumber);
                if (row is not null)
                {
                    rows.Add(row);
                }
            }
            catch (Exception exception) when (exception is BusinessValidationException or ArgumentException)
            {
                messages.Add($"Dòng {lineNumber}: {exception.Message}");
            }
        }

        errors = messages;
        return rows;
    }

    /// <summary>Finds a material by its stored file name, ignoring case.</summary>
    internal static int? ResolveCsvMaterialId(
        string? fileName,
        IReadOnlyDictionary<string, int> materialIdsByFileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var key = fileName.Trim();

        if (materialIdsByFileName.TryGetValue(key, out var materialId))
        {
            return materialId;
        }

        foreach (var pair in materialIdsByFileName)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static QuestionImportRow? BuildRow(
        IReadOnlyList<string> header,
        IReadOnlyList<string> cells,
        IReadOnlyList<string> optionColumns,
        IReadOnlyDictionary<string, int> materialIdsByFileName,
        int lineNumber)
    {
        var text = Value(header, cells, QuestionTextColumn);
        if (string.IsNullOrWhiteSpace(text))
        {
            // A trailing blank line that survived the filter: silently skip it.
            return null;
        }

        var options = optionColumns
            .Select(column => new QuestionOptionInput
            {
                Text = Value(header, cells, column) ?? string.Empty
            })
            .Where(option => !string.IsNullOrWhiteSpace(option.Text))
            .ToList();

        // A row with no options is an oral question; one lone option is a mistake.
        if (options.Count == 1)
        {
            throw new BusinessValidationException("Câu trắc nghiệm cần ít nhất 2 phương án trả lời.");
        }

        var correctIndex = options.Count == 0 ? -1 : ResolveCorrectIndex(Value(header, cells, CorrectColumn), options.Count);
        var marked = new List<QuestionOptionInput>(options.Count);
        for (var index = 0; index < options.Count; index++)
        {
            marked.Add(new QuestionOptionInput
            {
                Text = options[index].Text,
                IsCorrect = index == correctIndex
            });
        }

        var materialId = ResolveCsvMaterialId(Value(header, cells, MaterialColumn), materialIdsByFileName);

        return new QuestionImportRow
        {
            SourceLine = lineNumber,
            QuestionText = text,
            ExpectedAnswer = Value(header, cells, ExpectedAnswerColumn),
            MaterialId = materialId,
            Difficulty = ParseDifficulty(Value(header, cells, DifficultyColumn)),
            BloomLevel = ParseBloom(Value(header, cells, BloomColumn)),
            Options = marked
        };
    }

    private static int ResolveCorrectIndex(string? raw, int optionCount)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new BusinessValidationException(
                "Thiếu cột correct, cần chỉ ra phương án đúng (A..D hoặc 1..4).");
        }

        if (value.Length == 1 && char.IsLetter(value[0]))
        {
            var byLetter = char.ToUpperInvariant(value[0]) - 'A';
            if (byLetter >= 0 && byLetter < optionCount)
            {
                return byLetter;
            }
        }

        if (int.TryParse(value, out var byPosition) && byPosition >= 1 && byPosition <= optionCount)
        {
            return byPosition - 1;
        }

        throw new BusinessValidationException($"Phương án đúng \"{value}\" không hợp lệ.");
    }

    private static QuestionDifficulty ParseDifficulty(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return QuestionDifficulty.Medium;
        }

        return value.ToLowerInvariant() switch
        {
            "easy" or "de" or "dễ" or "de_easy" => QuestionDifficulty.Easy,
            "hard" or "kho" or "khó" => QuestionDifficulty.Hard,
            _ => QuestionDifficulty.Medium
        };
    }

    private static BloomLevel ParseBloom(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return BloomLevel.Understand;
        }

        return value.ToLowerInvariant() switch
        {
            "remember" or "nho" or "nhớ" => BloomLevel.Remember,
            "apply" or "ap_dung" or "áp dụng" => BloomLevel.Apply,
            "analyze" or "analyse" or "phan_tich" or "phân tích" => BloomLevel.Analyze,
            _ => BloomLevel.Understand
        };
    }

    private static string? Value(IReadOnlyList<string> header, IReadOnlyList<string> cells, string column)
    {
        var index = header.ToList().IndexOf(column);
        if (index < 0 || index >= cells.Count)
        {
            return null;
        }

        var value = cells[index].Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static char DetectSeparator(string headerLine) =>
        headerLine.Count(item => item == ';') >= headerLine.Count(item => item == ',') ? ';' : ',';

    /// <summary>
    /// Splits one line, honouring double quotes so a question containing a comma or a
    /// semicolon still lands in a single cell.
    /// </summary>
    private static List<string> SplitRow(string line, char separator)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (character == '"')
            {
                // A doubled quote inside a quoted cell is one literal quote.
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (character == separator && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        cells.Add(current.ToString());
        return cells;
    }

    private static string NormaliseHeader(string column) => column
        .Trim()
        .Trim('﻿')
        .ToLowerInvariant()
        .Replace(' ', '_');
    /// <summary>Draft authorization and round rules; runs on the transaction's current snapshot.</summary>

    private static IReadOnlyDictionary<int, int?> ValidateDraft(
        ExamDraftState? state, int studentId, IReadOnlyDictionary<int, int?> answers, DateTime now)
    {
        if (state is null) throw new BusinessValidationException("Không tìm thấy lượt thi.");
        if (state.StudentId != studentId || state.CandidateStatus.ToBusiness() != CandidateStatus.InProgress
            || !ExamSessionRules.CanSit(state.SessionStatus.ToBusiness()))
            throw new BusinessValidationException("Bạn không được lưu đáp án cho lượt thi này.");
        if (state.ScheduledTime is not DateTime start || !ExamSessionRules.IsSlotOpen(now, start, state.TimePerStudent))
            throw new BusinessValidationException("Đã hết giờ làm bài, không thể lưu tạm.");

        var followUp = state.Questions.Any(x => x.IsFollowUp);
        var open = state.Questions.Where(x => x.IsFollowUp == followUp).ToDictionary(x => x.ExamQuestionId);
        foreach (var (id, option) in answers)
        {
            if (!open.TryGetValue(id, out var question) || (option.HasValue && !question.OptionIds.Contains(option.Value)))
                throw new BusinessValidationException("Câu hỏi hoặc đáp án không thuộc vòng thi hiện tại.");
            if (question.IsSubmitted)
                throw new BusinessValidationException("Vòng đã nộp không được sửa.");
        }
        return answers;
    }
}

/// <summary>A main question the student has just answered, as the planner needs to see it.</summary>
public sealed record FollowUpSource(
    int ExamQuestionId,
    int OrderNo,
    int? MaterialId,
    QuestionDifficulty Difficulty,
    bool IsCorrect);

/// <summary>A follow-up question in the bank that could be asked.</summary>
public sealed record FollowUpCandidate(int QuestionId, int MaterialId, QuestionDifficulty Difficulty);

/// <summary>One follow-up to deal: which bank question, digging into which main slot.</summary>
public sealed record FollowUpPick(int ParentExamQuestionId, int QuestionId);
