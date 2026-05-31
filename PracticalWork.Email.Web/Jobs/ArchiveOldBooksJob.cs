using Microsoft.Extensions.Options;
using PracticalWork.Library.Abstractions.Jobs;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Settings;

namespace PracticalWork.Email.Web.Jobs;

/// <summary>
/// Фоновая задача: автоматическая архивация старых книг
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
        
        var archiveResult = await _archiveService.ArchiveOldBooks(
            _archiveSettings.YearsWithoutBorrow,
            _archiveSettings.MaxBooksPerRun);

        _logger.LogInformation(
            "Архивация завершена: " +
            "обработано {Total}, " +
            "заархивировано {Archived}, " +
            "пропущено {Skipped}, " +
            "время выполнения {Time}",
            archiveResult.TotalProcessed, archiveResult.ArchivedCount, 
            archiveResult.SkippedCount, archiveResult.ExecutionTime);
    }
}