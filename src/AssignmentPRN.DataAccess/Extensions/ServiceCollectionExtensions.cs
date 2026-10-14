using AssignmentPRN.DataAccess.Repositories;
using AssignmentPRN.DataAccess.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssignmentPRN.DataAccess.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        string connectionString)
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
        services.AddScoped<IExamSessionRepository, ExamSessionRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();

        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddHostedService<DatabaseInitializerHostedService>();

        return services;
    }
}
