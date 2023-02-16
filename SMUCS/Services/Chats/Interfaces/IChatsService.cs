using Microsoft.AspNetCore.Mvc;
using SMUCS.DTOs.Contacts;

namespace SMUCS.Services.Chats.Interfaces
{
    public interface IChatsService
    {
        Task<JsonResult> Create(string lang, HashSet<ContactDto> contacts);
        Task<JsonResult> Delete(string lang, int chatId);
        Task<JsonResult> ChangeTitle(int chatId, string title);
        Task<JsonResult> AddUsers(int chatId, HashSet<ContactDto> contacts);
        Task<JsonResult> DeleteUsers(int chatId, HashSet<ContactDto> contacts);
        Task<JsonResult> GetAll(string lang, string timeZoneId, int pageSize, int pageNumber);
        Task<JsonResult> Get(int chatId, string lang, string timeZoneId, int pageSize, int pageNumber);
        Task<KeyValuePair<byte[], string>> DownloadAttachment(int attachmentId);
        Task<JsonResult> GetAttachments(int chatId, int pageSize, int pageNumber);
        Task<JsonResult> AddMessage(int chatId, string message, IFormFile? attachment, string timeZoneId);
        Task<JsonResult> Search(string target, string filter, string lang, string timeZoneId, int pageSize, int pageNumber, int? chatId);
    }
}
