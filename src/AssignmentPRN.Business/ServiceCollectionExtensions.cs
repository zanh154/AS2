using AssignmentPRN.DataAccess.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AssignmentPRN.Business;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusiness(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDataAccess(connectionString);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExamSessionService, ExamSessionService>();
        services.AddScoped<IClassManagementService, ClassManagementService>();
        services.AddScoped<IDocumentService, DocumentService>();

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
