using Microsoft.EntityFrameworkCore;
using PracticalWork.Library.Data.PostgreSql;
using PracticalWork.Library.Data.PostgreSql.Entities;
using PracticalWork.Library.Data.PostgreSql.Repositories;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Library.Tests.Repositories;

public class BookRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly BookRepository _repository;
    private readonly string _dbName = Guid.NewGuid().ToString();

    private readonly Guid _fictionBookId = Guid.NewGuid();
    private readonly Guid _scientificBookId = Guid.NewGuid();
    private readonly Guid _availableBookId = Guid.NewGuid();
    private readonly Guid _borrowedBookId = Guid.NewGuid();
    private readonly Guid _archivedBookId = Guid.NewGuid();
    private readonly Guid _readerId = Guid.NewGuid();
    private readonly string _testAuthor = "Test Author";

    public BookRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;

        _context = new AppDbContext(options);
        _repository = new BookRepository(_context);
        
        SeedData();
    }

    private void SeedData()
    {
        _context.Books.AddRange(new List<AbstractBookEntity>
        {
            new FictionBookEntity 
            {
                Id = _fictionBookId, 
                Title = "Fiction Title", 
                Authors = new List<string> { _testAuthor, "Another Author" },
                Status = BookStatus.Borrow,
                Description = "Desc 1", Year = 2000, CoverImagePath = "path/1"
            },
            new ScientificBookEntity 
            {
                Id = _scientificBookId, 
                Title = "Scientific Title", 
                Authors = new List<string> { _testAuthor },
                Status = BookStatus.Available,
                Description = "Desc 2", Year = 2010, CoverImagePath = "path/2"
            },
            new EducationalBookEntity
            {
                Id = _availableBookId,
                Title = "Available Book Title",
                Authors = new List<string> { _testAuthor },
                Status = BookStatus.Available,
                Description = "Desc 3", Year = 2020, CoverImagePath = "path/3"
            },
            new EducationalBookEntity
            {
                Id = _borrowedBookId,
                Title = "Borrowed Book Title",
                Authors = new List<string> { _testAuthor },
                Status = BookStatus.Borrow,
                Description = "Desc 4", Year = 2020, CoverImagePath = "path/4",
                IssuanceRecords = new List<BookBorrowEntity>
                {
                    new BookBorrowEntity
                    {
                        BookId = _borrowedBookId,
                        ReaderId = _readerId,
                        BorrowDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
                        DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(20)),
                        Status = BookIssueStatus.Issued
                    },
                    new BookBorrowEntity
                    {
                        BookId = _borrowedBookId,
                        ReaderId = Guid.NewGuid(),
                        Status = BookIssueStatus.Returned 
                    }
                }
            },
            new ScientificBookEntity
            {
                Id = _archivedBookId,
                Title = "Archived Book Title",
                Authors = new List<string> { _testAuthor },
                Status = BookStatus.Archived,
                Description = "Desc 5", Year = 2020, CoverImagePath = "path/5"
            }
        });
        
        _context.SaveChanges();
    }
    
    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
    
    [Theory]
    [InlineData(BookCategory.ScientificBook, typeof(ScientificBookEntity))]
    [InlineData(BookCategory.EducationalBook, typeof(EducationalBookEntity))]
    [InlineData(BookCategory.FictionBook, typeof(FictionBookEntity))]
    public async Task CreateBook_ShouldCreateCorrectEntityTypeAndReturnId(BookCategory category, Type expectedEntityType)
    {
        // Arrange
        var newBook = new Book
        {
            Title = "New Title",
            Authors = new List<string> { "A1" },
            Category = category,
            Status = BookStatus.Available,
            Description = "Test",
            Year = 2025
        };

        // Act
        var resultId = await _repository.CreateBook(newBook);

        // Assert
        Assert.NotEqual(Guid.Empty, resultId);
        
        var savedEntity = await _context.Books.FindAsync(resultId);
        Assert.NotNull(savedEntity);
        Assert.IsType(expectedEntityType, savedEntity); 
        Assert.Equal(newBook.Title, savedEntity.Title);
        Assert.Equal(BookStatus.Available, savedEntity.Status);
    }
    
    [Fact]
    public async Task CreateBook_ShouldThrowArgumentException_WhenCategoryIsUnsupported()
    {
        // Arrange
        var newBook = new Book
        {
            Category = (BookCategory)999,
            Authors = new List<string> { "A1" },
            Status = BookStatus.Available,
            Title = "Bad Book", Description = "Test", Year = 2025
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.CreateBook(newBook));
    }
    
    [Fact]
    public async Task GetBookById_ShouldReturnBook_WhenFound()
    {
        // Act
        var book = await _repository.GetBookById(_scientificBookId);

        // Assert
        Assert.NotNull(book);
        Assert.Equal("Scientific Title", book.Title);
        Assert.Equal(BookCategory.ScientificBook, book.Category); 
        Assert.Equal(BookStatus.Available, book.Status);
    }

    [Fact]
    public async Task GetBookById_ShouldThrowArgumentException_WhenNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetBookById(Guid.NewGuid()));
    }
    
    [Fact]
    public async Task UpdateBook_ShouldUpdateAllFieldsAndSave()
    {
        // Arrange
        var updatedBook = new Book
        {
            Title = "Updated Title",
            Authors = new List<string> { "New Author" },
            Description = "New Desc",
            Year = 2024,
            Status = BookStatus.Archived,
            CoverImagePath = "new/path/file.jpg",
            Category = BookCategory.ScientificBook // Категория не должна меняться в базе, но должна быть в модели
        };

        // Act
        await _repository.UpdateBook(_scientificBookId, updatedBook);

        // Assert
        var entity = await _context.Books.FindAsync(_scientificBookId);
        Assert.NotNull(entity);
        Assert.Equal("Updated Title", entity.Title);
        Assert.Equal(new List<string> { "New Author" }, entity.Authors);
        Assert.Equal(BookStatus.Archived, entity.Status);
        Assert.Equal("new/path/file.jpg", entity.CoverImagePath);
    }
    
    [Fact]
    public async Task UpdateBook_ShouldThrowArgumentException_WhenNotFound()
    {
        // Arrange
        var book = new Book();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.UpdateBook(Guid.NewGuid(), book));
    }
    
    [Fact]
    public async Task GetBooks_ShouldFilterByAuthorStatusAndCategory()
    {
        // Act
        var books = await _repository.GetBooks(BookStatus.Available, BookCategory.ScientificBook, _testAuthor, 1, 10);

        // Assert
        Assert.Single(books);
        Assert.Equal(_scientificBookId, books.First().Id);
    }
    
    [Fact]
    public async Task GetBooks_ShouldApplyPagination()
    {
        // Arrange
        _context.Books.AddRange(new List<AbstractBookEntity>
        {
            new ScientificBookEntity { Id = Guid.NewGuid(), Title = "P1", Authors = new List<string> { _testAuthor }, Status = BookStatus.Available, Year = 2000 },
            new ScientificBookEntity { Id = Guid.NewGuid(), Title = "P2", Authors = new List<string> { _testAuthor }, Status = BookStatus.Available, Year = 2000 },
            new ScientificBookEntity { Id = Guid.NewGuid(), Title = "P3", Authors = new List<string> { _testAuthor }, Status = BookStatus.Available, Year = 2000 },
        });
        await _context.SaveChangesAsync();
        
        // Act
        var books = await _repository.GetBooks(BookStatus.Available, BookCategory.ScientificBook, _testAuthor, 2, 2);

        // Assert
        Assert.Equal(2, books.Count);
    }
}