using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

public interface IQuestionRepository
{
    /// <summary>Reads a current snapshot, invokes the business validator and saves its result in one transaction.</summary>
    Task SaveDraftAsync(int candidateId, Func<ExamDraftState?, IReadOnlyDictionary<int, int?>> validate,
        CancellationToken cancellationToken = default);
    Task<QuestionPickRequest> GetExamConfigurationAsync(int examId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuestionListItem>> ListAsync(
        QuestionQuery query,
        CancellationToken cancellationToken = default);

    Task<QuestionDetail?> GetDetailAsync(int questionId, CancellationToken cancellationToken = default);

    /// <summary>Creates the question when <see cref="QuestionUpsertInput.QuestionId"/> is 0, otherwise replaces its body and choices.</summary>
    Task<QuestionDetail> SaveAsync(QuestionUpsertInput input, int authorId, CancellationToken cancellationToken = default);

    /// <summary>Hides a question without touching exams that already reference it.</summary>
    Task ArchiveAsync(int questionId, CancellationToken cancellationToken = default);

    /// <summary>True when at least one exam slot already holds this question.</summary>
    Task<bool> IsAssignedToExamAsync(int questionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the review status of an AI-generated question. Only callable on a question
    /// that is still pending review.
    /// </summary>
    Task SetReviewStatusAsync(
        int questionId,
        QuestionStatus status,
        int reviewerId,
        DateTime reviewedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the rubric for a question, if it exists.</summary>
    Task<RubricDetail?> GetRubricAsync(int questionId, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a rubric with its criteria for a question.</summary>
    Task<RubricDetail> SaveRubricAsync(RubricUpsertInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes a rubric and its criteria.</summary>
    Task DeleteRubricAsync(int rubricId, CancellationToken cancellationToken = default);

    /// <summary>Lists rubrics for questions in a course, with optional search.</summary>
    Task<IReadOnlyList<RubricListItem>> ListRubricsAsync(int? courseId, string? term, CancellationToken cancellationToken = default);

    /// <summary>Gets the question ID and course ID for a rubric.</summary>
    Task<(int QuestionId, int CourseId)?> GetRubricQuestionAndCourseAsync(int rubricId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Id, text and status of every live question in the course, so the service can turn
    /// away a question that repeats one already there. Archived rows are excluded.
    /// </summary>
    Task<IReadOnlyList<QuestionTextMatch>> ListTextMatchesAsync(
        int courseId,
        CancellationToken cancellationToken = default);

    /// <summary>Identifiers of every approved question still free for the exam being built.</summary>
    Task<IReadOnlyList<int>> ListPoolIdsAsync(QuestionPickRequest request, CancellationToken cancellationToken = default);

    /// <summary>How many approved main questions the course can deal from, the same pool <see cref="ListPoolIdsAsync"/> reads.</summary>
    Task<int> CountMainPoolAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>Identifiers of the candidates registered for the exam.</summary>
    Task<IReadOnlyList<int>> ListCandidateIdsAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Every question id already written to the exam, across all its candidates.</summary>
    Task<IReadOnlyList<int>> ListAssignedQuestionIdsAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deals papers for one exam while the exam row is locked, so two deals of the same exam
    /// (two students entering at once, a double click, a lecturer dealing ahead) never read
    /// the same "already dealt" state. <paramref name="planner"/> receives what is dealt at
    /// that moment and returns the rows to add; they are written in the same transaction,
    /// and an exception from the planner leaves the exam untouched.
    /// </summary>
    Task DealAsync(
        int examId,
        Func<ExamDealState, Task<IReadOnlyList<ExamQuestionInput>>> planner,
        CancellationToken cancellationToken = default,
        QuestionPickRequest? configuration = null);

    /// <summary>Every candidate of the exam with the paper the bank dealt them.</summary>
    Task<IReadOnlyList<ExamPaperItem>> ListExamPaperAsync(
        int examId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True once any student of the exam has opened their slot, or any question has been
    /// asked or answered. A paper is dealt the moment a student enters, before anything is
    /// asked, so the questions alone cannot tell.
    /// </summary>
    Task<bool> HasExamStartedAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Drops the exam's papers so they can be dealt again. Returns how many rows went.</summary>
    Task<int> ClearExamQuestionsAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>One slot with its session, for the exam room to check before letting a student in.</summary>
    Task<ExamRoomCandidate?> GetExamRoomCandidateAsync(
        int candidateId,
        CancellationToken cancellationToken = default);

    /// <summary>One candidate's paper as the student sees it, without the answer key.</summary>
    Task<IReadOnlyList<ExamRoomQuestion>> ListCandidateQuestionsAsync(
        int candidateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One completed paper with the student's choices and answer key. The expected answer
    /// is examiner material, so it is only read when <paramref name="includeExpectedAnswer"/> is set.
    /// </summary>
    Task<IReadOnlyList<ExamResultQuestion>> ListCandidateResultsAsync(
        int candidateId,
        bool includeExpectedAnswer = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the slot as being sat. Only the first entry records the clock, so refreshing
    /// the page does not restart the exam.
    /// </summary>
    Task StartCandidateAsync(int candidateId, DateTime startedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the candidate's choices for one round. Answers are written per exam question,
    /// one row each, so a resubmit overwrites rather than piles up. With no
    /// <paramref name="followUps"/> the slot closes; otherwise they are dealt as the next
    /// round and the slot stays open. Either way it is a single transaction.
    /// </summary>
    Task SubmitAnswersAsync(
        int candidateId,
        IReadOnlyDictionary<int, int?> selectedOptionByExamQuestion,
        DateTime finishedAt,
        IReadOnlyList<ExamQuestionInput> followUps,
        CancellationToken cancellationToken = default);

    /// <summary>Approved follow-up questions of the course filed under the given topics.</summary>
    Task<IReadOnlyList<FollowUpPoolItem>> ListFollowUpPoolAsync(
        int courseId,
        IReadOnlyCollection<int> materialIds,
        CancellationToken cancellationToken = default);
}

public class QuestionRepository(AivesDbContext context, IExamStatePolicy policy) : IQuestionRepository
{
    private async Task EnsureNotVoiceRoomAsync(int candidateId, CancellationToken ct)
    {
        if (await context.Set<ExamLog>().AnyAsync(x => x.CandidateId == candidateId
                && x.Action == VoiceRoomRepository.StartedAction, ct))
            throw new ArgumentException("Lượt thi đang ở chế độ giọng nói. Hãy tiếp tục phòng thi giọng nói từ lịch thi.");
    }

    public async Task SaveDraftAsync(int candidateId, Func<ExamDraftState?, IReadOnlyDictionary<int, int?>> validate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validate);
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        await EnsureNotVoiceRoomAsync(candidateId, cancellationToken);
        var candidate = await context.ExamCandidates.AsNoTracking().Include(x => x.Session)
            .SingleOrDefaultAsync(x => x.CandidateId == candidateId, cancellationToken);
        var slots = await context.ExamQuestions.AsNoTracking().Include(x => x.Question).ThenInclude(x => x.Options)
            .Where(x => x.CandidateId == candidateId).ToListAsync(cancellationToken);
        var existing = await context.Answers.Where(x => x.CandidateId == candidateId).ToListAsync(cancellationToken);
        var submittedIds = await context.Answers.AsNoTracking().Where(x => x.CandidateId == candidateId && x.FinishedAt != null)
            .Select(x => x.ExamQuestionId).ToListAsync(cancellationToken);
        var state = candidate is null ? null : new ExamDraftState {
            StudentId = candidate.StudentId, CandidateStatus = candidate.Status, SessionStatus = candidate.Session.Status,
            ScheduledTime = candidate.ScheduledTime, TimePerStudent = candidate.Session.TimePerStudent,
            Questions = slots.Select(x => new ExamDraftQuestion {
                ExamQuestionId = x.ExamQuestionId, IsFollowUp = x.ParentExamQuestionId.HasValue,
                IsSubmitted = submittedIds.Contains(x.ExamQuestionId),
                OptionIds = x.Question.Options.Select(option => option.OptionId).ToHashSet()
            }).ToList()
        };
        // No mutation before Business has validated the whole request.
        var answers = validate(state);
        var now = DateTime.Now;
        var slotsById = slots.ToDictionary(x => x.ExamQuestionId);
        foreach (var (id, option) in answers)
        {
            var slot = slotsById[id];
            var answer = existing.SingleOrDefault(x => x.ExamQuestionId == id);
            if (answer is null)
            {
                answer = new Answer { CandidateId = candidateId, ExamQuestionId = id, CreatedAt = now, StartedAt = slot.AskedAt };
                context.Answers.Add(answer);
            }
            answer.SelectedOptionId = option;
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    public async Task<QuestionPickRequest> GetExamConfigurationAsync(int examId, CancellationToken cancellationToken = default)
    {
        var session = await context.ExamSessions.AsNoTracking().SingleAsync(x => x.ExamId == examId, cancellationToken);
        return ReadConfiguration(session);
    }

    private static QuestionPickRequest ReadConfiguration(ExamSession session)
    {
        var scope = session.QuestionScopeJson is null ? new QuestionPickRequest()
            : System.Text.Json.JsonSerializer.Deserialize<QuestionPickRequest>(session.QuestionScopeJson)!;
        return new QuestionPickRequest { CourseId = session.CourseId, Count = session.MainQuestionCount,
            MaterialIds = scope.MaterialIds, Difficulties = scope.Difficulties };
    }
    public async Task<IReadOnlyList<QuestionListItem>> ListAsync(
        QuestionQuery query,
        CancellationToken cancellationToken = default)
    {
        var questions = context.Questions.AsNoTracking().AsQueryable();

        if (query.CourseId is int courseId)
        {
            questions = questions.Where(question => question.CourseId == courseId);
        }

        if (query.MaterialId is int materialId)
        {
            questions = questions.Where(question => question.SourceMaterialId == materialId);
        }

        if (query.Difficulty is QuestionDifficulty difficulty)
        {
            questions = questions.Where(question => question.Difficulty == difficulty);
        }

        if (query.BloomLevel is BloomLevel bloomLevel)
        {
            questions = questions.Where(question => question.BloomLevel == bloomLevel);
        }

        if (query.Status is QuestionStatus status)
        {
            questions = questions.Where(question => question.Status == status);
        }

        if (!query.IncludeArchived)
        {
            questions = questions.Where(question => question.Status != QuestionStatus.Archived);
        }

        var term = query.Term?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            questions = questions.Where(question =>
                question.QuestionText.Contains(term) || (question.ExpectedAnswer != null && question.ExpectedAnswer.Contains(term)));
        }

        return await questions
            .OrderBy(question => question.Course.CourseCode)
            .ThenByDescending(question => question.CreatedAt)
            .Select(question => new QuestionListItem
            {
                QuestionId = question.QuestionId,
                CourseId = question.CourseId,
                CourseCode = question.Course.CourseCode,
                CourseName = question.Course.CourseName,
                MaterialId = question.SourceMaterialId,
                MaterialName = question.SourceMaterial == null ? null : question.SourceMaterial.FileName,
                QuestionText = question.QuestionText,
                Difficulty = question.Difficulty,
                BloomLevel = question.BloomLevel,
                QuestionType = question.QuestionType,
                Status = question.Status,
                Source = question.Source,
                AuthorName = question.CreatedByUser.FullName,
                CreatedAt = question.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<QuestionDetail?> GetDetailAsync(int questionId, CancellationToken cancellationToken = default)
    {
        if (questionId <= 0)
        {
            return null;
        }

        var question = await context.Questions
            .AsNoTracking()
            .Include(item => item.Options)
            .FirstOrDefaultAsync(item => item.QuestionId == questionId, cancellationToken);

        return question is null ? null : MapDetail(question);
    }

    public async Task<QuestionDetail> SaveAsync(
        QuestionUpsertInput input,
        int authorId,
        CancellationToken cancellationToken = default)
    {
        var options = input.Options
            .Select((option, index) => new QuestionOption
            {
                OptionText = option.Text,
                IsCorrect = option.IsCorrect,
                DisplayOrder = index + 1
            })
            .ToList();

        if (input.QuestionId == 0)
        {
            var initialStatus = input.Source == QuestionSource.AI
                ? QuestionStatus.PendingReview
                : QuestionStatus.Approved;

            var created = new Question
            {
                CourseId = input.CourseId,
                SourceMaterialId = input.MaterialId,
                CreatedBy = authorId,
                QuestionText = input.QuestionText,
                ExpectedAnswer = input.ExpectedAnswer,
                BloomLevel = input.BloomLevel,
                Difficulty = input.Difficulty,
                QuestionType = input.QuestionType,
                Status = initialStatus,
                Source = input.Source,
                CreatedAt = DateTime.Now,
                Options = options
            };

            context.Questions.Add(created);
            await context.SaveChangesAsync(cancellationToken);
            return (await GetDetailAsync(created.QuestionId, cancellationToken))!;
        }

        var existing = await context.Questions
            .Include(question => question.Options)
            .FirstOrDefaultAsync(question => question.QuestionId == input.QuestionId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy câu hỏi.");

        // Replacing the collection removes the old rows and inserts the new ones, so
        // option identifiers are never reused for a different choice.
        context.QuestionOptions.RemoveRange(existing.Options);
        existing.CourseId = input.CourseId;
        existing.SourceMaterialId = input.MaterialId;
        existing.QuestionText = input.QuestionText;
        existing.ExpectedAnswer = input.ExpectedAnswer;
        existing.BloomLevel = input.BloomLevel;
        existing.Difficulty = input.Difficulty;
        existing.QuestionType = input.QuestionType;
        existing.UpdatedAt = DateTime.Now;
        existing.Options = options;

        await context.SaveChangesAsync(cancellationToken);
        return (await GetDetailAsync(existing.QuestionId, cancellationToken))!;
    }

    public async Task ArchiveAsync(int questionId, CancellationToken cancellationToken = default)
    {
        var question = await context.Questions.FindAsync([questionId], cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy câu hỏi.");

        question.Status = QuestionStatus.Archived;
        question.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetReviewStatusAsync(
        int questionId,
        QuestionStatus status,
        int reviewerId,
        DateTime reviewedAt,
        CancellationToken cancellationToken = default)
    {
        var question = await context.Questions.FindAsync([questionId], cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy câu hỏi.");

        question.Status = status;
        question.ReviewedBy = reviewerId;
        question.ReviewedAt = reviewedAt;
        question.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<RubricDetail?> GetRubricAsync(int questionId, CancellationToken cancellationToken = default)
    {
        var rubric = await context.Rubrics
            .AsNoTracking()
            .Include(r => r.Question)
            .Include(r => r.Criteria)
            .FirstOrDefaultAsync(r => r.QuestionId == questionId, cancellationToken);

        if (rubric is null)
        {
            return null;
        }

        return new RubricDetail
        {
            RubricId = rubric.RubricId,
            QuestionId = rubric.QuestionId,
            QuestionText = rubric.Question?.QuestionText ?? string.Empty,
            TotalScore = rubric.TotalScore,
            CreatedAt = rubric.CreatedAt,
            UpdatedAt = rubric.UpdatedAt,
            Criteria = rubric.Criteria
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new RubricCriteriaDetail
                {
                    CriteriaId = c.CriteriaId,
                    CriteriaName = c.CriteriaName,
                    Description = c.Description,
                    MaxScore = c.MaxScore,
                    DisplayOrder = c.DisplayOrder
                })
                .ToList()
        };
    }

    public async Task<RubricDetail> SaveRubricAsync(RubricUpsertInput input, CancellationToken cancellationToken = default)
    {
        var question = await context.Questions.FindAsync([input.QuestionId], cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy câu hỏi.");

        Rubric rubric;
        if (input.RubricId == 0)
        {
            rubric = new Rubric
            {
                QuestionId = input.QuestionId,
                CreatedAt = DateTime.Now,
                Criteria = new List<RubricCriteria>()
            };
            context.Rubrics.Add(rubric);
        }
        else
        {
            rubric = await context.Rubrics
                .Include(r => r.Criteria)
                .FirstOrDefaultAsync(r => r.RubricId == input.RubricId, cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy bộ tiêu chí.");

            if (rubric.QuestionId != input.QuestionId)
                throw new InvalidOperationException("Bộ tiêu chí không thuộc câu hỏi đã chọn.");

        }

        var submittedIds = input.Criteria
            .Where(c => c.CriteriaId > 0)
            .Select(c => c.CriteriaId)
            .ToHashSet();
        if (submittedIds.Any(id => rubric.Criteria.All(c => c.CriteriaId != id)))
            throw new InvalidOperationException("Tiêu chí không thuộc bộ tiêu chí đã chọn.");

        var removed = rubric.Criteria.Where(c => !submittedIds.Contains(c.CriteriaId)).ToList();
        if (removed.Count > 0)
        {
            var removedIds = removed.Select(c => c.CriteriaId).ToArray();
            var hasHistoricalMarks = await context.AiEvaluationCriteria
                .AnyAsync(mark => removedIds.Contains(mark.CriteriaId), cancellationToken);
            if (hasHistoricalMarks)
                throw new InvalidOperationException("Không thể xóa tiêu chí đã được dùng để chấm bài.");
            context.RubricCriteria.RemoveRange(removed);
        }

        decimal totalScore = 0;
        foreach (var criterionInput in input.Criteria.OrderBy(c => c.DisplayOrder))
        {
            if (criterionInput.MaxScore <= 0)
            {
                throw new ArgumentException($"Tiêu chí '{criterionInput.CriteriaName}' phải có điểm lớn hơn 0.");
            }

            var criterion = criterionInput.CriteriaId > 0
                ? rubric.Criteria.Single(c => c.CriteriaId == criterionInput.CriteriaId)
                : new RubricCriteria { RubricId = rubric.RubricId };
            criterion.CriteriaName = criterionInput.CriteriaName;
            criterion.Description = criterionInput.Description;
            criterion.MaxScore = criterionInput.MaxScore;
            criterion.DisplayOrder = criterionInput.DisplayOrder;
            if (criterionInput.CriteriaId == 0) rubric.Criteria.Add(criterion);
            totalScore += criterionInput.MaxScore;
        }

        rubric.TotalScore = totalScore;
        rubric.UpdatedAt = DateTime.Now;

        await context.SaveChangesAsync(cancellationToken);
        return (await GetRubricAsync(input.QuestionId, cancellationToken))!;
    }

    public async Task DeleteRubricAsync(int rubricId, CancellationToken cancellationToken = default)
    {
        var rubric = await context.Rubrics.FindAsync([rubricId], cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy bộ tiêu chí.");

        context.Rubrics.Remove(rubric);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RubricListItem>> ListRubricsAsync(int? courseId, string? term, CancellationToken cancellationToken = default)
    {
        var query = context.Rubrics
            .AsNoTracking()
            .Include(r => r.Question)
                .ThenInclude(q => q.Course)
            .Include(r => r.Criteria)
            .AsQueryable();

        if (courseId is int cId)
        {
            query = query.Where(r => r.Question.CourseId == cId);
        }

        var searchTerm = term?.Trim();
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(r =>
                r.Question.QuestionText.Contains(searchTerm) ||
                r.Question.Course.CourseCode.Contains(searchTerm) ||
                r.Question.Course.CourseName.Contains(searchTerm));
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RubricListItem
            {
                RubricId = r.RubricId,
                QuestionId = r.QuestionId,
                QuestionText = r.Question.QuestionText,
                CourseId = r.Question.CourseId,
                CourseCode = r.Question.Course.CourseCode,
                CourseName = r.Question.Course.CourseName,
                TotalScore = r.TotalScore,
                CriteriaCount = r.Criteria.Count,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<(int QuestionId, int CourseId)?> GetRubricQuestionAndCourseAsync(int rubricId, CancellationToken cancellationToken = default)
    {
        return await context.Rubrics
            .AsNoTracking()
            .Include(r => r.Question)
            .Where(r => r.RubricId == rubricId)
            .Select(r => new ValueTuple<int, int>(r.QuestionId, r.Question.CourseId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> IsAssignedToExamAsync(int questionId, CancellationToken cancellationToken = default) =>
        context.ExamQuestions.AnyAsync(item => item.QuestionId == questionId, cancellationToken);

    public async Task<IReadOnlyList<QuestionTextMatch>> ListTextMatchesAsync(
        int courseId,
        CancellationToken cancellationToken = default)
    {
        // Only the id, text and status are needed to spot a repeat, and a question bank
        // for one course is small enough to compare in memory once the rows are trimmed
        // the same way on both sides.
        return await context.Questions
            .AsNoTracking()
            .Where(question => question.CourseId == courseId
                && question.Status != QuestionStatus.Archived)
            .Select(question => new QuestionTextMatch
            {
                QuestionId = question.QuestionId,
                QuestionText = question.QuestionText,
                Status = question.Status,
                MaterialId = question.SourceMaterialId
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> ListPoolIdsAsync(
        QuestionPickRequest request,
        CancellationToken cancellationToken = default)
    {
        var pool = context.Questions
            .AsNoTracking()
            .Where(question => question.CourseId == request.CourseId
                && question.QuestionType == QuestionType.Main
                && question.Status == QuestionStatus.Approved);

        if (request.MaterialIds.Count > 0)
        {
            var materialIds = request.MaterialIds;
            pool = pool.Where(question => question.SourceMaterialId.HasValue
                && materialIds.Contains(question.SourceMaterialId.Value));
        }

        if (request.Difficulties.Count > 0)
        {
            var difficulties = request.Difficulties;
            pool = pool.Where(question => difficulties.Contains(question.Difficulty));
        }

        if (request.TakenQuestionIds.Count > 0)
        {
            var taken = request.TakenQuestionIds;
            pool = pool.Where(question => !taken.Contains(question.QuestionId));
        }

        return await pool
            .OrderBy(question => question.QuestionId)
            .Select(question => question.QuestionId)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountMainPoolAsync(int courseId, CancellationToken cancellationToken = default) =>
        context.Questions.CountAsync(
            question => question.CourseId == courseId
                && question.QuestionType == QuestionType.Main
                && question.Status == QuestionStatus.Approved,
            cancellationToken);

    public async Task<IReadOnlyList<int>> ListCandidateIdsAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        await context.ExamCandidates
            .AsNoTracking()
            .Where(candidate => candidate.ExamId == examId)
            .OrderBy(candidate => candidate.CandidateId)
            .Select(candidate => candidate.CandidateId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> ListAssignedQuestionIdsAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        await context.ExamQuestions
            .AsNoTracking()
            .Where(item => item.ExamId == examId)
            .Select(item => item.QuestionId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task DealAsync(
        int examId,
        Func<ExamDealState, Task<IReadOnlyList<ExamQuestionInput>>> planner,
        CancellationToken cancellationToken = default,
        QuestionPickRequest? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(planner);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Row lock on the exam: a second deal of the same exam waits here until this one
        // commits, then reads the rows this one wrote.
        if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite") await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT exam_id FROM exam_sessions WHERE exam_id = {examId} FOR UPDATE",
            cancellationToken);

        var session = await context.ExamSessions.SingleAsync(x => x.ExamId == examId, cancellationToken);
        await context.Entry(session).ReloadAsync(cancellationToken);
        var savedConfiguration = ReadConfiguration(session);

        var dealt = await context.ExamQuestions
            .AsNoTracking()
            .Where(item => item.ExamId == examId)
            .Select(item => new { item.CandidateId, item.QuestionId })
            .ToListAsync(cancellationToken);

        if (configuration is not null)
        {
            if (configuration.CourseId != session.CourseId || configuration.Count != session.MainQuestionCount)
                throw new ArgumentException("Số câu và môn học phải khớp cấu hình phiên thi. Hãy tải lại trang.");
            if (dealt.Count > 0 && (!configuration.MaterialIds.ToHashSet().SetEquals(savedConfiguration.MaterialIds)
                || !configuration.Difficulties.ToHashSet().SetEquals(savedConfiguration.Difficulties)))
                throw new ArgumentException("Phiên đã có đề. Hãy huỷ đề trước khi đổi chủ đề hoặc độ khó.");
            if (configuration.Difficulties.Any(x => !Enum.IsDefined(x)))
                throw new ArgumentException("Độ khó không hợp lệ.");
            var materialIds = configuration.MaterialIds.Distinct().ToList();
            if (await context.CourseMaterials.CountAsync(x => materialIds.Contains(x.MaterialId)
                    && x.CourseId == session.CourseId, cancellationToken) != materialIds.Count)
                throw new ArgumentException("Chủ đề không thuộc môn học của phiên thi.");
            savedConfiguration = configuration;
        }

        var rows = await planner(new ExamDealState
        {
            Configuration = savedConfiguration,
            TakenQuestionIds = dealt.Select(item => item.QuestionId).ToHashSet(),
            CandidatesWithPaper = dealt.Select(item => item.CandidateId).ToHashSet()
        });

        if (configuration is not null)
            session.QuestionScopeJson = System.Text.Json.JsonSerializer.Serialize(new QuestionPickRequest {
                MaterialIds = configuration.MaterialIds.Distinct().Order().ToList(),
                Difficulties = configuration.Difficulties.Distinct().Order().ToList() });

        // One SaveChanges inside the transaction: either every slot lands or none does,
        // which keeps a candidate from ending up with half a paper.
        context.ExamQuestions.AddRange(rows.Select(row => new ExamQuestion
        {
            ExamId = examId,
            CandidateId = row.CandidateId,
            QuestionId = row.QuestionId,
            ParentExamQuestionId = row.ParentExamQuestionId,
            OrderNo = row.OrderNo,
            AskedAt = null,
            IsCompleted = false
        }));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExamPaperItem>> ListExamPaperAsync(
        int examId,
        CancellationToken cancellationToken = default)
    {
        // Every candidate is listed, dealt or not, so a half-finished assignment is visible
        // instead of silently looking like an exam with fewer students.
        var candidates = await context.ExamCandidates
            .AsNoTracking()
            .Where(candidate => candidate.ExamId == examId)
            .OrderBy(candidate => candidate.ScheduledTime)
            .ThenBy(candidate => candidate.CandidateId)
            .Select(candidate => new
            {
                candidate.CandidateId,
                candidate.Student.FullName,
                candidate.Student.Email,
                candidate.ScheduledTime,
                HasStarted = candidate.StartedAt != null
                    || candidate.Status == CandidateStatus.InProgress
                    || candidate.Status == CandidateStatus.Completed
            })
            .ToListAsync(cancellationToken);

        var papers = await context.ExamQuestions
            .AsNoTracking()
            .Where(item => item.ExamId == examId)
            .OrderBy(item => item.OrderNo)
            .Select(item => new
            {
                item.CandidateId,
                Question = new ExamPaperQuestion
                {
                    ExamQuestionId = item.ExamQuestionId,
                    QuestionId = item.QuestionId,
                    OrderNo = item.OrderNo,
                    QuestionText = item.Question.QuestionText,
                    Difficulty = item.Question.Difficulty,
                    MaterialName = item.Question.SourceMaterial == null
                        ? null
                        : item.Question.SourceMaterial.FileName,
                    IsCompleted = item.IsCompleted
                }
            })
            .ToListAsync(cancellationToken);

        var byCandidate = papers
            .GroupBy(item => item.CandidateId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Question).ToList());

        return candidates
            .Select(candidate => new ExamPaperItem
            {
                CandidateId = candidate.CandidateId,
                StudentName = candidate.FullName,
                StudentEmail = candidate.Email,
                ScheduledTime = candidate.ScheduledTime,
                HasStarted = candidate.HasStarted,
                Questions = byCandidate.TryGetValue(candidate.CandidateId, out var questions)
                    ? questions
                    : []
            })
            .ToList();
    }

    public async Task<bool> HasExamStartedAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        await context.ExamCandidates.AnyAsync(
            candidate => candidate.ExamId == examId
                && (candidate.StartedAt != null
                    || candidate.Status == CandidateStatus.InProgress
                    || candidate.Status == CandidateStatus.Completed),
            cancellationToken)
        || await context.ExamQuestions.AnyAsync(
            item => item.ExamId == examId && (item.IsCompleted || item.AskedAt != null),
            cancellationToken);

    public async Task<int> ClearExamQuestionsAsync(
        int examId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.ExamQuestions
            .Where(item => item.ExamId == examId)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return 0;
        }

        context.ExamQuestions.RemoveRange(rows);
        await context.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    public Task<ExamRoomCandidate?> GetExamRoomCandidateAsync(
        int candidateId,
        CancellationToken cancellationToken = default) =>
        candidateId <= 0
            ? Task.FromResult<ExamRoomCandidate?>(null)
            : context.ExamCandidates
                .AsNoTracking()
                .Where(candidate => candidate.CandidateId == candidateId)
                .Select(candidate => new ExamRoomCandidate
                {
                    CandidateId = candidate.CandidateId,
                    ExamId = candidate.ExamId,
                    StudentId = candidate.StudentId,
                    ExamName = candidate.Session.ExamName,
                    CourseId = candidate.Session.CourseId,
                    CourseCode = candidate.Session.Course.CourseCode,
                    CourseName = candidate.Session.Course.CourseName,
                    LecturerName = candidate.Session.Lecturer.FullName,
                    ScheduledTime = candidate.ScheduledTime,
                    TimePerStudent = candidate.Session.TimePerStudent,
                    MainQuestionCount = candidate.Session.MainQuestionCount,
                    MaxFollowUpCount = candidate.Session.MaxFollowUpCount,
                    CandidateStatus = candidate.Status,
                    SessionStatus = candidate.Session.Status,
                    StartedAt = candidate.StartedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ExamRoomQuestion>> ListCandidateQuestionsAsync(
        int candidateId,
        CancellationToken cancellationToken = default) =>
        await context.ExamQuestions
            .AsNoTracking()
            .Where(item => item.CandidateId == candidateId)
            .OrderBy(item => item.OrderNo)
            .Select(item => new ExamRoomQuestion
            {
                ExamQuestionId = item.ExamQuestionId,
                OrderNo = item.OrderNo,
                QuestionText = item.Question.QuestionText,
                Difficulty = item.Question.Difficulty,
                // Only the id and the text of each choice are projected, so is_correct
                // never leaves the database on this path.
                Options = item.Question.Options
                    .OrderBy(option => option.DisplayOrder)
                    .Select(option => new ExamRoomOption
                    {
                        OptionId = option.OptionId,
                        Text = option.OptionText
                    })
                    .ToList(),
                SelectedOptionId = context.Answers
                    .Where(answer => answer.ExamQuestionId == item.ExamQuestionId)
                    .Select(answer => answer.SelectedOptionId)
                    .FirstOrDefault(),
                ParentExamQuestionId = item.ParentExamQuestionId
            })
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExamResultQuestion>> ListCandidateResultsAsync(
        int candidateId,
        bool includeExpectedAnswer = false,
        CancellationToken cancellationToken = default) =>
        await context.ExamQuestions
            .AsNoTracking()
            .Where(item => item.CandidateId == candidateId)
            .OrderBy(item => item.OrderNo)
            .Select(item => new ExamResultQuestion
            {
                ExamQuestionId = item.ExamQuestionId,
                OrderNo = item.OrderNo,
                QuestionText = item.Question.QuestionText,
                Difficulty = item.Question.Difficulty,
                Options = item.Question.Options
                    .OrderBy(option => option.DisplayOrder)
                    .Select(option => new ExamResultOption
                    {
                        OptionId = option.OptionId,
                        Text = option.OptionText,
                        IsCorrect = option.IsCorrect
                    })
                    .ToList(),
                SelectedOptionId = context.Answers
                    .Where(answer => answer.ExamQuestionId == item.ExamQuestionId)
                    .Select(answer => answer.SelectedOptionId)
                    .FirstOrDefault(),
                MaterialId = item.Question.SourceMaterialId,
                ParentExamQuestionId = item.ParentExamQuestionId,
                ExpectedAnswer = includeExpectedAnswer ? item.Question.ExpectedAnswer : null
            })
            .ToListAsync(cancellationToken);

    public async Task SubmitAnswersAsync(
        int candidateId,
        IReadOnlyDictionary<int, int?> selectedOptionByExamQuestion,
        DateTime finishedAt,
        IReadOnlyList<ExamQuestionInput> followUps,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(followUps);

        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        await EnsureNotVoiceRoomAsync(candidateId, cancellationToken);
        var currentCandidate = await context.ExamCandidates.SingleAsync(x => x.CandidateId == candidateId, cancellationToken);
        if (currentCandidate.Status != CandidateStatus.InProgress)
            throw new ArgumentException("Lượt thi không còn nhận bài nộp.");

        var slots = await context.ExamQuestions
            .Where(item => item.CandidateId == candidateId)
            .ToListAsync(cancellationToken);

        var existing = await context.Answers
            .Where(answer => answer.CandidateId == candidateId)
            .ToListAsync(cancellationToken);

        var isFollowUpRound = slots.Any(x => x.ParentExamQuestionId.HasValue);
        if (selectedOptionByExamQuestion.Keys.Any(id => !slots.Any(x => x.ExamQuestionId == id
                && x.ParentExamQuestionId.HasValue == isFollowUpRound))
            || existing.Any(x => selectedOptionByExamQuestion.ContainsKey(x.ExamQuestionId) && x.FinishedAt.HasValue))
            throw new ArgumentException("Vòng thi đã thay đổi hoặc đã nộp. Hãy tải lại trang.");

        foreach (var slot in slots)
        {
            if (!selectedOptionByExamQuestion.TryGetValue(slot.ExamQuestionId, out var optionId))
            {
                continue;
            }

            var answer = existing.FirstOrDefault(item => item.ExamQuestionId == slot.ExamQuestionId);
            if (answer is null)
            {
                answer = new Answer
                {
                    ExamQuestionId = slot.ExamQuestionId,
                    CandidateId = candidateId,
                    CreatedAt = finishedAt,
                    StartedAt = slot.AskedAt
                };
                context.Answers.Add(answer);
            }

            answer.SelectedOptionId = optionId;
            answer.FinishedAt = finishedAt;

            slot.IsCompleted = optionId.HasValue;
            slot.AskedAt ??= finishedAt;
        }

        var candidate = await context.ExamCandidates
            .FirstOrDefaultAsync(item => item.CandidateId == candidateId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lượt thi.");

        if (followUps.Count == 0)
        {
            candidate.Status = CandidateStatus.Completed;
            candidate.FinishedAt = finishedAt;
        }
        else
        {
            // The next round is put to the student right now, so it is asked at this moment.
            context.ExamQuestions.AddRange(followUps.Select(row => new ExamQuestion
            {
                ExamId = candidate.ExamId,
                CandidateId = candidateId,
                QuestionId = row.QuestionId,
                ParentExamQuestionId = row.ParentExamQuestionId,
                OrderNo = row.OrderNo,
                AskedAt = finishedAt,
                IsCompleted = false
            }));
        }

        // One SaveChanges: the answers and either the closed slot or the next round land
        // together or not at all.
        await context.SaveChangesAsync(cancellationToken);
        await ExamSessionLifecycle.SynchronizeAsync(context, policy, candidate.ExamId, finishedAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FollowUpPoolItem>> ListFollowUpPoolAsync(
        int courseId,
        IReadOnlyCollection<int> materialIds,
        CancellationToken cancellationToken = default)
    {
        if (materialIds.Count == 0)
        {
            return Array.Empty<FollowUpPoolItem>();
        }

        return await context.Questions
            .AsNoTracking()
            .Where(question => question.CourseId == courseId
                && question.QuestionType == QuestionType.FollowUp
                && question.Status == QuestionStatus.Approved
                && question.SourceMaterialId.HasValue
                && materialIds.Contains(question.SourceMaterialId.Value))
            .OrderBy(question => question.QuestionId)
            .Select(question => new FollowUpPoolItem
            {
                QuestionId = question.QuestionId,
                MaterialId = question.SourceMaterialId!.Value,
                Difficulty = question.Difficulty
            })
            .ToListAsync(cancellationToken);
    }

    public async Task StartCandidateAsync(
        int candidateId,
        DateTime startedAt,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        await EnsureNotVoiceRoomAsync(candidateId, cancellationToken);
        var candidate = await context.ExamCandidates.Include(x => x.Session)
            .FirstOrDefaultAsync(item => item.CandidateId == candidateId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lượt thi.");

        if (!policy.CanSit(candidate.Session.Status)
            || candidate.Status is not (CandidateStatus.Waiting or CandidateStatus.InProgress)
            || candidate.ScheduledTime is not DateTime scheduled
            || !policy.IsSlotOpen(DateTime.Now, scheduled, candidate.Session.TimePerStudent))
            throw new ArgumentException("Phiên thi hoặc lượt thi không còn cho phép vào thi.");

        if (candidate.Status == CandidateStatus.Waiting)
        {
            candidate.Status = CandidateStatus.InProgress;
        }

        // Keep the first timestamp: a refresh must not look like a fresh start.
        candidate.StartedAt ??= startedAt;
        await context.SaveChangesAsync(cancellationToken);
        await ExamSessionLifecycle.SynchronizeAsync(context, policy, candidate.ExamId, startedAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static QuestionDetail MapDetail(Question question) => new()
    {
        QuestionId = question.QuestionId,
        CourseId = question.CourseId,
        MaterialId = question.SourceMaterialId,
        QuestionText = question.QuestionText,
        ExpectedAnswer = question.ExpectedAnswer,
        BloomLevel = question.BloomLevel,
        Difficulty = question.Difficulty,
        QuestionType = question.QuestionType,
        Status = question.Status,
        Source = question.Source,
        ReviewedBy = question.ReviewedBy,
        ReviewedAt = question.ReviewedAt,
        Options = question.Options
            .OrderBy(option => option.DisplayOrder)
            .Select(option => new QuestionOptionDetail
            {
                OptionId = option.OptionId,
                Text = option.OptionText,
                IsCorrect = option.IsCorrect,
                DisplayOrder = option.DisplayOrder
            })
            .ToList()
    };
}
