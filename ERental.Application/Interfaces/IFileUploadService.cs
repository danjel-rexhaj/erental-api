namespace ERental.Application.Interfaces;

public interface IFileUploadService
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string? contentType, string folder);
}
