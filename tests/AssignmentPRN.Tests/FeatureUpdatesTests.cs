using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Business.Services;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CandidateStatus = AssignmentPRN.DataAccess.Enums.CandidateStatus;
using ExamSessionStatus = AssignmentPRN.DataAccess.Enums.ExamSessionStatus;

namespace AssignmentPRN.Tests;

public sealed class FeatureUpdatesTests
{
    private sealed class FixedHolisticHttpClientFactory(string innerJson) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new FixedHandler(innerJson));

        private sealed class FixedHandler(string innerJson) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var envelope = System.Text.Json.JsonSerializer.Serialize(new { response = innerJson });
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(envelope, System.Text.Encoding.UTF8, "application/json")
                });
            }
        }
    }

    private sealed class StubOllamaOptions : Microsoft.Extensions.Options.IOptions<AssignmentPRN.Business.OllamaOptions>
    {
        public AssignmentPRN.Business.OllamaOptions Value { get; } = new();
    }

    private sealed class NullLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    [Fact]
    public async Task Holistic_ai_grading_works_when_rubric_has_no_criteria()
    {
        await using var db = await IssuedPaperEditTests.OpenAsync();
        db.Roles.Add(new Role { RoleId = 3, RoleName = "Student" });
        db.Users.Add(new User { UserId = 2, RoleId = 3, FullName = "Student", Email = "s@test.local", PasswordHash = "x", Status = "Active" });
        var candidate = await db.ExamCandidates.SingleAsync();
        candidate.StudentId = 2;
        candidate.Status = CandidateStatus.Completed;
        candidate.FinishedAt = DateTime.Now;
        var eq = await db.ExamQuestions.SingleAsync();
        db.Answers.Add(new Answer
        {
            AnswerId = 1,
            ExamQuestionId = eq.ExamQuestionId,
            CandidateId = candidate.CandidateId,
            Transcript = "SQL JOIN dùng để kết nối dữ liệu từ nhiều bảng dựa trên khóa."
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var jsonClient = new OllamaJsonClient(
            new FixedHolisticHttpClientFactory("""{"score":8.5,"strengths":"Nắm đúng khái niệm JOIN.","weaknesses":"Chưa nêu các loại JOIN.","missingPoints":"Thiếu ví dụ INNER/LEFT JOIN.","confidence":0.92}"""),
            new StubOllamaOptions(),
            new NullLogger<OllamaJsonClient>());
        var grader = new OllamaAnswerGrader(jsonClient);
        var gradingService = new GradingService(new GradingRepository(db, new ExamStatePolicy()), grader, TimeProvider.System);

        var outcome = await gradingService.GradeCandidateAsync(candidate.CandidateId);
        Assert.True(outcome.Success, outcome.Error);
        Assert.Equal(1, outcome.Data!.GradedAnswers);
        Assert.Equal(0, outcome.Data.SkippedAnswers);
        Assert.Equal(8.5m, outcome.Data.SlotScore);

        var eval = await db.AiEvaluations.SingleAsync();
        Assert.Equal(8.5m, eval.AiScore);
        Assert.Contains("JOIN", eval.Strengths);
        var finalResult = await db.FinalResults.SingleAsync();
        Assert.Equal(8.5m, finalResult.AiScore);
        Assert.Equal(8.5m, finalResult.FinalScore);
    }

    [Fact]
    public async Task ResultReview_computes_question_total_and_average_and_logs_per_question_adjustment()
    {
        await using var db = await IssuedPaperEditTests.OpenAsync();
        db.Roles.Add(new Role { RoleId = 3, RoleName = "Student" });
        db.Users.Add(new User { UserId = 2, RoleId = 3, FullName = "Student", Email = "s2@test.local", PasswordHash = "x", Status = "Active" });
        var candidate = await db.ExamCandidates.SingleAsync();
        candidate.StudentId = 2;
        candidate.Status = CandidateStatus.Completed;

        var eq1 = await db.ExamQuestions.SingleAsync();
        db.Questions.Add(new Question { QuestionId = 2, CourseId = 1, CreatedBy = 1, QuestionText = "Q2" });
        var eq2 = new ExamQuestion { ExamQuestionId = 2, ExamId = 1, CandidateId = candidate.CandidateId, QuestionId = 2, OrderNo = 2 };
        db.ExamQuestions.Add(eq2);

        db.Answers.Add(new Answer { AnswerId = 10, ExamQuestionId = eq1.ExamQuestionId, CandidateId = candidate.CandidateId, Transcript = "Ans 1" });
        db.Answers.Add(new Answer { AnswerId = 11, ExamQuestionId = 2, CandidateId = candidate.CandidateId, Transcript = "Ans 2" });
        db.AiEvaluations.Add(new AiEvaluation { AnswerId = 10, AiScore = 7.0m, EvaluatedAt = DateTime.Now });
        db.AiEvaluations.Add(new AiEvaluation { AnswerId = 11, AiScore = 9.0m, EvaluatedAt = DateTime.Now });
        db.FinalResults.Add(new FinalResult { ExamId = 1, CandidateId = candidate.CandidateId, AiScore = 8.0m, FinalScore = 8.0m });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var policy = new ExamStatePolicy();
        var reviewService = new ResultReviewService(
            new GradingRepository(db, policy),
            new ExamResultService(new ExamResultRepository(db, policy), new QuestionRepository(db, policy)),
            TimeProvider.System);

        var review = (await reviewService.GetAsync(candidate.CandidateId, 1)).Data!;
        Assert.Equal(2, review.GradedQuestionCount);
        Assert.Equal(16.0m, review.QuestionTotalScore);
        Assert.Equal(8.0m, review.QuestionAverageScore);
        Assert.Contains(review.Answers, a => a.Transcript == "Ans 1");

        var studentReview = (await reviewService.GetForStudentAsync(candidate.CandidateId, 2)).Data!;
        Assert.All(studentReview.Answers, a => Assert.Null(a.ExpectedAnswer));

        var adj = await reviewService.AdjustAsync(
            candidate.CandidateId, 1, 8.75m, "Cộng thêm điểm trình bày",
            [new QuestionScoreAdjustment(1, 8.0m), new QuestionScoreAdjustment(2, 9.5m)],
            1);
        Assert.True(adj.Success, adj.Error);

        var updated = (await reviewService.GetAsync(candidate.CandidateId, 1)).Data!;
        Assert.Equal(8.75m, updated.FinalScore);
        Assert.Contains(updated.Trail, t => t.Description.Contains("Câu 1: 8") && t.Description.Contains("Câu 2: 9.5"));
    }

    [Fact]
    public async Task Schedules_are_ordered_newest_first_for_lecturer_and_student()
    {
        await using var db = await IssuedPaperEditTests.OpenAsync();
        db.Roles.Add(new Role { RoleId = 3, RoleName = "Student" });
        db.Users.Add(new User { UserId = 2, RoleId = 3, FullName = "Student", Email = "s3@test.local", PasswordHash = "x", Status = "Active" });
        var c1 = await db.ExamCandidates.SingleAsync();
        c1.StudentId = 2;
        c1.ScheduledTime = DateTime.Now.AddHours(1);

        var exam1 = await db.ExamSessions.SingleAsync();
        exam1.StartTime = DateTime.Now.AddHours(1);
        exam1.EndTime = DateTime.Now.AddHours(2);

        db.ExamSessions.Add(new ExamSession
        {
            ExamId = 2,
            CourseId = 1,
            LecturerId = 1,
            ExamName = "Newer Exam",
            StartTime = DateTime.Now.AddHours(5),
            EndTime = DateTime.Now.AddHours(6),
            TimePerStudent = 20,
            MainQuestionCount = 1,
            Status = ExamSessionStatus.Scheduled
        });
        db.ExamCandidates.Add(new ExamCandidate
        {
            CandidateId = 2,
            ExamId = 2,
            StudentId = 2,
            ScheduledTime = DateTime.Now.AddHours(5),
            Status = CandidateStatus.Waiting
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repo = new ExamSessionRepository(db, new ExamStatePolicy());
        var list = await repo.ListAsync();
        Assert.Equal([2, 1], list.Select(x => x.ExamId).ToArray());

        var schedule = await repo.GetStudentScheduleAsync(2);
        Assert.Equal([2, 1], schedule!.Items.Select(x => x.ExamId).ToArray());
    }

    [Fact]
    public async Task Create_with_selected_students_schedules_only_chosen_students()
    {
        await using var db = await IssuedPaperEditTests.OpenAsync();
        db.Roles.Add(new Role { RoleId = 3, RoleName = "Student" });
        var s1 = new User { UserId = 2, RoleId = 3, FullName = "An", Email = "an@test.local", PasswordHash = "x", Status = "Active" };
        var s2 = new User { UserId = 3, RoleId = 3, FullName = "Binh", Email = "binh@test.local", PasswordHash = "x", Status = "Active" };
        db.Users.AddRange(s1, s2);
        db.AcademicClasses.Add(new AcademicClass { ClassId = 1, ClassCode = "SE1801", ClassName = "SE1801", CourseId = 1, LecturerId = 1, IsActive = true });
        db.ClassStudents.AddRange(
            new ClassStudent { ClassId = 1, StudentId = 2 },
            new ClassStudent { ClassId = 1, StudentId = 3 });
        db.Questions.AddRange(
            new Question { QuestionId = 2, CourseId = 1, CreatedBy = 1, QuestionText = "Q2", Status = AssignmentPRN.DataAccess.Enums.QuestionStatus.Approved },
            new Question { QuestionId = 3, CourseId = 1, CreatedBy = 1, QuestionText = "Q3", Status = AssignmentPRN.DataAccess.Enums.QuestionStatus.Approved });
        var q1 = await db.Questions.SingleAsync(q => q.QuestionId == 1);
        q1.Status = AssignmentPRN.DataAccess.Enums.QuestionStatus.Approved;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var policy = new ExamStatePolicy();
        var service = new ExamSessionService(
            new ExamSessionRepository(db, policy),
            new CatalogRepository(db),
            new QuestionRepository(db, policy));

        var tomorrow = DateTime.Today.AddDays(1).AddHours(9);
        var created = await service.CreateAsync(new ExamSessionCreateRequest
        {
            CourseId = 1,
            LecturerId = 1,
            ClassId = 1,
            ExamName = "Selected Students Test",
            StartTime = tomorrow,
            TimePerStudent = 15,
            MainQuestionCount = 1,
            MaxFollowUpCount = 1,
            SelectedStudentIds = [3],
            FilterStudentsBySelection = true
        });

        Assert.True(created.Success, created.Error);
        var cand = Assert.Single(created.Data!.Candidates);
        Assert.Equal(3, cand.StudentId);
    }

    [Fact]
    public async Task Completed_session_allows_continuing_for_new_attempt_and_deals_questions()
    {
        await using var db = await IssuedPaperEditTests.OpenAsync();
        db.Roles.Add(new Role { RoleId = 3, RoleName = "Student" });
        db.Users.Add(new User { UserId = 2, RoleId = 3, FullName = "Student", Email = "retake@test.local", PasswordHash = "x", Status = "Active" });

        var exam = await db.ExamSessions.SingleAsync();
        exam.Status = ExamSessionStatus.Completed;
        exam.StartTime = DateTime.Now.AddMinutes(-30);
        exam.EndTime = DateTime.Now.AddMinutes(-10);

        var candidate = await db.ExamCandidates.SingleAsync();
        candidate.StudentId = 2;
        candidate.Status = CandidateStatus.Completed;
        candidate.ScheduledTime = DateTime.Now.AddMinutes(-30);
        candidate.StartedAt = DateTime.Now.AddMinutes(-30);
        candidate.FinishedAt = DateTime.Now.AddMinutes(-20);

        var q1 = await db.Questions.SingleAsync(q => q.QuestionId == 1);
        q1.Status = AssignmentPRN.DataAccess.Enums.QuestionStatus.Approved;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var policy = new ExamStatePolicy();
        var sessionService = new ExamSessionService(
            new ExamSessionRepository(db, policy),
            new CatalogRepository(db),
            new QuestionRepository(db, policy));
        var questionService = new QuestionService(new QuestionRepository(db, policy), new CatalogRepository(db), null!);

        var continued = await sessionService.ContinueAsStudentAsync(1, 2);
        Assert.True(continued.Success, continued.Error);
        Assert.Equal(AssignmentPRN.Business.Interfaces.ExamSessionStatus.InProgress, continued.Data!.Status);
        Assert.Equal(2, continued.Data.Candidates.Count);

        var newSlot = continued.Data.Candidates.OrderByDescending(c => c.CandidateId).First();
        Assert.Equal(AssignmentPRN.Business.Interfaces.CandidateStatus.Waiting, newSlot.Status);

        var entered = await questionService.EnterExamAsync(newSlot.CandidateId, 2);
        Assert.True(entered.Success, entered.Error);
        Assert.Single(entered.Data!.Questions);
    }

    [Fact]
    public void SessionResultResponse_AverageScore_is_null_when_completed_candidate_awaits_ai_score()
    {
        var session = new SessionResultResponse
        {
            ExamId = 65,
            Status = AssignmentPRN.Business.Interfaces.ExamSessionStatus.Completed,
            Candidates =
            [
                new CandidateResultItemResponse
                {
                    CandidateId = 1,
                    Status = AssignmentPRN.Business.Interfaces.CandidateStatus.Completed,
                    Score = null
                }
            ]
        };

        Assert.Equal(1, session.CompletedCount);
        Assert.Null(session.AverageScore);
    }

    private sealed class GeminiEnvelopeHttpClientFactory(Action<HttpRequestMessage, string>? capture = null) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(capture));

        private sealed class Handler(Action<HttpRequestMessage, string>? capture) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var bodyText = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
                capture?.Invoke(request, bodyText);
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new
                            {
                                parts = new[] { new { text = "```json\n{\"score\":9.0,\"strengths\":\"Tot\",\"weaknesses\":\"Khong\",\"missingPoints\":\"\",\"confidence\":0.95}\n```" } }
                            }
                        }
                    }
                });
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
                };
            }
        }
    }

    private sealed class StubGeminiOptions(AssignmentPRN.Business.GeminiOptions value)
        : Microsoft.Extensions.Options.IOptions<AssignmentPRN.Business.GeminiOptions>
    {
        public AssignmentPRN.Business.GeminiOptions Value { get; } = value;
    }

    [Fact]
    public async Task GeminiJsonClient_sanitizes_schema_and_parses_structured_json_response()
    {
        HttpRequestMessage? capturedReq = null;
        string capturedBody = "";
        var geminiOpts = new AssignmentPRN.Business.GeminiOptions
        {
            Provider = "Gemini",
            ApiKey = "test-secret-key",
            Model = "gemini-2.5-flash"
        };
        var client = new GeminiJsonClient(
            new GeminiEnvelopeHttpClientFactory((req, body) => { capturedReq = req; capturedBody = body; }),
            new StubGeminiOptions(geminiOpts),
            new NullLogger<GeminiJsonClient>());

        var schemaWithIntEnum = new
        {
            type = "object",
            properties = new
            {
                criteriaId = new { type = "integer", @enum = new[] { 1, 2 } },
                reason = new { type = "string", @enum = new[] { "Sufficient", "Vague" } }
            },
            required = new[] { "criteriaId", "reason" }
        };

        using var doc = await client.GenerateAsync("Cham bai", schemaWithIntEnum, TimeSpan.FromSeconds(10), 400);
        Assert.NotNull(doc);
        Assert.Equal(9.0m, doc!.RootElement.GetProperty("score").GetDecimal());
        Assert.NotNull(capturedReq);
        Assert.Equal("test-secret-key", capturedReq!.Headers.GetValues("x-goog-api-key").Single());
        Assert.Equal("test-secret-key", capturedReq!.Headers.GetValues("x-goog-api-key").Single());

        // Integer enum stripped for Gemini compatibility; string enum preserved
        using var reqJson = System.Text.Json.JsonDocument.Parse(capturedBody);
        var props = reqJson.RootElement.GetProperty("generationConfig").GetProperty("responseSchema").GetProperty("properties");
        Assert.False(props.GetProperty("criteriaId").TryGetProperty("enum", out _));
        Assert.True(props.GetProperty("reason").TryGetProperty("enum", out _));
    }

    [Fact]
    public void RoutingAiJsonClient_selects_provider_from_options()
    {
        var geminiClient = new GeminiJsonClient(
            new GeminiEnvelopeHttpClientFactory(),
            new StubGeminiOptions(new AssignmentPRN.Business.GeminiOptions { ApiKey = "k", Model = "gemini-2.5-flash" }),
            new NullLogger<GeminiJsonClient>());
        var ollamaClient = new OllamaJsonClient(
            new GeminiEnvelopeHttpClientFactory(),
            new StubOllamaOptions(),
            new NullLogger<OllamaJsonClient>());

        var autoWithKey = new RoutingAiJsonClient(
            new StubGeminiOptions(new AssignmentPRN.Business.GeminiOptions { Provider = "Auto", ApiKey = "k", Model = "gemini-2.5-flash" }),
            geminiClient,
            ollamaClient);
        Assert.Equal("gemini-2.5-flash", autoWithKey.Model);

        var forcedOllama = new RoutingAiJsonClient(
            new StubGeminiOptions(new AssignmentPRN.Business.GeminiOptions { Provider = "Ollama", ApiKey = "k", Model = "gemini-2.5-flash" }),
            geminiClient,
            ollamaClient);
        Assert.Equal(ollamaClient.Model, forcedOllama.Model);

        var geminiOnlyWithoutFallback = new RoutingAiJsonClient(
            new StubGeminiOptions(new AssignmentPRN.Business.GeminiOptions { Provider = "Gemini", ApiKey = "", Model = "gemini-2.5-flash" }),
            geminiClient,
            ollamaClient);
        Assert.Equal("gemini-2.5-flash", geminiOnlyWithoutFallback.Model);
    }

    private sealed class EmbeddingHttpClientFactory : IHttpClientFactory
    {
        public string? CapturedUrl { get; private set; }
        public string CapturedBody { get; private set; } = "";

        public HttpClient CreateClient(string name) => new(new Handler(this));

        private sealed class Handler(EmbeddingHttpClientFactory parent) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                parent.CapturedUrl = request.RequestUri?.ToString();
                parent.CapturedBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    embedding = new { values = new[] { 0.1f, 0.2f, 0.3f } }
                });
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
                };
            }
        }
    }

    [Fact]
    public async Task GeminiJsonClient_EmbedAsync_calls_embedContent_with_768_dimensions()
    {
        var factory = new EmbeddingHttpClientFactory();
        var client = new GeminiJsonClient(
            factory,
            new StubGeminiOptions(new AssignmentPRN.Business.GeminiOptions { Provider = "Gemini", ApiKey = "embed-key", EmbeddingModel = "gemini-embedding-001" }),
            new NullLogger<GeminiJsonClient>());

        var vec = await client.EmbedAsync("Hello RAG");
        Assert.NotNull(vec);
        Assert.Equal([0.1f, 0.2f, 0.3f], vec);
        Assert.Contains("gemini-embedding-001:embedContent", factory.CapturedUrl);
        Assert.Contains("\"outputDimensionality\":768", factory.CapturedBody.Replace(" ", ""));
    }

    [Fact]
    public async Task PendingCandidateIdsAsync_skips_mcq_empty_and_disqualified_and_orders_newest_first()
    {
        await using var db = await IssuedPaperEditTests.OpenAsync();
        db.Roles.Add(new Role { RoleId = 3, RoleName = "Student" });
        var baseCandidate = await db.ExamCandidates.SingleAsync();
        baseCandidate.Status = CandidateStatus.InProgress; // ignore default seeded candidate

        var opt = new QuestionOption { OptionId = 1, QuestionId = 1, OptionText = "A", IsCorrect = true, DisplayOrder = 1 };
        db.QuestionOptions.Add(opt);
        await db.SaveChangesAsync();

        var nextUserId = 10;
        var nextCandidateId = 10;
        var nextEqId = 10;

        async Task<int> AddCandidateAsync(string email, DateTime finishedAt, string? transcript, int? selectedOptionId = null, bool disqualified = false)
        {
            var uid = nextUserId++;
            var cid = nextCandidateId++;
            var eqid = nextEqId++;
            db.Users.Add(new User { UserId = uid, RoleId = 3, FullName = email, Email = email, PasswordHash = "x", Status = "Active" });
            db.ExamCandidates.Add(new ExamCandidate { CandidateId = cid, ExamId = 1, StudentId = uid, Status = CandidateStatus.Completed, FinishedAt = finishedAt });
            db.ExamQuestions.Add(new ExamQuestion { ExamQuestionId = eqid, ExamId = 1, CandidateId = cid, QuestionId = 1, OrderNo = 1 });
            db.Answers.Add(new Answer { CandidateId = cid, ExamQuestionId = eqid, Transcript = transcript, SelectedOptionId = selectedOptionId });
            if (disqualified)
            {
                db.Set<ExamLog>().Add(new ExamLog { CandidateId = cid, UserId = uid, Action = "CHEAT_DISQUALIFIED", Description = "Cheat", Timestamp = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
            return cid;
        }

        var oldValid = await AddCandidateAsync("old_valid@aives.test", DateTime.UtcNow.AddHours(-3), "Older valid spoken answer");
        _ = await AddCandidateAsync("empty@aives.test", DateTime.UtcNow.AddHours(-1), "   ");
        _ = await AddCandidateAsync("mcq@aives.test", DateTime.UtcNow.AddMinutes(-30), null, selectedOptionId: opt.OptionId);
        _ = await AddCandidateAsync("cheat@aives.test", DateTime.UtcNow.AddMinutes(-10), "Cheated answer", disqualified: true);
        var newValid = await AddCandidateAsync("new_valid@aives.test", DateTime.UtcNow.AddMinutes(-5), "Newest valid spoken answer");

        var repo = new GradingRepository(db, new ExamStatePolicy());
        var pending = await repo.PendingCandidateIdsAsync();

        Assert.Contains(newValid, pending);
        Assert.Contains(oldValid, pending);
        Assert.True(pending.ToList().IndexOf(newValid) < pending.ToList().IndexOf(oldValid));
        Assert.Equal(2, pending.Count);
    }

    [Fact]
    public async Task Scheduling_checks_conflict_per_student_only_and_ignores_completed_slots()
    {
        await using var db = await IssuedPaperEditTests.OpenAsync();
        db.Roles.Add(new Role { RoleId = 3, RoleName = "Student" });
        db.Users.AddRange(
            new User { UserId = 2, RoleId = 3, FullName = "An", Email = "an_sched@test.local", PasswordHash = "x", Status = "Active" },
            new User { UserId = 3, RoleId = 3, FullName = "Binh", Email = "binh_sched@test.local", PasswordHash = "x", Status = "Active" });
        db.AcademicClasses.Add(new AcademicClass { ClassId = 1, ClassCode = "SE1801", ClassName = "SE1801", CourseId = 1, LecturerId = 1, IsActive = true });
        db.ClassStudents.AddRange(
            new ClassStudent { ClassId = 1, StudentId = 2 },
            new ClassStudent { ClassId = 1, StudentId = 3 });
        var q1 = await db.Questions.SingleAsync(q => q.QuestionId == 1);
        q1.Status = AssignmentPRN.DataAccess.Enums.QuestionStatus.Approved;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var policy = new ExamStatePolicy();
        var service = new ExamSessionService(
            new ExamSessionRepository(db, policy),
            new CatalogRepository(db),
            new QuestionRepository(db, policy));

        var slotStart = DateTime.Today.AddDays(1).AddHours(10);

        // 1. Lecturer creates session 1 for Student 2 at 10:00
        var s1 = await service.CreateAsync(new ExamSessionCreateRequest
        {
            CourseId = 1,
            LecturerId = 1,
            ClassId = 1,
            ExamName = "Session 1",
            StartTime = slotStart,
            TimePerStudent = 30,
            MainQuestionCount = 1,
            MaxFollowUpCount = 0,
            SelectedStudentIds = [2],
            FilterStudentsBySelection = true
        });
        Assert.True(s1.Success, s1.Error);

        // 2. Same lecturer creates session 2 for Student 3 at the exact same time -> succeeds (no lecturer conflict)
        var s2 = await service.CreateAsync(new ExamSessionCreateRequest
        {
            CourseId = 1,
            LecturerId = 1,
            ClassId = 1,
            ExamName = "Session 2",
            StartTime = slotStart,
            TimePerStudent = 30,
            MainQuestionCount = 1,
            MaxFollowUpCount = 0,
            SelectedStudentIds = [3],
            FilterStudentsBySelection = true
        });
        Assert.True(s2.Success, s2.Error);

        // 3. Same student 2 cannot be double-booked while slot in Session 1 is still Waiting
        var conflict = await service.CreateAsync(new ExamSessionCreateRequest
        {
            CourseId = 1,
            LecturerId = 1,
            ClassId = 1,
            ExamName = "Session 3 Conflict",
            StartTime = slotStart,
            TimePerStudent = 30,
            MainQuestionCount = 1,
            MaxFollowUpCount = 0,
            SelectedStudentIds = [2],
            FilterStudentsBySelection = true
        });
        Assert.False(conflict.Success);
        Assert.Contains("sinh viên An", conflict.Error);

        // 4. Once student 2's slot in Session 1 is Completed, booking at 10:00 succeeds
        var c2 = await db.ExamCandidates.SingleAsync(x => x.ExamId == s1.Data!.ExamId && x.StudentId == 2);
        c2.Status = CandidateStatus.Completed;
        await db.SaveChangesAsync();

        var afterCompleted = await service.CreateAsync(new ExamSessionCreateRequest
        {
            CourseId = 1,
            LecturerId = 1,
            ClassId = 1,
            ExamName = "Session 3 After Completed",
            StartTime = slotStart,
            TimePerStudent = 30,
            MainQuestionCount = 1,
            MaxFollowUpCount = 0,
            SelectedStudentIds = [2],
            FilterStudentsBySelection = true
        });
        Assert.True(afterCompleted.Success, afterCompleted.Error);
    }
}
