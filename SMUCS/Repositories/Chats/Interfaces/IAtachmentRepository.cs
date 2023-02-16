using SMUCS.Models.Chats;

namespace SMUCS.Repositories.Chats.Interfaces;

public interface IAtachmentRepository
{
    Task<Attachment> Create(IFormFile file);
    Task<KeyValuePair<byte[], string>> Download(int attachmentId, int userId);
    Task<ICollection<Attachment>> GetAll(int chatId, int userId, int pageSize, int pageNumber);
}