using Microsoft.EntityFrameworkCore;
using PracticalWork.Email.Web.Abstractions;
using PracticalWork.Email.Web.Models;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Data.PostgreSql;
using PracticalWork.Library.MessageBroker.Events.Book;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Email.Web.Services;

/// <inheritdoc cref="IArchiveService"/>
public class ArchiveService : IArchiveService
{
    private readonly AppDbContext _dbContext;
    private readonly IRabbitMqProducer _rabbitMqProducer;
    private readonly ILogger<ArchiveService> _logger;

    public ArchiveService(
        AppDbContext dbContext,
        IRabbitMqProducer rabbitMqProducer,
        ILogger<ArchiveService> logger)
    {
        _dbContext = dbContext;
        _rabbitMqProducer = rabbitMqProducer;
        _logger = logger;
    }

    /// <inheritdoc cref="IArchiveService.ArchiveOldBooks"/>
    public async Task<ArchiveResult> ArchiveOldBooks(int yearsWithoutBorrow, int maxBooksPerRun)
    {
        var result = new ArchiveResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-yearsWithoutBorrow));

        // Находим книги-кандидаты на архивацию:
        // Статус Available, не выданы в данный момент, не выдавались более N лет или никогда не выдавались
        var candidateBooks = await _dbContext.Books
            .AsNoTracking()
            .Where(b => b.Status == BookStatus.Available)
            .Where(b => !_dbContext.BookBorrows
                .Any(bb => bb.BookId == b.Id && bb.Status == BookIssueStatus.Issued))
            .Where(b => !_dbContext.BookBorrows
                .Any(bb => bb.BookId == b.Id && bb.BorrowDate >= cutoffDate))
            .Take(maxBooksPerRun)
            .ToListAsync();

        result.TotalProcessed = candidateBooks.Count;

        foreach (var book in candidateBooks)
        {
            try
            {
                // Повторная проверка — не выдана ли книга сейчас
                var isCurrentlyBorrowed = await _dbContext.BookBorrows
                    .AnyAsync(bb => bb.BookId == book.Id && bb.Status == BookIssueStatus.Issued);

                if (isCurrentlyBorrowed)
                {
                    result.SkippedCount++;
                    result.SkippedBooks.Add(new ArchiveSkippedBook
                    {
                        BookId = book.Id,
                        Title = book.Title,
                        Reason = "Книга в данный момент выдана читателю"
                    });
                    _logger.LogWarning("Книга '{Title}' (ID: {Id}) пропущена: выдана читателю", book.Title, book.Id);
                    continue;
                }

                // Перевод в статус Archived
                var entity = await _dbContext.Books.FindAsync(book.Id);
                if (entity == null) continue;

                entity.Status = BookStatus.Archived;
                await _dbContext.SaveChangesAsync();

                // Публикация события в RabbitMQ
                var archivedEvent = new BookArchivedEvent(
                    book.Id,
                    book.Title,
                    $"Автоматическая архивация: книга не выдавалась более {yearsWithoutBorrow} лет",
                    DateTime.UtcNow);

                await _rabbitMqProducer.PublishEventAsync(archivedEvent);

                result.ArchivedCount++;
                _logger.LogInformation("Книга '{Title}' (ID: {Id}) заархивирована", book.Title, book.Id);
            }
            catch (Exception ex)
            {
                result.ErrorCount++;
                result.ErrorBooks.Add(new ArchiveErrorBook
                {
                    BookId = book.Id,
                    Title = book.Title,
                    Error = ex.Message
                });
                _logger.LogError(ex, "Ошибка архивации книги '{Title}' (ID: {Id})", book.Title, book.Id);
            }
        }

        stopwatch.Stop();
        result.ExecutionTime = stopwatch.Elapsed;

        _logger.LogInformation(
            "Архивация завершена: обработано {Total}, заархивировано {Archived}, пропущено {Skipped}, ошибок {Errors}, время {Time}",
            result.TotalProcessed, result.ArchivedCount, result.SkippedCount, result.ErrorCount, result.ExecutionTime);

        return result;
    }
}