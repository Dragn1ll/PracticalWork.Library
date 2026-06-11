using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.MessageBroker;
using PracticalWork.Library.Services;
using PracticalWork.Library.Settings;

namespace PracticalWork.Library;

public static class Entry
{
    /// <summary>
    /// Регистрация зависимостей уровня бизнес-логики
    /// </summary>
    public static IServiceCollection AddDomain(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<IReaderService, ReaderService>();
        services.AddScoped<IReportService, ReportService>();
        
        services.Configure<JobSettings>(configuration.GetSection("JobSettings"));
        services.Configure<ArchiveSettings>(configuration.GetSection("ArchiveSettings"));
        services.Configure<EmailTemplateSettings>(configuration.GetSection("EmailTemplateSettings"));

        return services;
    }
}