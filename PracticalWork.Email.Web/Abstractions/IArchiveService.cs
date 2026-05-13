using PracticalWork.Email.Web.Models;

namespace PracticalWork.Email.Web.Abstractions;

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