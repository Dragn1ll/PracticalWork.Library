using Microsoft.EntityFrameworkCore;
using PracticalWork.Library.Data.PostgreSql;
using PracticalWork.Library.Data.PostgreSql.Entities;
using PracticalWork.Library.Data.PostgreSql.Repositories;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Library.Tests.Repositories;

public class LibraryRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly LibraryRepository _repository;
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly string _testAuthor = "Test Author";
    private readonly Guid _readerId = Guid.NewGuid();
    private readonly Guid _fictionBookId = Guid.NewGuid();

    private readonly Guid _activeBookId = Guid.NewGuid();
    private readonly Guid _returnedBookId = Guid.NewGuid();
    private readonly Guid _activeReaderId = Guid.NewGuid();

    public LibraryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;

        _context = new AppDbContext(options);
        _repository = new LibraryRepository(_context);
        
        SeedData();
    }

    private void SeedData()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        
        _context.BookBorrows.Add(new BookBorrowEntity
        {
            BookId = _activeBookId,
            ReaderId = _activeReaderId,
            BorrowDate = today.AddDays(-5),
            DueDate = today.AddDays(25),
            Status = BookIssueStatus.Issued
        });
        
        _context.BookBorrows.Add(new BookBorrowEntity
        {
            BookId = _returnedBookId,
            ReaderId = Guid.NewGuid(),
            BorrowDate = today.AddDays(-50),
            DueDate = today.AddDays(-20),
            ReturnDate = today.AddDays(-15),
            Status = BookIssueStatus.Returned
        });
        
        _context.BookBorrows.Add(new BookBorrowEntity
        {
            BookId = _activeBookId,
            ReaderId = Guid.NewGuid(),
            Status = BookIssueStatus.Returned
        });
        
        _context.Books.AddRange(new List<AbstractBookEntity>
        {
            new FictionBookEntity 
            {
                Id = _fictionBookId, 
                Title = "Fiction Title", 
                Authors = new List<string> { _testAuthor, "Another Author" },
                Status = BookStatus.Borrow,
                Description = "Desc 1", Year = 2000, CoverImagePath = "path/1"
            }
        });
        
        _context.SaveChanges();
    }
    
    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateBorrow_ShouldAddEntityAndReturnId()
    {
        // Arrange
        var newBorrow = new Borrow
        {
            BookId = Guid.NewGuid(),
            ReaderId = Guid.NewGuid(),
            BorrowDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = DateOnly.FromDateTime(DateTime.Today).AddDays(30),
            Status = BookIssueStatus.Issued
        };

        // Act
        var resultId = await _repository.CreateBorrow(newBorrow);

        // Assert
        Assert.NotEqual(Guid.Empty, resultId);
        var savedEntity = await _context.BookBorrows.FindAsync(resultId);
        
        Assert.NotNull(savedEntity);
        Assert.Equal(newBorrow.BookId, savedEntity.BookId);
        Assert.Equal(newBorrow.Status, savedEntity.Status);
    }

    [Fact]
    public async Task GetBorrowByBookId_ShouldReturnActiveBorrowRecord()
    {
        // Act
        var result = await _repository.GetBorrowByBookId(_activeBookId);

        // Assert
        Assert.Equal(_activeBookId, result.BookId);
        Assert.Equal(_activeReaderId, result.ReaderId);
        Assert.Equal(BookIssueStatus.Issued, result.Status);
    }

    [Fact]
    public async Task GetBorrowByBookId_ShouldThrowArgumentException_WhenNoActiveRecordFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetBorrowByBookId(_returnedBookId));
        
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetBorrowByBookId(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateBorrow_ShouldUpdateStatusAndReturnDate_WhenRecordIsActive()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var updatedBorrow = new Borrow
        {
            BookId = _activeBookId,
            ReaderId = _activeReaderId,
            ReturnDate = today,
            Status = BookIssueStatus.Returned
        };

        // Act
        await _repository.UpdateBorrow(updatedBorrow);

        // Assert
        var updatedEntity = await _context.BookBorrows
            .SingleOrDefaultAsync(b => b.BookId == _activeBookId && b.ReaderId == _activeReaderId 
                                                                 && b.Status == BookIssueStatus.Returned);
        
        Assert.NotNull(updatedEntity);
        Assert.Equal(updatedBorrow.ReturnDate, updatedEntity.ReturnDate);
        Assert.Equal(BookIssueStatus.Returned, updatedEntity.Status);
    }

    [Fact]
    public async Task UpdateBorrow_ShouldThrowArgumentException_WhenRecordIsNotIssued()
    {
        // Arrange
        var returnedBorrow = new Borrow
        {
            BookId = _returnedBookId,
            ReaderId = Guid.NewGuid(),
            Status = BookIssueStatus.Overdue
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.UpdateBorrow(returnedBorrow));
    }

    [Fact]
    public async Task UpdateBorrow_ShouldThrowArgumentException_WhenReaderIdMismatch()
    {
        // Arrange
        var wrongReaderBorrow = new Borrow
        {
            BookId = _activeBookId,
            ReaderId = Guid.NewGuid(),
            Status = BookIssueStatus.Returned
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.UpdateBorrow(wrongReaderBorrow));
    }
    
    [Fact]
    public async Task GetLibraryBooks_ShouldFilterOutArchivedBooks()
    {
        // Act
        var books = await _repository.GetLibraryBooks(BookCategory.ScientificBook, _testAuthor, 
            false, 1, 10);

        // Assert
        Assert.Single(books);
        Assert.Equal("Scientific Title", books.First().Title);
    }
    
    [Fact]
    public async Task GetLibraryBooks_ShouldFilterByAvailableOnly()
    {
        // Act
        var books = await _repository.GetLibraryBooks(BookCategory.EducationalBook, _testAuthor, 
            true, 1, 10);

        // Assert
        Assert.Single(books);
        Assert.Equal("Available Book Title", books.First().Title);
    }

    [Fact]
    public async Task GetLibraryBooks_ShouldReturnCorrectLastIssuanceRecord()
    {
        // Act
        var books = await _repository.GetLibraryBooks(BookCategory.EducationalBook, _testAuthor, 
            false, 1, 10);

        // Assert
        Assert.Equal(2, books.Count); 
        
        var borrowedBook = books.Single(b => b.Title == "Borrowed Book Title");
        
        Assert.Equal(_readerId, borrowedBook.ReaderId);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today.AddDays(-10)), borrowedBook.BorrowDate);
    }
    
    
    
    [Fact]
    public async Task GetBookIdByTitle_ShouldReturnId_WhenFound()
    {
        // Arrange
        var title = "Fiction Title";

        // Act
        var resultId = await _repository.GetBookIdByTitle(title);

        // Assert
        Assert.Equal(_fictionBookId, resultId);
    }
    
    [Fact]
    public async Task GetBookIdByTitle_ShouldThrowArgumentException_WhenNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetBookIdByTitle("Missing Title"));
    }
}