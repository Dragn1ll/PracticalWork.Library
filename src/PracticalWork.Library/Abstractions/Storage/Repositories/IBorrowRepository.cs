using PracticalWork.Library.Models;

namespace PracticalWork.Library.Abstractions.Storage.Repositories;

/// <summary>
/// Репозиторий для записей выдачи книг
/// </summary>
public interface IBorrowRepository
{
    /// <summary>
    /// Создать запись о выдаче книги
    /// </summary>
    /// <param name="borrow">Запись о выдаче книги</param>
    /// <returns>Идентификатор выдачи</returns>
    Task<Guid> CreateBorrow(Borrow borrow);
    
    /// <summary>
    /// Получить последнюю запись о выдаче по идентификатору книги 
    /// </summary>
    /// <param name="bookId">Идентификатор книги</param>
    /// <returns>Запись о выдаче</returns>
    Task<Borrow> GetBorrowByBookId(Guid bookId);
    
    /// <summary>
    /// Обновить данные последней выдачи
    /// </summary>
    /// <param name="borrow">Обновлённая запись о выдаче книги</param>
    /// <returns></returns>
    Task UpdateBorrow(Borrow borrow);
}