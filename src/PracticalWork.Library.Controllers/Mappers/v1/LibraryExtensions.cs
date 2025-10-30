using PracticalWork.Library.Contracts.v1.Library.Requests;
using PracticalWork.Library.Contracts.v1.Library.Response;
using PracticalWork.Library.Dto.Input;
using PracticalWork.Library.Dto.Output;

namespace PracticalWork.Library.Controllers.Mappers.v1;

public static class LibraryExtensions
{
    public static GetLibraryBooksDto ToGetLibraryBooksDto(this GetLibraryBooksRequest request) =>
        new(
            request.Category, 
            request.Author, 
            request.AvailableOnly, 
            request.Page, 
            request.PageSize
            );

    public static LibraryBookResponse ToLibraryBookResponse(this LibraryBookDto dto) =>
        new(
            dto.Title, 
            dto.Authors, 
            dto.Description, 
            dto.Year,
            dto.ReaderId,
            dto.BorrowDate,
            dto.DueDate
            );

    public static BookDetailsResponse ToBookDetailsResponse(this BookDetailsDto dto) =>
        new(
            dto.Id,
            dto.Title,
            dto.Category,
            dto.Authors,
            dto.Description,
            dto.Year,
            dto.CoverImagePath,
            dto.Status,
            dto.IsArchived
            );
}