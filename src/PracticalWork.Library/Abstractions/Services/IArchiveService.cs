using PracticalWork.Library.Models;

namespace PracticalWork.Library.Abstractions.Services;

/// <summary>
/// Сервис для автоматической архивации старых книг
/// </summary>
public interface IArchiveService
{
    /// <summary>
    /// Архивирование старых книг
    /// </summary>
    Task<ArchiveResult> ArchiveOldBooks(int yearsWithoutBorrow, int maxBooksPerRun);
}