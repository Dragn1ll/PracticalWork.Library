using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using PracticalWork.Library.Abstractions.Storage;

namespace PracticalWork.Library.Data.Minio;

/// <inheritdoc cref="IMinIoService"/>
public class MinIoService : IMinIoService
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;

    public MinIoService(IOptions<MinIoOptions> minioOptions)
    {
        _minioClient = new MinioClient()
            .WithEndpoint(minioOptions.Value.Endpoint)
            .WithCredentials(minioOptions.Value.AccessKey, minioOptions.Value.SecretKey)
            .Build();
        _bucketName = minioOptions.Value.BucketName;
    }

    /// <inheritdoc cref="IMinIoService.UploadFileAsync"/>
    public async Task UploadFileAsync(string fileName, Stream fileStream, string extensionType)
    {
        await CheckBucketAsync();
        
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Пустое название файла!");
        }

        if (fileStream == null)
        {
            throw new ArgumentNullException(nameof(fileStream));
        }

        if (string.IsNullOrWhiteSpace(extensionType))
        {
            throw new ArgumentException("Пустое название типа файла!");
        }
        
        var putObjectArgs = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(fileName)
            .WithStreamData(fileStream)
            .WithObjectSize(fileStream.Length)
            .WithContentType(extensionType);
        
        await _minioClient.PutObjectAsync(putObjectArgs);
    }

    /// <inheritdoc cref="IMinIoService.GetFileUrlAsync"/>
    public async Task<string> GetFileUrlAsync(string fileName, int expiryMinutes = 60)
    {
        await CheckBucketAsync();
        
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Пустое название файла!");
        }
        
        var presignedGetArgs = new PresignedGetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(fileName)
            .WithExpiry(expiryMinutes * 60);
        
        return await _minioClient.PresignedGetObjectAsync(presignedGetArgs);
    }

    private async Task CheckBucketAsync()
    {
        var bucketExists = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName));
        if (!bucketExists)
        {
            await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName));
        }
    }
}