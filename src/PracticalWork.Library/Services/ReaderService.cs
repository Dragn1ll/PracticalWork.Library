using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Services;

/// <inheritdoc cref="IReaderService"/>
public sealed class ReaderService : IReaderService
{
    private readonly IReaderRepository _readerRepository;

    public ReaderService(IReaderRepository readerRepository)
    {
        _readerRepository = readerRepository;
    }

    /// <inheritdoc cref="IReaderService.CreateReader"/>
    public async Task<Guid> CreateReader(Reader reader)
    {
        reader.IsActive = true;
        try
        {
            return await _readerRepository.CreateReader(reader);
        }
        catch (Exception ex)
        {
            throw new ReaderServiceException("Ошибка создания карточки читателя!", ex);
        }
    }

    /// <inheritdoc cref="IReaderService.ExtendValidity"/>
    public async Task ExtendValidity(Guid readerId, DateOnly newExpiryDate)
    {
        try
        {
            if (newExpiryDate < DateOnly.FromDateTime(DateTime.Now))
            {
                throw new ArgumentException("Дата продления не может быть раньше сегодняшней!");
            }
            
            var reader = await _readerRepository.GetReaderById(readerId);
            
            reader.UpdateExpiryDate(newExpiryDate);
            
            await _readerRepository.UpdateReader(readerId, reader);
        }
        catch (Exception ex)
        {
            throw new ReaderServiceException("Ошибка продления карточки читателя!", ex);
        }
    }

    /// <inheritdoc cref="IReaderService.CloseReader"/>
    public async Task CloseReader(Guid readerId)
    {
        try
        {
            var reader = await _readerRepository.GetReaderById(readerId);
            
            // todo пытаемся достать список книг из кэша в Redis
            var books = await _readerRepository.GetBorrowedBooks(readerId);

            if (books.Any())
            {
                throw new ReaderServiceException("У пользователя есть взятые книги!", books);
            }
            
            reader.DeActiveReader();
            
            await _readerRepository.UpdateReader(readerId, reader);
        }
        catch (Exception ex)
        {
            throw new ReaderServiceException("Ошибка закрытия карточки читателя!", ex);
        }
    }

    /// <inheritdoc cref="IReaderService.GetBorrowedBooks"/>
    public async Task<IList<BorrowedBookDto>> GetBorrowedBooks(Guid readerId)
    {
        try
        {
            // todo вытаскивание списка взятых книг из кэша Redis
            return await _readerRepository.GetBorrowedBooks(readerId);
        }
        catch (Exception ex)
        {
            throw new ReaderServiceException("Ошибка получения взятых книг!", ex);
        }
    }
}