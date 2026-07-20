using ERental.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ERental.Infrastructure.Services;

public class PrivateFileService : IPrivateFileService
{
    private readonly IConfiguration _config;
    public PrivateFileService(IConfiguration config) => _config = config;

    private Amazon.S3.AmazonS3Client BuildClient()
    {
        var accessKey = _config["R2:AccessKey"];
        var secretKey = _config["R2:SecretKey"];
        var endpoint = _config["R2:Endpoint"];

        var s3Config = new Amazon.S3.AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
        };

        return new Amazon.S3.AmazonS3Client(accessKey, secretKey, s3Config);
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string? contentType, string folder)
    {
        var bucketName = _config["R2:PrivateBucketName"];
        using var s3Client = BuildClient();

        var key = $"{folder}/{Guid.NewGuid()}{Path.GetExtension(fileName)}";

        var putRequest = new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = contentType,
            DisablePayloadSigning = true,
            UseChunkEncoding = false
        };

        await s3Client.PutObjectAsync(putRequest);

        return key;
    }

    public async Task<(Stream Stream, string? ContentType)> DownloadAsync(string key)
    {
        var bucketName = _config["R2:PrivateBucketName"];
        var s3Client = BuildClient();

        var response = await s3Client.GetObjectAsync(new Amazon.S3.Model.GetObjectRequest
        {
            BucketName = bucketName,
            Key = key
        });

        var memory = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memory);
        memory.Position = 0;
        s3Client.Dispose();

        return (memory, response.Headers.ContentType);
    }
}
