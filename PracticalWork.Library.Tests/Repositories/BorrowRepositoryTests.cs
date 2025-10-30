using Microsoft.EntityFrameworkCore;
using PracticalWork.Library.Data.PostgreSql;
using PracticalWork.Library.Data.PostgreSql.Entities;
using PracticalWork.Library.Data.PostgreSql.Repositories;
using PracticalWork.Library.Enums;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Tests.Repositories;

public class BorrowRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly BorrowRepository _repository;
    private readonly string _dbName = Guid.NewGuid().ToString();

    private readonly Guid _activeBookId = Guid.NewGuid();
    private readonly Guid _returnedBookId = Guid.NewGuid();
    private readonly Guid _activeReaderId = Guid.NewGuid();

    public BorrowRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;

        _context = new AppDbContext(options);
        _repository = new BorrowRepository(_context);
        
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
}