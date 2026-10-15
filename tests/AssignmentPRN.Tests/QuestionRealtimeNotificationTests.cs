using System.Reflection;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using BusinessQuestionOptionInput = AssignmentPRN.Business.Interfaces.QuestionOptionInput;

namespace AssignmentPRN.Tests;

public sealed class QuestionRealtimeNotificationTests
{
    [Fact]
    public async Task Save_success_notifies_the_persisted_course()
    {
        var notifier = new RecordingNotifier();
        var service = CreateService(notifier);

        var result = await service.SaveAsync(ValidSaveRequest(17), lecturerId: 7, ownerLecturerId: 7);

        Assert.True(result.Success);
        var change = Assert.Single(notifier.Changes);
        Assert.Equal(17, change.CourseId);
        Assert.Equal(7, change.LecturerId);
        Assert.Equal("questions", change.Kind);
        Assert.Equal(0, change.ExamId);
    }

    [Fact]
    public async Task Failed_save_does_not_notify()
    {
        var notifier = new RecordingNotifier();
        var service = CreateService(notifier, saveException: new InvalidOperationException());

        var result = await service.SaveAsync(ValidSaveRequest(17), lecturerId: 7, ownerLecturerId: 7);

        Assert.False(result.Success);
        Assert.Empty(notifier.Changes);
    }

    [Fact]
    public async Task Archive_success_notifies_the_course_owner()
    {
        var notifier = new RecordingNotifier();
        var service = CreateService(notifier);

        var result = await service.ArchiveAsync(9, lecturerId: null);

        Assert.True(result.Success);
        var change = Assert.Single(notifier.Changes);
        Assert.Equal(17, change.CourseId);
        Assert.Equal(7, change.LecturerId);
        Assert.Equal("questions", change.Kind);
    }

    [Fact]
    public async Task Failed_archive_does_not_notify()
    {
        var notifier = new RecordingNotifier();
        var service = CreateService(notifier, archiveException: new InvalidOperationException());

        var result = await service.ArchiveAsync(9, lecturerId: 7);

        Assert.False(result.Success);
        Assert.Empty(notifier.Changes);
    }

    [Fact]
    public async Task Import_notifies_once_for_successful_rows_and_not_for_empty_success()
    {
        var notifier = new RecordingNotifier();
        var service = CreateService(notifier);

        var result = await service.ImportAsync(17, [ValidImportRow(2), ValidImportRow(3)], 7, 7);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Imported);
        var change = Assert.Single(notifier.Changes);
        Assert.Equal(17, change.CourseId);

        notifier.Changes.Clear();
        var noRowsImported = await service.ImportAsync(17, [InvalidImportRow(4)], 7, 7);

        Assert.True(noRowsImported.Success);
        Assert.Equal(0, noRowsImported.Data!.Imported);
        Assert.Empty(notifier.Changes);
    }

    private static QuestionService CreateService(
        RecordingNotifier notifier,
        Exception? saveException = null,
        Exception? archiveException = null)
    {
        var question = new QuestionDetail { QuestionId = 9, CourseId = 17 };
        var questionRepository = TestProxy<IQuestionRepository>.Create((method, _) => method.Name switch
        {
            nameof(IQuestionRepository.GetDetailAsync) => Task.FromResult<QuestionDetail?>(question),
            nameof(IQuestionRepository.SaveAsync) => saveException is null
                ? Task.FromResult(new QuestionDetail { QuestionId = 10, CourseId = 17 })
                : throw saveException,
            nameof(IQuestionRepository.ArchiveAsync) => archiveException is null
                ? Task.CompletedTask
                : throw archiveException,
            nameof(IQuestionRepository.IsAssignedToExamAsync) => Task.FromResult(false),
            nameof(IQuestionRepository.ListTextMatchesAsync) => Task.FromResult<IReadOnlyList<QuestionTextMatch>>([]),
            _ => TestProxy<IQuestionRepository>.Default(method.ReturnType)
        });

        var catalog = TestProxy<ICatalogRepository>.Create((method, _) => method.Name switch
        {
            nameof(ICatalogRepository.CourseExistsAsync) => Task.FromResult(true),
            nameof(ICatalogRepository.ListActiveCoursesAsync) => Task.FromResult<IReadOnlyList<Course>>(
                [new Course { CourseId = 17, LecturerId = 7, IsActive = true }]),
            _ => TestProxy<ICatalogRepository>.Default(method.ReturnType)
        });

        var materials = TestProxy<ICourseMaterialRepository>.Create((method, _) => method.Name == nameof(ICourseMaterialRepository.GetAsync)
            ? Task.FromResult<CourseMaterialItem?>(null)
            : TestProxy<ICourseMaterialRepository>.Default(method.ReturnType));

        return new QuestionService(questionRepository, catalog, materials, notifier);
    }

    private static QuestionSaveRequest ValidSaveRequest(int courseId) => new()
    {
        CourseId = courseId,
        QuestionText = "Một câu hỏi hợp lệ",
        Options =
        [
            new BusinessQuestionOptionInput { Text = "Đúng", IsCorrect = true },
            new BusinessQuestionOptionInput { Text = "Sai", IsCorrect = false }
        ]
    };

    private static QuestionImportRow ValidImportRow(int line) => new()
    {
        SourceLine = line,
        QuestionText = $"Câu hỏi dòng {line}",
        Options =
        [
            new BusinessQuestionOptionInput { Text = "A", IsCorrect = true },
            new BusinessQuestionOptionInput { Text = "B", IsCorrect = false }
        ]
    };

    private static QuestionImportRow InvalidImportRow(int line) => new()
    {
        SourceLine = line,
        QuestionText = "Câu hỏi lỗi",
        Options = [new BusinessQuestionOptionInput { Text = "Chỉ một đáp án", IsCorrect = true }]
    };

    private sealed class RecordingNotifier : IExamRealtimeNotifier
    {
        public List<ExamChange> Changes { get; } = [];

        public Task ChangedAsync(ExamChange change, CancellationToken ct = default)
        {
            Changes.Add(change);
            return Task.CompletedTask;
        }
    }

    private class TestProxy<T> : DispatchProxy where T : class
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod ?? throw new InvalidOperationException(), args);

        public static T Create(Func<MethodInfo, object?[]?, object?> handler)
        {
            var proxy = DispatchProxy.Create<T, TestProxy<T>>();
            ((TestProxy<T>)(object)proxy).Handler = handler;
            return proxy;
        }

        public static object? Default(Type returnType)
        {
            if (returnType == typeof(Task)) return Task.CompletedTask;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var valueType = returnType.GetGenericArguments()[0];
                var value = valueType.IsValueType ? Activator.CreateInstance(valueType) : null;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(valueType)
                    .Invoke(null, [value]);
            }

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }
}
