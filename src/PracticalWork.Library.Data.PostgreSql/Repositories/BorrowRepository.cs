using Microsoft.EntityFrameworkCore;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Data.PostgreSql.Entities;
using PracticalWork.Library.Enums;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Data.PostgreSql.Repositories;

/// <inheritdoc cref="IBorrowRepository"/>
public class BorrowRepository : IBorrowRepository
{
    private readonly AppDbContext _appDbContext;

    public BorrowRepository(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }
    
    /// <inheritdoc cref="IBorrowRepository.CreateBorrow"/>
    public async Task<Guid> CreateBorrow(Borrow borrow)
    {
        var entity = new BookBorrowEntity
        {
            BookId = borrow.BookId,
            ReaderId = borrow.ReaderId,
            BorrowDate = borrow.BorrowDate,
            DueDate = borrow.DueDate,
            Status = borrow.Status
        };
        
        _appDbContext.Add(entity);
        await _appDbContext.SaveChangesAsync();
        
        return entity.Id;
    }

    /// <inheritdoc cref="IBorrowRepository.GetBorrowByBookId"/>
    public async Task<Borrow> GetBorrowByBookId(Guid bookId)
    {
        var entity = await _appDbContext.BookBorrows. AsNoTracking()
            .SingleOrDefaultAsync(b => b.BookId == bookId && b.Status == BookIssueStatus.Issued)
            ?? throw new BorrowNotFoundException($"Отсутствует активная запись о выдачи книги с идентификатором: {bookId}");
        
        return new Borrow
        {
            BookId = bookId,
            ReaderId = entity.ReaderId,
            BorrowDate = entity.BorrowDate,
            DueDate = entity.DueDate,
            Status = entity.Status
        };
    }

    /// <inheritdoc cref="IBorrowRepository.UpdateBorrow"/>
    public async Task UpdateBorrow(Borrow borrow)
    {
        var entity = await _appDbContext.BookBorrows 
                         .SingleOrDefaultAsync(b => b.BookId == borrow.BookId && b.ReaderId == borrow.ReaderId 
                                                                              && b.Status == BookIssueStatus.Issued)
            ?? throw new BorrowNotFoundException("Отсутствует активная запись о выдачи книги");
    
        entity.ReturnDate = borrow.ReturnDate;
        entity.Status = borrow.Status;
    
        await _appDbContext.SaveChangesAsync();
    }
}