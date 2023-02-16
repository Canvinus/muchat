using SMUCS.DTOs.FileUploader;

namespace SMUCS.Services.FileUploader.Interfaces;

public interface IFileUploaderService
{
    Task<bool> CreateDirectory(string path);
    Task DeleteDirectory(string path);
    Task<ICollection<DirViewDto>> ViewDirectory(string path);
    Task<ICollection<string>> ViewDirectoryOS(string path);
    Task<FileUploaderDto> UploadFile(string dirPath, IFormFile file);
    FileUploaderDto ShowFileInfo(string filePath);
    Task DeleteFile(string filePath);
    Task<KeyValuePair<byte[], string>> DownloadFile(string filePath);
}