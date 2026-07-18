using ERental.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ERental.Infrastructure.Services;

public class FileUploadService : IFileUploadService
{
    private readonly IConfiguration _config;
    public FileUploadService(IConfiguration config) => _config = config;

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string? contentType, string folder)
    {
        var accessKey = _config["R2:AccessKey"];
        var secretKey = _config["R2:SecretKey"];
        var endpoint = _config["R2:Endpoint"];
        var bucketName = _config["R2:BucketName"];
        var publicUrl = _config["R2:PublicUrl"];

        var s3Config = new Amazon.S3.AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
        };

        using var s3Client = new Amazon.S3.AmazonS3Client(accessKey, secretKey, s3Config);

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

        return $"{publicUrl}/{key}";
    }
}
