using Microsoft.Extensions.Options;
using PracticalWork.Email.Web.Abstractions;
using PracticalWork.Email.Web.Configuration;

namespace PracticalWork.Email.Web.Jobs;

/// <summary>
/// Фоновая задача: автоматическая архивация старых книг.
/// Первого числа каждого месяца архивирует книги, не выдававшиеся более 3 лет.
/// </summary>
public class ArchiveOldBooksJob : ILibraryJob
{
    public string JobName => "ArchiveOldBooks";
    public string Description => "Автоматическая архивация старых книг";

    private readonly IArchiveService _archiveService;
    private readonly ArchiveSettings _archiveSettings;
    private readonly ILogger<ArchiveOldBooksJob> _logger;

    public ArchiveOldBooksJob(
        IArchiveService archiveService,
        IOptions<ArchiveSettings> archiveSettings,
        ILogger<ArchiveOldBooksJob> logger)
    {
        _archiveService = archiveService;
        _archiveSettings = archiveSettings.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Начало выполнения задачи: {JobName}", JobName);

        try
        {
            var result = await _archiveService.ArchiveOldBooks(
                _archiveSettings.YearsWithoutBorrow,
                _archiveSettings.MaxBooksPerRun);

            _logger.LogInformation(
                "Архивация завершена: обработано {Total}, заархивировано {Archived}, " +
                "пропущено {Skipped}, ошибок {Errors}, время выполнения {Time}",
                result.TotalProcessed, result.ArchivedCount, 
                result.SkippedCount, result.ErrorCount, result.ExecutionTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при выполнении архивации");
            throw;
        }
    }
}