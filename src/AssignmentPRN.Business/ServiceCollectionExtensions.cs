using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AssignmentPRN.Business;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusiness(
        this IServiceCollection services,
        string connectionString,
        string storageRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<AssignmentPRN.DataAccess.Contracts.IExamStatePolicy, AssignmentPRN.Business.Policies.ExamStatePolicy>();
        services.AddDataAccess(connectionString, storageRootPath);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExamSessionService, ExamSessionService>();
        services.AddScoped<IExamVivaSettingsService, ExamVivaSettingsService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IAcademicClassService, AcademicClassService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IVoiceRoomService, VoiceRoomService>();
        services.AddSingleton<AssignmentPRN.DataAccess.Services.IVoiceAudioStore>(
            new AssignmentPRN.DataAccess.Services.LocalVoiceAudioStore(Path.Combine(storageRootPath, "voice-audio")));
        services.AddScoped<IQuestionReviewService, QuestionReviewService>();
        services.AddScoped<ICourseMaterialService, CourseMaterialService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IExamResultService, ExamResultService>();
        services.AddScoped<IClassManagementService, ClassManagementService>();
        services.AddScoped<IRubricService, RubricService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDocumentService, DocumentService>();

        // The AI examiner. Both adapters swallow model failures, so a viva still runs
        // end to end with nothing listening on the Ollama port.
        services.AddSingleton<OllamaJsonClient>();
        services.AddScoped<IFollowUpAnalyzer, OllamaFollowUpAnalyzer>();
        services.AddScoped<IAnswerGrader, OllamaAnswerGrader>();
        services.AddScoped<AssignmentPRN.DataAccess.Repositories.IGradingRepository,
            AssignmentPRN.DataAccess.Repositories.GradingRepository>();
        services.AddScoped<IGradingService, GradingService>();
        services.AddScoped<IResultReviewService, ResultReviewService>();
        services.AddSingleton<GradingQueue>();
        services.AddHostedService<GradingBackgroundService>();

        // Nạp sẵn model lúc khởi động: lần gọi đầu tiên mất hơn 30 giây vì phải nạp
        // model, và không được để sinh viên đầu tiên trong ngày trả giá đó.
        services.AddHostedService<OllamaWarmUpService>();

        // Document processing
        services.AddSingleton<DocumentProcessingQueue>();
        services.AddHostedService<DocumentProcessingBackgroundService>();

        // HTTP client for Ollama
        services.AddHttpClient();

        // Options
        services.AddOptions<DocumentStorageOptions>()
            .BindConfiguration(DocumentStorageOptions.SectionName)
            .ValidateOnStart();
        services.AddOptions<OllamaOptions>()
            .BindConfiguration(OllamaOptions.SectionName)
            .ValidateOnStart();
        services.AddOptions<RagOptions>()
            .BindConfiguration(RagOptions.SectionName)
            .ValidateOnStart();

        return services;
    }
}
