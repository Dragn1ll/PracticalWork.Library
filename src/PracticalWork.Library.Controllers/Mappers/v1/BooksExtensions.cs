﻿using PracticalWork.Library.Contracts.v1.Books.Request;
using PracticalWork.Library.Contracts.v1.Books.Response;
using PracticalWork.Library.Dto.Input;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Enums;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Controllers.Mappers.v1;

public static class BooksExtensions
{
    public static Book ToBook(this CreateBookRequest request) =>
        new()
        {
            Authors = request.Authors,
            Title = request.Title,
            Description = request.Description,
            Year = request.Year,
            Category = (BookCategory)request.Category
        };

    public static UpdateBookDto ToUpdateBookDto(this UpdateBookRequest request) =>
        new(
            request.Title, 
            request.Authors, 
            request.Year
            );

    public static ArchiveBookResponse ToArchiveBookResponse(this ArchiveBookDto dto) =>
        new(
            dto.Id, 
            dto.Title, 
            dto.ArchivedAt
            );

    public static GetBookListDto ToGetBookListDto(this GetBookListRequest request) =>
        new(
            request.Status,
            request.Category,
            request.Author,
            request.Page,
            request.PageSize
            );

    public static BookListResponse ToBookListResponse(this BookListDto dto) =>
        new(
            dto.Id,
            dto.Title,
            dto.Authors,
            dto.Description,
            dto.Year,
            dto.CoverImagePath
            );
}