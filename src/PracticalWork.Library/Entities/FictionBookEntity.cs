namespace PracticalWork.Library.Entities;

/// <summary>
/// Художественная литература
/// </summary>
public sealed class FictionBookEntity : AbstractBookEntity
{
    /// <summary>Категория художественной прозы</summary>
    public string CategoriesOfFiction { get; set; }
}