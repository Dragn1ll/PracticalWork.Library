using JetBrains.Annotations;
using PracticalWork.Library.Dto;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Exceptions;

public sealed class ReaderServiceException : AppException
{
    [CanBeNull] private readonly IList<BorrowedBookDto> _borrowedBooks = null;
    
    public ReaderServiceException(string message) : base($"{message}")
    {
    }

    public ReaderServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ReaderServiceException(string message, IList<BorrowedBookDto> borrowedBooks) : base($"{message}")
    {
        _borrowedBooks = borrowedBooks;
    }
}