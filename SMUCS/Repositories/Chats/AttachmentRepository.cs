using System.Net;
using Microsoft.EntityFrameworkCore;
using SMUCS.DTOs.FileUploader;
using SMUCS.Models;
using SMUCS.Models.Chats;
using SMUCS.Repositories.Chats.Interfaces;
using SMUCS.Services.FileUploader.Interfaces;
using SMUCS.Utilities;
using SMUCS.Utilities.ErrorHandling.Exceptions;

namespace SMUCS.Repositories.Chats
{
    public class AttachmentRepository : IAtachmentRepository
    {
        private readonly IFileUploaderService _fileUploaderService;
        private readonly ModelContext _context;
        private readonly IChatRepository _chatRepository;
        private readonly string _attachmentsPath;

        public AttachmentRepository(ModelContext context, IFileUploaderService fileUploaderService, IChatRepository chatRepository, IConfiguration configuration)
        {
            _context = context;
            _fileUploaderService = fileUploaderService;
            _chatRepository = chatRepository;
            _attachmentsPath = configuration["AttachmentsFolderName"];
            _fileUploaderService.CreateDirectory(_attachmentsPath);
        }

        public async Task<Attachment> Create(IFormFile file)
        {
            FileUploaderDto fileUploaderDto;
            try
            {
                fileUploaderDto = await _fileUploaderService.UploadFile(_attachmentsPath, file);
            }
            catch
            {
                throw new HttpStatusException("Error while uploading a file", "ATTACHMENT_UPLOAD_ERR", HttpStatusCode.BadRequest);
            }

            var attachment = new Attachment()
            {
                FilePath = fileUploaderDto.TargetPath,
                FileName = file.FileName,
                FileSize = fileUploaderDto.Size,

            };
            _context.Attachments.Add(attachment);
            await _context.SaveChangesAsync();

            return attachment;
        }
    
        public async Task<KeyValuePair<byte[], string>> Download(int attachmentId, int userId)
        {
            var attachment = await _context.Attachments
                .Include(a => a.Message)
                .FirstOrDefaultAsync(a => a.AttachmentId == attachmentId);

            if (attachment == null)
                throw new HttpStatusException("Attachment not found", "ATTACHMENT_NOT_FOUND", HttpStatusCode.NotFound);

            var chat = await _chatRepository
                .Find(attachment.Message.ChatId, userId);

            if (chat == null)
                throw new HttpStatusException("Chat not found", "CHAT_NOT_FOUND_ERR", HttpStatusCode.NotFound);

            var responseMessage = await _fileUploaderService.DownloadFile(attachment.FilePath);
        
            return responseMessage;
        }
    
        public async Task<Attachment> Delete(int attachmentId, int userId)
        {
            var attachment = await _context.Attachments
                .Include(a => a.Message)
                .FirstOrDefaultAsync(a => a.AttachmentId == attachmentId);

            if (attachment == null)
                throw new HttpStatusException("Attachment not found", "ATTACHMENT_NOT_FOUND", HttpStatusCode.NotFound);

            var chat = await _chatRepository
                .Find(attachment.Message.ChatId, userId);

            if (chat == null)
                throw new HttpStatusException("Chat not found", "CHAT_NOT_FOUND_ERR", HttpStatusCode.NotFound);

            _context.Attachments.Remove(attachment);
            await _context.SaveChangesAsync();

            await _fileUploaderService.DeleteFile(attachment.FilePath);

            return attachment;
        }
    
        public async Task<Attachment> Update(Attachment attachment, int userId)
        {
            _context.Attachments.Update(attachment);
            await _context.SaveChangesAsync();

            return attachment;
        }
    
        public async Task<ICollection<Attachment>> GetAll(int chatId, int userId, int pageSize, int pageNumber)
        {
            var chat = await _chatRepository
                .Find(chatId, userId);

            if (chat == null)
                throw new HttpStatusException("Chat not found", "CHAT_NOT_FOUND_ERR", HttpStatusCode.NotFound);

            IQueryable<Attachment> attachments = _context.Attachments
                .Include(a => a.Message)
                .AsNoTracking()
                .Where(a => a.Message.ChatId == chatId)
                .OrderByDescending(a => a.AttachmentId);

            if (! await attachments.AnyAsync())
                return new List<Attachment>();

            var result = await PaginatedList<Attachment>.CreateAsync(attachments, pageNumber, pageSize);

            return result;
        }
    }
}