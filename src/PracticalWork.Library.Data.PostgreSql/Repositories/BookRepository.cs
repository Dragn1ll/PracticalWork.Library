using Microsoft.EntityFrameworkCore;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Data.PostgreSql.Entities;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Enums;
using PracticalWork.Library.Exceptions;
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

    /// <inheritdoc cref="IBookRepository.GetBookById"/>
    public async Task<Book> GetBookById(Guid bookId)
    {
        var entity = await _appDbContext.Books.FindAsync(bookId);

        return entity == null
            ? throw new BookNotFoundException($"Отсутствует книга с идентификатором: {bookId}")
            : ConvertToBook(entity);
    }

    /// <inheritdoc cref="IBookRepository.GetBookIdByTitle"/>
    public async Task<Guid> GetBookIdByTitle(string title)
    {
        var entity = await _appDbContext.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Title == title);

        return entity?.Id ?? throw new BookNotFoundException($"Отсутствует книга с названием: {title}");
    }

    /// <inheritdoc cref="IBookRepository.GetBooks"/>
    public async Task<IList<BookListDto>> GetBooks(BookStatus status, BookCategory category, string author, 
        int page, int pageSize)
    {
        var books = GetBookCategoryData(category);
        var entities = await books.AsNoTracking()
            .Where(b => (b.Authors.Contains(author) || string.IsNullOrEmpty(author)) 
                        && b.Status == status)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return entities.Select(e => new BookListDto(e.Id, e.Title, e.Authors, e.Description, 
                e.Year, e.CoverImagePath))
            .ToList();
    }

    /// <inheritdoc cref="IBookRepository.GetLibraryBooks"/>
    public async Task<IList<LibraryBookDto>> GetLibraryBooks(BookCategory category, string author, bool availableOnly, 
        int page, int pageSize)
    {
        var books = GetBookCategoryData(category);
        var entities = await books.AsNoTracking()
            .Include(b => b.IssuanceRecords)
            .Where(b => (b.Authors.Contains(author) || string.IsNullOrEmpty(author)) 
                        && b.Status != BookStatus.Archived
                        && (b.Status == BookStatus.Available || !availableOnly))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return entities.Select(e =>
            {
                var lastIssuance = e.IssuanceRecords.LastOrDefault(ir => 
                    ir.Status == BookIssueStatus.Issued);

                if (lastIssuance == null)
                {
                    return new LibraryBookDto(e.Title, e.Authors, e.Description, e.Year);
                }

                return new LibraryBookDto(e.Title, e.Authors, e.Description, e.Year, lastIssuance.ReaderId,
                    lastIssuance.BorrowDate, lastIssuance.DueDate);
            })
            .ToList();
    }

    /// <inheritdoc cref="IBookRepository.UpdateBook"/>
    public async Task UpdateBook(Guid bookId, Book book)
    {
        var entity = await _appDbContext.Books.FindAsync(bookId) 
                     ?? throw new BookNotFoundException($"Отсутствует книга с идентификатором: {bookId}");

        entity.Title = book.Title;
        entity.Authors = book.Authors;
        entity.Description = book.Description;
        entity.Year = book.Year;
        entity.Status = book.Status;
        entity.CoverImagePath = book.CoverImagePath;
        
        _appDbContext.Update(entity);
        await _appDbContext.SaveChangesAsync();
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
    
    private IQueryable<AbstractBookEntity> GetBookCategoryData(BookCategory category)
    {
        return category switch
        {
            BookCategory.ScientificBook => _appDbContext.ScientificBooks,
            BookCategory.EducationalBook => _appDbContext.EducationalBooks,
            BookCategory.FictionBook => _appDbContext.FictionBooks,
            BookCategory.Default => _appDbContext.Books,
            _ => throw new ArgumentException($"Неподдерживаемый тип книги: { category }")
        };
    }
}