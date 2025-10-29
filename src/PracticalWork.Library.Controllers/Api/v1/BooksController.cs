using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Contracts.v1.Books.Request;
using PracticalWork.Library.Contracts.v1.Books.Response;
using PracticalWork.Library.Controllers.Mappers.v1;

namespace PracticalWork.Library.Controllers.Api.v1;

[ApiController]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/books")]
public class BooksController : Controller
{
    private readonly IBookService _bookService;

    public BooksController(IBookService bookService)
    {
        _bookService = bookService;
    }

    /// <summary>Создание новой книги</summary>
    [HttpPost]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CreateBookResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CreateBook([FromBody] CreateBookRequest request)
    {
        var result = await _bookService.CreateBook(request.ToBook());

        return Content(result.ToString());
    }

    /// <summary>Обновление данных книги</summary>
    [HttpPut("{id:guid}")]
    [Produces("application/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UpdateBook([FromRoute] Guid id, [FromBody] UpdateBookRequest request)
    {
        await _bookService.UpdateBook(id, request.ToUpdateBookDto());
        
        return Ok();
    }

    /// <summary>Перевод книги в архив</summary>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(typeof(ArchiveBookResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ArchiveBook([FromRoute] Guid id)
    {
        var result = await _bookService.ArchiveBook(id);
        
        return Ok(result.ToArchiveBookResponse());
    }
    
    /// <summary>Получение списка книг</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IList<BookListResponse>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetBooks([FromBody] GetBookListRequest request)
    {
        var result = await _bookService.GetBooks(request.ToGetBookListDto());

        return Ok(result.Select(bl => bl.ToBookListResponse()).ToList());
    }
    
    /// <summary>Добавление деталей книг</summary>
    [HttpPost("{id:guid}/details")]
    [Produces("application/json")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> AddBookDetails([FromRoute] Guid id, [FromBody] AddBookDetailsRequest request)
    {
        await _bookService.CreateBookDetails(id, request.CoverImage, request.Description);
        
        return Ok();
    }
}