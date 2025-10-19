namespace PracticalWork.Library.Models;

/// <summary>
/// Читатель
/// </summary>
public sealed class Reader
{
    /// <summary>ФИО читателя</summary>
    public string FullName { get; set; }

    /// <summary>Номер телефона</summary>
    public string PhoneNumber { get; set; }

    /// <summary>Дата окончания действия</summary>
    public DateOnly ExpiryDate { get; set; }

    /// <summary>Активность карточки</summary>
    public bool IsActive { get; set; }
}