using AssignmentPRN.DataAccess.Extensions;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
