using Microsoft.EntityFrameworkCore;
using Npgsql;
using PracticalWork.Email.Web.Abstractions;
using PracticalWork.Email.Web.Configuration;
using PracticalWork.Email.Web.Jobs;
using PracticalWork.Email.Web.Services;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Cache.Redis;
using PracticalWork.Library.Data.Minio;
using PracticalWork.Library.Data.PostgreSql;
using PracticalWork.Library.MessageBroker;
using PracticalWork.Library.Services;
using PracticalWork.Reports.Data.PostgreSql;

namespace PracticalWork.Email.Web;

/// <summary>
/// Точка входа для регистрации зависимостей модуля Email
/// </summary>
public static class Entry
{
    /// <summary>
    /// Регистрация всех зависимостей для Email Worker Service
    /// </summary>
    public static IServiceCollection AddEmailWorkerServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.Configure<JobSettings>(configuration.GetSection("JobSettings"));
        services.Configure<ArchiveSettings>(configuration.GetSection("ArchiveSettings"));
        services.Configure<EmailTemplateSettings>(configuration.GetSection("EmailTemplateSettings"));
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));

        services.AddSingleton<IRabbitMqProducer, RabbitMqProducer>();

        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<IEmailTemplateService, EmailTemplateService>();

        services.AddScoped<IReportJobService, ReportJobService>();
        services.AddScoped<IArchiveService, ArchiveService>();

        services.AddScoped<ReturnReminderJob>();
        services.AddScoped<WeeklyReportJob>();
        services.AddScoped<ArchiveOldBooksJob>();

        return services;
    }

    /// <summary>
    /// Настройка баз данных для Email Worker
    /// </summary>
    public static IServiceCollection AddEmailWorkerDatabases(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.AddPostgreSqlStorage(cfg =>
        {
            var npgsqlDataSource = new NpgsqlDataSourceBuilder(
                    configuration["App:DbConnectionString"])
                .EnableDynamicJson()
                .Build();

            cfg.UseNpgsql(npgsqlDataSource);
        });

        services.AddReportPostgreSqlStorage(cfg =>
        {
            var npgsqlDataSource = new NpgsqlDataSourceBuilder(
                    configuration["Report:DbConnectionString"])
                .EnableDynamicJson()
                .Build();

            cfg.UseNpgsql(npgsqlDataSource);
        });

        services.AddCache(configuration);

        services.AddMinioFileStorage(configuration);

        return services;
    }
}