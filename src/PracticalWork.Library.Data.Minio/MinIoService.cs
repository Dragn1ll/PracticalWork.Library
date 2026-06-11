using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using PracticalWork.Library.Abstractions.Storage;

namespace PracticalWork.Library.Data.Minio;

/// <inheritdoc cref="IMinIoService"/>
public class MinIoService : IMinIoService
{
    private readonly IMinioClient _minioClient;
    private readonly string _defaultBucketName;

    public MinIoService(IOptions<MinIoOptions> minioOptions)
    {
        _minioClient = new MinioClient()
            .WithEndpoint(minioOptions.Value.Endpoint)
            .WithCredentials(minioOptions.Value.AccessKey, minioOptions.Value.SecretKey)
            .Build();
        _defaultBucketName = minioOptions.Value.BucketName;
    }

    /// <inheritdoc cref="IMinIoService.UploadFileAsync"/>
    public async Task UploadFileAsync(
        string fileName,
        Stream fileStream,
        string contentType,
        string bucketName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Пустое название файла!", nameof(fileName));
        if (fileStream is null)
            throw new ArgumentNullException(nameof(fileStream));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Пустой тип содержимого!", nameof(contentType));

        var target = bucketName ?? _defaultBucketName;
        await EnsureBucketExistsAsync(target);

        var putObjectArgs = new PutObjectArgs()
            .WithBucket(target)
            .WithObject(fileName)
            .WithStreamData(fileStream)
            .WithObjectSize(fileStream.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putObjectArgs);
    }

    /// <inheritdoc cref="IMinIoService.GetFileUrlAsync"/>
    public async Task<string> GetFileUrlAsync(
        string fileName,
        int expiryMinutes = 60,
        string bucketName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Пустое название файла!", nameof(fileName));

        var target = bucketName ?? _defaultBucketName;
        await EnsureBucketExistsAsync(target);

        var presignedGetArgs = new PresignedGetObjectArgs()
            .WithBucket(target)
            .WithObject(fileName)
            .WithExpiry(expiryMinutes * 60);

        return await _minioClient.PresignedGetObjectAsync(presignedGetArgs);
    }

    private async Task EnsureBucketExistsAsync(string bucket)
    {
        var exists = await _minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucket));

        if (!exists)
        {
            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(bucket));
        }
    }
}