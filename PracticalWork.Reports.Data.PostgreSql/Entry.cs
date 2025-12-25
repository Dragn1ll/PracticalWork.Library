using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Reports.Data.PostgreSql.Repositories;

namespace PracticalWork.Reports.Data.PostgreSql;

public static class Entry
{
    private static readonly Action<DbContextOptionsBuilder> DefaultOptionsAction = (_) => { };

    /// <summary>
    /// Добавления зависимостей для работы с БД
    /// </summary>
    public static IServiceCollection AddReportPostgreSqlStorage(this IServiceCollection serviceCollection, Action<DbContextOptionsBuilder> optionsAction)
    {
        serviceCollection.AddDbContext<ReportDbContext>(optionsAction ?? DefaultOptionsAction, optionsLifetime: ServiceLifetime.Singleton);

        serviceCollection.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        serviceCollection.AddScoped<IReportRepository, ReportRepository>();
        
        return serviceCollection;
    }
}