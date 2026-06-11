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
    /// <param name="contentType">MIME-тип файла</param>
    /// <param name="bucketName">Имя бакета (null = бакет по умолчанию)</param>
    Task UploadFileAsync(
        string fileName,
        Stream fileStream,
        string contentType,
        string bucketName = null);

    /// <summary>
    /// Получить presigned-ссылку на файл
    /// </summary>
    /// <param name="fileName">Название файла</param>
    /// <param name="expiryMinutes">Время жизни ссылки в минутах</param>
    /// <param name="bucketName">Имя бакета (null = бакет по умолчанию)</param>
    Task<string> GetFileUrlAsync(
        string fileName,
        int expiryMinutes = 60,
        string bucketName = null);
}