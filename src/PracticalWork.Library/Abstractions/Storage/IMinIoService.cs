namespace PracticalWork.Library.Abstractions.Storage;

/// <summary>
/// Сервис для работы с хранилищем MinIO
/// </summary>
public interface IMinIoService
{
    /// <summary>
    /// Загрузка файла в хранилище
    /// </summary>
    /// <param name="fileName">Название файла</param>
    /// <param name="fileStream">Стрим файла</param>
    /// <param name="extension">Тип расширения файла</param>
    /// <returns></returns>
    Task UploadFileAsync(string fileName, Stream fileStream, string extension);

    /// <summary>
    /// Получить ссылку на файл
    /// </summary>
    /// <param name="fileName">Название файла</param>
    /// <param name="expiryMinutes">Продолжительность работы ссылки</param>
    /// <returns></returns>
    Task<string> GetFileUrlAsync(string fileName, int expiryMinutes = 60);
}