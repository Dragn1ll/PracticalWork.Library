using PracticalWork.Library.Contracts.v1.Reader.Request;
using PracticalWork.Library.Contracts.v1.Reader.Response;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Controllers.Mappers.v1;

public static class ReadersExtensions
{
    public static Reader ToReader(this CreateReaderRequest request) =>
        new()
        {
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            ExpiryDate = request.ExpiryDate
        };
    
    public static BorrowedBookResponse ToBorrowedBookResponse(this BorrowedBookDto dto) =>
        new(
            dto.BookId, 
            dto.BorrowDate, 
            dto.DueDate
            );
}