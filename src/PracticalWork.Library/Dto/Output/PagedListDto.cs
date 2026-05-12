namespace PracticalWork.Library.Dto.Output;

/// <summary>
/// Список данных с информацией о пагинации
/// </summary>
/// <typeparam name="T">Данные для отправки пользователю</typeparam>
public class PagedListDto<T>
{
    /// <summary>Список данных</summary>
    public IEnumerable<T> Items { get; set; } = [];

    /// <summary>Номер страницы</summary>
    public int Page { get; set; }

    /// <summary>Размер страницы</summary>
    public int PageSize { get; set; }

    /// <summary>Общее количество данных</summary>
    public int TotalCount { get; set; }

    /// <summary>Количество страниц</summary>
    public int TotalPages =>
        (int)Math.Ceiling((double)TotalCount / PageSize);
}