namespace ERental.Application.Interfaces;

// Separate from IFileUploadService because that one uploads to the public-read bucket used for
// photos/logos — this one is for documents (driving licenses) that must never get a shareable URL.
public interface IPrivateFileService
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string? contentType, string folder);

    Task<(Stream Stream, string? ContentType)> DownloadAsync(string key);
}
