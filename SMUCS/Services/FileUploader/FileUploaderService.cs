using Microsoft.AspNetCore.StaticFiles;
using SMUCS.DTOs.FileUploader;
using SMUCS.Services.FileUploader.Interfaces;
using SMUCS.Utilities.ErrorHandling.Exceptions;
using SMUCS.Utilities.Hashing;

namespace SMUCS.Services.FileUploader;

public class FileUploaderService : IFileUploaderService
{
    private readonly string _targetFolderPath;
    private readonly uint _hashBytes;

    public FileUploaderService(IConfiguration configuration)
    {
        _targetFolderPath = configuration["FileUploader:TargetFolderPath"];
        _hashBytes = Convert.ToUInt32(configuration["FileUploader:HashBytes"]);

        CreateDirectory(_targetFolderPath);
    }
    private string FormatPath(string path)
    {
        return path.Replace("\\", "/"); ;
    }

    private string SubPath(string dirPath, string fullPath)
    {
        return fullPath.Replace(dirPath, "").Replace("\\", "/").Remove(0, 1);
    }

    private long GetDirectorySize(string folderPath)
    {
        DirectoryInfo di = new DirectoryInfo(folderPath);
        return di.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
    }

    public async Task<bool> CreateDirectory(string path)
    {
        var reqPath = Path.Combine(_targetFolderPath, path);
        if (Directory.Exists(reqPath))
            return false;

        await Task.Run(() => { 
                Directory.CreateDirectory(reqPath);
        });
        return true;
    }

    public async Task DeleteDirectory(string path)
    {
        var reqPath = Path.Combine(_targetFolderPath, path);

        await Task.Run(() => Directory.Delete(reqPath, true));
    }

    public async Task<ICollection<DirViewDto>> ViewDirectory(string path)
    {
        var reqPath = Path.Combine(_targetFolderPath, path);

        var dirs = Directory.GetDirectories(reqPath)
            .Select(entry => new DirViewDto(){ Type = "Directory", Info = new FileUploaderDto(){ Name = SubPath(reqPath, entry), TargetPath = SubPath(_targetFolderPath, entry), FullPath = FormatPath(entry), Size = GetDirectorySize(entry) } })
            .ToList();

        var files = Directory.GetFiles(reqPath)
            .Select(entry => new DirViewDto() { Type = "File", Info = ShowFileInfo(SubPath(_targetFolderPath, entry)) })
            .ToList(); 

        return dirs
            .Concat(files)
            .ToList();
    }

    public async Task<ICollection<string>> ViewDirectoryOS(string path)
    {
        var reqPath = Path.Combine(_targetFolderPath, path);

        var dirs = Directory.GetDirectories(reqPath)
            .Select(entry =>  "|DIR| " + entry.Replace(_targetFolderPath, "").Replace("\\", "/").Remove(0, 1) )
            .ToList();

        var files = Directory.GetFiles(reqPath)
            .Select(entry => "|FILE| "  + entry.Replace(_targetFolderPath, "").Replace("\\", "/").Remove(0, 1) )
            .ToList();

        return dirs
            .Concat(files)
            .ToList();
    }

    public async Task<FileUploaderDto> UploadFile(string dirPath, IFormFile file)
    {
        var fileInfo = new FileInfo(file.FileName);
        var reqPath = Path.Combine(
            _targetFolderPath, 
            dirPath, 
            HashGen.GenHashString(_hashBytes) + fileInfo.Extension);

        using (var stream = new FileStream(reqPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        fileInfo = new FileInfo(reqPath);
        return new FileUploaderDto() { FullPath = reqPath.Replace("\\", "/"), TargetPath = reqPath.Replace(_targetFolderPath, "").Replace("\\", "/").Remove(0, 1), Name = fileInfo.Name, Size = fileInfo.Length };
    }

    public FileUploaderDto ShowFileInfo(string filePath)
    {
        var reqPath = Path.Combine(_targetFolderPath, filePath);
        var fileInfo = new FileInfo(reqPath);

        return new FileUploaderDto() { FullPath = reqPath.Replace("\\", "/"), TargetPath = reqPath.Replace(_targetFolderPath, "").Replace("\\", "/").Remove(0, 1), Name = fileInfo.Name, Size = fileInfo.Length };
    }

    public async Task DeleteFile(string filePath)
    {
        var reqPath = Path.Combine(_targetFolderPath, filePath);

        await Task.Run(() => File.Delete(reqPath));
    }

    public async Task<KeyValuePair<byte[], string>> DownloadFile(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var reqPath = Path.Combine(_targetFolderPath, filePath);

        var provider = new FileExtensionContentTypeProvider();
        string contentType;
        if (!provider.TryGetContentType(fileInfo.Name + fileInfo.Extension, out contentType))
        {
            throw new HttpStatusException("Could not read content-type", "CONTENT_TYPE_READ_ERR", System.Net.HttpStatusCode.BadRequest);
        }

        return new KeyValuePair<byte[], string>(await File.ReadAllBytesAsync(reqPath), contentType);
    }
}