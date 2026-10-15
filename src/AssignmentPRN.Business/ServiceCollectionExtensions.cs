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
        services.AddScoped<IQuestionReviewService, QuestionReviewService>();
        services.AddScoped<ICourseMaterialService, CourseMaterialService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IExamResultService, ExamResultService>();
        services.AddScoped<IClassManagementService, ClassManagementService>();
        services.AddScoped<IRubricService, RubricService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDocumentService, DocumentService>();

        // Document processing
        services.AddSingleton<DocumentProcessingQueue>();
        services.AddHostedService<DocumentProcessingBackgroundService>();

        // HTTP client for Ollama
        services.AddHttpClient();

        // Options
        services.AddOptions<DocumentStorageOptions>()
            .BindConfiguration(DocumentStorageOptions.SectionName)
            .Validate(o => o.ChunkSize > 0 && o.ChunkOverlap >= 0 && o.ChunkOverlap < o.ChunkSize, "Chunk overlap must be smaller than chunk size.")
            .Validate(o => o.MaxFileSizeMB > 0 && o.MaxFileSizeMB <= 1024, "Document size limit must be between 1 and 1024 MB.")
            .ValidateOnStart();
        services.AddOptions<OllamaOptions>()
            .BindConfiguration(OllamaOptions.SectionName)
            .ValidateOnStart();
        services.AddOptions<RagOptions>()
            .BindConfiguration(RagOptions.SectionName)
            .Validate(o => o.TopK is >= 1 and <= 20 && o.SimilarityThreshold is >= -1 and <= 1, "RAG search settings are invalid.")
            .ValidateOnStart();

        return services;
    }
}
