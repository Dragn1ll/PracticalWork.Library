using PracticalWork.Library.Dto;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Abstractions.Storage;

public interface IReaderRepository
{
    Task<Guid> CreateReader(Reader reader);
    
    Task<Reader> GetReaderById(Guid readerId);
    
    Task<IList<BorrowedBookDto>> GetBorrowedBooks(Guid readerId);
    
    Task UpdateReader(Guid readerId,Reader reader);
}