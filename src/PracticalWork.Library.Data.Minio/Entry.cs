using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PracticalWork.Library.Data.Minio;

public static class Entry
{
    /// <summary>
    /// Регистрация зависимостей для хранилища документов
    /// </summary>
    public static IServiceCollection AddMinioFileStorage(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        // Реализация подключения к Minio и сервисов
        serviceCollection.Configure<MinIoOptions>(configuration.GetSection("App:Minio"));

        return serviceCollection;
    }
}
