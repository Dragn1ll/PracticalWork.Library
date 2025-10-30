using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Contracts.v1.Reader.Request;
using PracticalWork.Library.Contracts.v1.Reader.Response;
using PracticalWork.Library.Controllers.Mappers.v1;

namespace PracticalWork.Library.Controllers.Api.v1;

[ApiController]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/readers")]
public class ReaderController : Controller
{
    private readonly IReaderService _readerService;

    public ReaderController(IReaderService readerService)
    {
        _readerService = readerService;
    }
    
    /// <summary>Создание новой карточки читателя</summary>
    [HttpPost]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CreateReaderResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CreateReader([FromBody] CreateReaderRequest request)
    {
        var result = await _readerService.CreateReader(request.ToReader());

        return Content(result.ToString());
    }

    /// <summary>Продление карточки читателя</summary>
    [HttpPost("{id:guid}/extend")]
    [Produces("application/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ExtendReader([FromRoute] Guid id, [FromBody] ExtendReaderRequest request)
    {
        await _readerService.ExtendValidity(id, request.NewExpiryDate);
        
        return Ok();
    }

    /// <summary>Закрытие карточки читателя</summary>
    [HttpPost("{id:guid}/close")]
    [Produces("application/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CloseReader([FromRoute] Guid id)
    {
        await _readerService.CloseReader(id);
        
        return Ok();
    }
    
    /// <summary>Получение взятых книг</summary>
    [HttpPost("{id:guid}/books")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IList<BorrowedBookResponse>),200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetBorrowedBooks([FromRoute] Guid id)
    {
        var result = await _readerService.GetBorrowedBooks(id);
        
        return Ok(result.Select(bb => bb.ToBorrowedBookResponse()).ToList());
    }
}