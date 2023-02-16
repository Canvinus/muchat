using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMUCS.Services.FileUploader.Interfaces;
using SMUCS.Utilities.Responses;

namespace SMUCS.Controllers.FileUploader
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FileUploaderController : ControllerBase
    {
        private readonly IFileUploaderService _fileUploaderService;

        public FileUploaderController(IFileUploaderService fileUploaderService)
        {
            _fileUploaderService = fileUploaderService;
        }

        [HttpPost("CreateDir")]
        public async Task<JsonResult> CreateDir([FromQuery] string path)
        {
            var res = await _fileUploaderService.CreateDirectory(path);

            if(!res)
                return new JsonResult(new SuccessResponse() { Message = "Directory already exists" }) { StatusCode = 200 };

            return new JsonResult(new SuccessResponse() { Message = "Directory was created" }) { StatusCode = 200 };
        }

        [HttpGet("ViewDir")]
        public async Task<JsonResult> ViewDir([FromQuery] string? path)
        {
            if (path == null)
                path = "";

            return new JsonResult(await _fileUploaderService.ViewDirectory(path)) { StatusCode = 200 };
        }

        [HttpGet("ViewDirOS")]
        public async Task<JsonResult> ViewDirOS([FromQuery] string? path)
        {
            if (path == null)
                path = "";

            return new JsonResult(await _fileUploaderService.ViewDirectoryOS(path)) { StatusCode = 200 };
        }

        [HttpDelete("DeleteDir")]
        public async Task<JsonResult> DeleteDir([FromQuery] string path)
        {
            await _fileUploaderService.DeleteDirectory(path);

            return new JsonResult(new SuccessResponse() { Message = "Directory was deleted"}) { StatusCode = 200 };
        }

        [HttpGet("ShowFileInfo")]
        public async Task<JsonResult> ShowFileInfo([FromQuery] string path)
        {
            return new JsonResult(_fileUploaderService.ShowFileInfo(path)) { StatusCode = 200 };
        }

        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = 100_000_000_000)]
        [RequestSizeLimit(100_000_000_000)]
        [HttpPost("UploadFile")]
        public async Task<JsonResult> UploadFile([FromForm] IFormFile file, [FromQuery] string? path)
        {
            if (path == null)
                path = "";

            return new JsonResult(await _fileUploaderService.UploadFile(path, file)) { StatusCode = 200 };
        }

        [HttpPost("DownloadFile")]
        public async Task<FileContentResult> DownloadFile([FromQuery] string path)
        {
            var pair = await _fileUploaderService.DownloadFile(path);

            return File(pair.Key, pair.Value);
        }

        [HttpDelete("DeleteFile")]
        public async Task<JsonResult> DeleteFile([FromQuery] string path)
        {
            await _fileUploaderService.DeleteFile(path);

            return new JsonResult(new SuccessResponse() { Message = "File was deleted" }) { StatusCode = 200 };
        }
    }
}
