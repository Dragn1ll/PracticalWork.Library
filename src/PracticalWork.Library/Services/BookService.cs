using Microsoft.AspNetCore.Http;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Entities;
using PracticalWork.Library.Enums;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Services;

/// <inheritdoc cref="IBookService"/>
public sealed class BookService : IBookService
{
    private readonly IBookRepository _bookRepository;

    public BookService(IBookRepository bookRepository)
    {
        _bookRepository = bookRepository;
    }

    /// <inheritdoc cref="IBookService.CreateBook"/>
    public async Task<Guid> CreateBook(Book book)
    {
        book.Status = BookStatus.Available;
        try
        {
            return await _bookRepository.CreateBook(book);
        }
        catch (Exception ex)
        {
            throw new BookServiceException("Ошибка создание книги!", ex);
        }
    }

    /// <inheritdoc cref="IBookService.UpdateBook"/>
    public async Task UpdateBook(Guid bookId, Book book)
    {
        try
        {
            var bookEntity = await _bookRepository.GetBook(bookId);

            if (bookEntity.IsArchived)
            {
                throw new ArgumentException("Книга находится в архиве.");
            }
            
            await _bookRepository.UpdateBook(bookId, b =>
            {
                b.Title = book.Title;
                b.Authors = book.Authors;
                b.Year = book.Year;
            });
        }
        catch (Exception ex)
        {
            throw new BookServiceException("Ошибка обновления данных книги!", ex);
        }
    }

    /// <inheritdoc cref="IBookService.ArchiveBook"/>
    public async Task ArchiveBook(Guid bookId)
    {
        try
        {
            var book = await _bookRepository.GetBook(bookId);

            if (book.IsArchived)
            {
                throw new ArgumentException("Книга уже переведена в архив.");
            }
            
            book.Archive();
            
            await _bookRepository.UpdateBook(bookId, b => b.Status = BookStatus.Archived);
        }
        catch (Exception ex)
        {
            throw new BookServiceException("Ошибка перевода книги в архив!", ex);
        }
    }

    /// <inheritdoc cref="IBookService.GetBooks"/>
    public async Task<ICollection<Book>> GetBooks(BookStatus status, BookCategory category, string author, int page,
        int pageSize)
    {
        try
        {
            // todo добавить кэширование через Redis
            return await _bookRepository.GetBooks(page, pageSize, b => 
                b.Status == status && b.Authors.Contains(author) && b.GetType() == ConvertToBookType(category));
        }
        catch (Exception ex)
        {
            throw new BookServiceException("Ошибка получения списка книг!", ex);
        }
    }

    /// <inheritdoc cref="IBookService.CreateBookDetails"/>
    public async Task CreateBookDetails(Guid bookId, IFormFile coverImage, string description)
    {
        try
        {
            // todo добавление в MinIO
            await _bookRepository.UpdateBook(bookId, b =>
            {
                b.CoverImagePath = String.Empty; // временная заглушка
                b.Description = description;
            });
        }
        catch (Exception ex)
        {
            throw new BookServiceException("Ошибка добавления деталей книги!", ex);
        }
    }

    private Type ConvertToBookType(BookCategory category)
    {
        return category switch
        {
            BookCategory.ScientificBook => typeof(ScientificBookEntity),
            BookCategory.EducationalBook => typeof(EducationalBookEntity),
            BookCategory.FictionBook => typeof(FictionBookEntity),
            _ => throw new ArgumentException($"Неподдерживаемая категория книги: {category}", nameof(category))
        };
    }
}