using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.MessageBroker.Events.Book;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Library.Services;

/// <inheritdoc cref="IArchiveService"/>
public class ArchiveService : IArchiveService
{
    private readonly IBookRepository _bookRepository;
    private readonly IRabbitMqProducer _rabbitMqProducer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ArchiveService> _logger;

    public ArchiveService(
        IBookRepository bookRepository,
        IRabbitMqProducer rabbitMqProducer, 
        TimeProvider timeProvider,
        ILogger<ArchiveService> logger)
    {
        _bookRepository = bookRepository;
        _rabbitMqProducer = rabbitMqProducer;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc cref="IArchiveService.ArchiveOldBooks"/>
    public async Task<ArchiveResult> ArchiveOldBooks(int yearsWithoutBorrow, int maxBooksPerRun)
    {
        var stopWatch = Stopwatch.StartNew();
        
        var cutoffDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime.AddYears(-yearsWithoutBorrow));
        var books = await _bookRepository.GetAvailableOldBooks(cutoffDate, 1, maxBooksPerRun);
        var archiveResult = await ArchiveOldBooksAsync(books, yearsWithoutBorrow);

        stopWatch.Stop();
        archiveResult.ExecutionTime = stopWatch.Elapsed;

        return archiveResult;
    }

    private async Task<ArchiveResult> ArchiveOldBooksAsync(IList<AvailableOldBookDto> books, int yearsWithoutBorrow)
    {
        var archiveResult = new ArchiveResult();
        var skipReasons = new HashSet<string>();

        foreach (var book in books)
        {
            try
            {
                var bookEntity = await _bookRepository.GetBookById(book.Id);
                
                bookEntity.Archive();
                
                await _bookRepository.UpdateBook(book.Id, bookEntity);
                
                await PublishArchivedBookEventAsync(book, yearsWithoutBorrow);
                
                archiveResult.ArchivedCount++;
                _logger.LogInformation("Книга '{Title}' (ID: {Id}) заархивирована", book.Title, book.Id);
            }
            catch (Exception exception)
            {
                archiveResult.SkippedCount++;
                skipReasons.Add(exception.Message);
                _logger.LogError(exception, "Ошибка архивации книги '{Title}' (ID: {Id})", book.Title, book.Id);
            }
            
            archiveResult.TotalProcessed++;
        }

        await _bookRepository.SaveChangesAsync();
        
        archiveResult.SkipReasons = string.Join(";\n", skipReasons);
        return archiveResult;
    }

    private async Task PublishArchivedBookEventAsync(AvailableOldBookDto book, int yearsWithoutBorrow)
    {
        var archivedEvent = new BookArchivedEvent(
            book.Id,
            book.Title,
            $"Автоматическая архивация: книга не выдавалась более {yearsWithoutBorrow} лет",
            _timeProvider.GetUtcNow().UtcDateTime);

        await _rabbitMqProducer.PublishEventAsync(archivedEvent);
    }
}