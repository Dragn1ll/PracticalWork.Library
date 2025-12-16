using Microsoft.EntityFrameworkCore;
using PracticalWork.Library.Data.PostgreSql;
using PracticalWork.Library.Data.PostgreSql.Entities;
using PracticalWork.Library.Data.PostgreSql.Repositories;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Library.Tests.Repositories;

public class ReaderRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ReaderRepository _repository;
    private readonly string _dbName = Guid.NewGuid().ToString();

    private readonly Guid _existingReaderId = Guid.NewGuid();
    private readonly Guid _readerWithBooksId = Guid.NewGuid();
    private readonly string _uniquePhoneNumber = "79001234567";
    private readonly string _readerWithBooksPhone = "79009999999";
    private readonly Guid _bookId1 = Guid.NewGuid();
    private readonly Guid _bookId2 = Guid.NewGuid();

    public ReaderRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;

        _context = new AppDbContext(options);
        _repository = new ReaderRepository(_context);
        
        SeedData();
    }

    private void SeedData()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        
        _context.Readers.Add(new ReaderEntity
        {
            Id = _existingReaderId,
            FullName = "Иванов Иван",
            PhoneNumber = _uniquePhoneNumber,
            ExpiryDate = today.AddYears(1),
            IsActive = true
        });
        
        _context.Readers.Add(new ReaderEntity
        {
            Id = _readerWithBooksId,
            FullName = "Петров Петр",
            PhoneNumber = _readerWithBooksPhone,
            ExpiryDate = today.AddYears(1),
            IsActive = true,
            BorrowedRecords = new List<BookBorrowEntity>
            {
                new BookBorrowEntity
                {
                    BookId = _bookId1,
                    ReaderId = _readerWithBooksId,
                    BorrowDate = today.AddDays(-10),
                    DueDate = today.AddDays(20),
                    Status = BookIssueStatus.Issued
                },
                new BookBorrowEntity
                {
                    BookId = _bookId2,
                    ReaderId = _readerWithBooksId,
                    BorrowDate = today.AddDays(-50),
                    DueDate = today.AddDays(-30),
                    ReturnDate = today.AddDays(-20),
                    Status = BookIssueStatus.Returned
                }
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
    public async Task CreateReader_ShouldCreateEntityAndReturnId_WhenPhoneNumberIsUnique()
    {
        // Arrange
        var newReader = new Reader
        {
            FullName = "Сидоров С.",
            PhoneNumber = "79001112233",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(6),
            IsActive = true
        };

        // Act
        var resultId = await _repository.CreateReader(newReader);

        // Assert
        Assert.NotEqual(Guid.Empty, resultId);
        var savedEntity = await _context.Readers.FindAsync(resultId);
        
        Assert.NotNull(savedEntity);
        Assert.Equal(newReader.FullName, savedEntity.FullName);
        Assert.Equal(newReader.PhoneNumber, savedEntity.PhoneNumber);
    }
    
    [Fact]
    public async Task CreateReader_ShouldThrowArgumentException_WhenPhoneNumberAlreadyExists()
    {
        // Arrange
        var duplicateReader = new Reader
        {
            FullName = "Дубликат",
            PhoneNumber = _uniquePhoneNumber,
            IsActive = true
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _repository.CreateReader(duplicateReader));
        Assert.Contains($"Читатель с таким номером телефона уже существует: {_uniquePhoneNumber}", ex.Message);
    }
    
    [Fact]
    public async Task GetReaderById_ShouldReturnReaderModel_WhenFound()
    {
        // Act
        var reader = await _repository.GetReaderById(_existingReaderId);

        // Assert
        Assert.NotNull(reader);
        Assert.Equal("Иванов Иван", reader.FullName);
        Assert.Equal(_uniquePhoneNumber, reader.PhoneNumber);
        Assert.True(reader.IsActive);
    }

    [Fact]
    public async Task GetReaderById_ShouldThrowArgumentException_WhenNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetReaderById(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetBorrowedBooks_ShouldReturnOnlyIssuedBooks()
    {
        // Act
        var books = await _repository.GetBorrowedBooks(_readerWithBooksId);

        // Assert
        Assert.Single(books);
        Assert.Equal(_bookId1, books.First().BookId);
        Assert.DoesNotContain(books, b => b.BookId == _bookId2);
    }
    
    [Fact]
    public async Task GetBorrowedBooks_ShouldReturnEmptyList_WhenNoIssuedBooks()
    {
        // Arrange
        var readerId = Guid.NewGuid();
    
        _context.Readers.Add(new ReaderEntity 
        { 
            Id = readerId, 
            FullName = "Тестовый Читатель",
            PhoneNumber = "00000000000",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today).AddYears(1),
            BorrowedRecords = new List<BookBorrowEntity>() 
        });
    
        await _context.SaveChangesAsync(); 

        // Act
        var books = await _repository.GetBorrowedBooks(readerId);

        // Assert
        Assert.Empty(books);
    }

    [Fact]
    public async Task GetBorrowedBooks_ShouldThrowArgumentException_WhenReaderNotFound()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetBorrowedBooks(Guid.NewGuid()));
        Assert.Contains("Отсутствует читатель", ex.Message);
    }
    
    [Fact]
    public async Task UpdateReader_ShouldUpdateAllFieldsAndSave()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var updatedReader = new Reader
        {
            FullName = "Иванов Новый",
            PhoneNumber = "79008887766",
            ExpiryDate = today.AddYears(5),
            IsActive = false
        };

        // Act
        await _repository.UpdateReader(_existingReaderId, updatedReader);

        // Assert
        _context.Entry((await _context.Readers.FindAsync(_existingReaderId))!).State = EntityState.Detached; 
        
        var entity = await _context.Readers.FindAsync(_existingReaderId);
        
        Assert.NotNull(entity);
        Assert.Equal("Иванов Новый", entity.FullName);
        Assert.Equal("79008887766", entity.PhoneNumber);
        Assert.False(entity.IsActive);
        Assert.Equal(today.AddYears(5), entity.ExpiryDate);
    }
    
    [Fact]
    public async Task UpdateReader_ShouldThrowArgumentException_WhenNotFound()
    {
        // Arrange
        var reader = new Reader();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.UpdateReader(Guid.NewGuid(), reader));
    }
}