using Microsoft.AspNetCore.Mvc;
using SMUCS.DTOs.Chats;
using SMUCS.DTOs.Contacts;
using SMUCS.Models.Chats;

namespace SMUCS.Repositories.Chats.Interfaces
{
    public interface IChatRepository
    {
        Task<JsonResult> Add(int userId, HashSet<ContactDto> users);
        Task<Chat> Delete(int chatId, int userId);
        Task<Message> AddMessage(int chatId, int senderId, string message, DateTime time, Attachment? attachment);
        Task<Message> UpdateMessageStatus(int messageId, string status);
        Task<bool> ChatExists(int chatId);
        Task<Chat?> Find(int chatId, int userId);
        Task<Message?> GetMessage(int messageId);
        Task<ICollection<Chat>> FindAll(int userId);
        Task<ChatUser?> FindChatUser(int chatId, int userId);
        Task<HashSet<ChatUser>> AddUsers(int chatId, int userId, HashSet<ContactDto> contacts);
        Task<HashSet<ChatUser>> DeleteUsers(int chatId, int userId, HashSet<ContactDto> contacts);
        Task<Chat> ChangeTitle(int chatId, int userId, string title);
        Task<Chat> Get(int chatId, int userId, string timeZoneId, int pageSize, int pageNumber);
        Task<ICollection<Message>> SearchMessages(int userId, int? chatId, string timeZoneId, string filter, int pageSize, int pageNumber);
        Task<ICollection<ChatResultDto>> GetAll(int userId, string timeZoneId, int pageSize, int pageNumber);
        Task<ICollection<ChatResultDto>> SearchChats(int userId, string timeZoneId, string filter, int pageSize, int pageNumber);
    }
}
