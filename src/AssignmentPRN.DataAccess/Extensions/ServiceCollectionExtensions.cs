using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Repositories;
using AssignmentPRN.DataAccess.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssignmentPRN.DataAccess.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        string connectionString,
        string storageRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A MySQL connection string is required.", nameof(connectionString));
        }

        services.AddDbContext<AivesDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 8))));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IAcademicClassRepository, AcademicClassRepository>();
        services.AddScoped<IExamSessionRepository, ExamSessionRepository>();
        services.AddScoped<IExamVivaSettingsRepository, ExamVivaSettingsRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IVoiceRoomRepository, VoiceRoomRepository>();
        services.AddScoped<ICourseMaterialRepository, CourseMaterialRepository>();
        services.AddScoped<IExamResultRepository, ExamResultRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddSingleton<IMaterialFileStore>(new LocalMaterialFileStore(storageRootPath));
        services.AddScoped<IDocumentRepository, DocumentRepository>();

        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddHostedService<DatabaseInitializerHostedService>();

        return services;
    }
}
