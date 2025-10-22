using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Entities;
using PracticalWork.Library.Enums;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Data.PostgreSql.Repositories;

/// <inheritdoc cref="IBookRepository"/>>
public sealed class BookRepository : IBookRepository
{
    private readonly AppDbContext _appDbContext;

    public BookRepository(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }

    /// <inheritdoc cref="IBookRepository.CreateBook"/>
    public async Task<Guid> CreateBook(Book book)
    {
        AbstractBookEntity entity = book.Category switch
        {
            BookCategory.ScientificBook => new ScientificBookEntity(),
            BookCategory.EducationalBook => new EducationalBookEntity(),
            BookCategory.FictionBook => new FictionBookEntity(),
            _ => throw new ArgumentException($"Неподдерживаемая категория книги: {book.Category}", nameof(book.Category))
        };

        entity.Title = book.Title;
        entity.Description = book.Description;
        entity.Year = book.Year;
        entity.Authors = book.Authors;
        entity.Status = book.Status;

        _appDbContext.Add(entity);
        await _appDbContext.SaveChangesAsync();

        return entity.Id;
    }

    /// <inheritdoc cref="IBookRepository.GetBook"/>
    public async Task<Book> GetBook(Guid bookId)
    {
        var entity = await _appDbContext.Books.FindAsync(bookId);

        if (entity == null)
        {
            throw new ArgumentException($"Отсутствует книга с идентификатором: {bookId}");
        }
        
        return ConvertToBook(entity);
    }

    /// <inheritdoc cref="IBookRepository.UpdateBook"/>
    public async Task UpdateBook(Guid bookId, Action<AbstractBookEntity> updateAction)
    {
        var entity = await _appDbContext.Books.FindAsync(bookId);

        if (entity == null)
        {
            throw new ArgumentException($"Отсутствует книга с идентификатором: {bookId}");
        }
        
        updateAction(entity);
        
        _appDbContext.Update(entity);
        await _appDbContext.SaveChangesAsync();
    }

    /// <inheritdoc cref="IBookRepository.GetBooks"/>
    public async Task<ICollection<Book>> GetBooks(int page, int pageSize, 
        Expression<Func<AbstractBookEntity, bool>> predicate)
    {
        return await _appDbContext.Books
                                    .Where(predicate)
                                    .Skip((page - 1) * pageSize)
                                    .Take(pageSize)
                                    .Select(e => ConvertToBook(e))
                                    .ToListAsync();
    }

    private Book ConvertToBook(AbstractBookEntity entity)
    {
        return new Book
        {
            Title = entity.Title,
            Authors = entity.Authors,
            Description = entity.Description,
            Year = entity.Year,
            Status = entity.Status,
            CoverImagePath = entity.CoverImagePath,
            Category = entity switch
            {
                ScientificBookEntity => BookCategory.ScientificBook,
                EducationalBookEntity => BookCategory.EducationalBook,
                FictionBookEntity => BookCategory.FictionBook,
                _ => throw new ArgumentException($"Неподдерживаемый тип книги: {entity.GetType()}")
            },
            IsArchived = entity.Status == BookStatus.Archived
        };
    }
}