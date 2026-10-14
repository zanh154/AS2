using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace AssignmentPRN.Business;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusiness(
        this IServiceCollection services,
        string connectionString,
        string storageRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<AssignmentPRN.DataAccess.Contracts.IExamStatePolicy, AssignmentPRN.Business.BusinessRules.ExamStatePolicy>();
        services.AddDataAccess(connectionString, storageRootPath);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExamSessionService, ExamSessionService>();
        services.AddScoped<IExamVivaSettingsService, ExamVivaSettingsService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IAcademicClassService, AcademicClassService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<ICourseMaterialService, CourseMaterialService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IExamResultService, ExamResultService>();
        services.AddScoped<IClassManagementService, ClassManagementService>();

        return services;
    }
}
