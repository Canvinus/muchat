using System.Net;
using Microsoft.AspNetCore.Mvc;
using SMUCS.DTOs.Contacts;
using SMUCS.Repositories.Chats.Interfaces;
using SMUCS.Services.Chats.Interfaces;
using SMUCS.Services.JWT.interfaces;
using System.Security.Claims;
using SMUCS.Utilities.ErrorHandling.Exceptions;
using SMUCS.Utilities.Time;
using SMUCS.Repositories.Users.Interfaces;

namespace SMUCS.Services.Chats
{
    public class ChatsService : IChatsService
    {
        private readonly IChatRepository _chatRepository;
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly IAtachmentRepository _atachmentRepository;

        public ChatsService(IChatRepository chatRepository, ITokenService tokenService, IAtachmentRepository atachmentRepository, IUserRepository userRepository)
        {
            _chatRepository = chatRepository;
            _tokenService = tokenService;
            _atachmentRepository = atachmentRepository;
            _userRepository = userRepository;
        }

        public async Task<JsonResult> Create(string lang, HashSet<ContactDto> contacts)
        {
            var currentUserId = Convert.ToInt32(await _tokenService.ReadClaim(ClaimTypes.NameIdentifier));

            var newChat = await _chatRepository.Add(currentUserId, contacts);

            return newChat;
        }
        
        public async Task<JsonResult> AddMessage(int chatId, string message, IFormFile? attachment, string timeZoneId)
        {
            var now = DateTime.UtcNow;
            var currentUserId = Convert.ToInt32(await _tokenService.ReadClaim(ClaimTypes.NameIdentifier));

            Models.Chats.Attachment resAttachment = null;
            if (attachment != null)
            {
                resAttachment = await _atachmentRepository.Create(attachment);
            }

            var messageResult = await _chatRepository.AddMessage(chatId, currentUserId, message, now,
                resAttachment);

            // Updating LastActivity
            var user = await _userRepository.Find(currentUserId);
            if (user == null)
                throw new HttpStatusException("User not found", "USER_NOT_FOUND_ERR", HttpStatusCode.NotFound);
            user.LastActivity = now;
            await _userRepository.Update(user);

            messageResult.Datetime = TimeConverter.ConvertFromUtc(DateTime.UtcNow, timeZoneId);

            return new JsonResult(messageResult) { StatusCode = 200 };
        }

        private async Task ModeratorCheck(int chatId, int userId)
        {
            var chatUser = await _chatRepository.FindChatUser(chatId, userId);
            if (chatUser == null)
                throw new HttpStatusException("ChatUser not found", "CHATUSER_NOT_FOUND", HttpStatusCode.NotFound);
            if(chatUser.IsModerator == false)
                throw new HttpStatusException("ChatUser is not moderator", "CHATUSER_NOT_MODER", HttpStatusCode.Forbidden);
        }
        
        public async Task<JsonResult> Delete(string lang, int chatId)
        {
            var currentUserId = Convert.ToInt32(await _tokenService.ReadClaim(ClaimTypes.NameIdentifier));
            var chat = _chatRepository.Find(chatId, currentUserId);
            if(chat == null)
                throw new HttpStatusException("Chat not found", "CHAT_NOT_FOUND", HttpStatusCode.NotFound);
            await ModeratorCheck(chatId, currentUserId);

            return new JsonResult(await _chatRepository.Delete(chatId, currentUserId)) { StatusCode = 200 };
        }
        
        public async Task<JsonResult> ChangeTitle(int chatId, string title)
        {
            var currentUserId = Convert.ToInt32(await _tokenService.ReadClaim(ClaimTypes.NameIdentifier));
            await ModeratorCheck(chatId, currentUserId);
            
            return new JsonResult(await _chatRepository.ChangeTitle(chatId, currentUserId, title)) { StatusCode = 200 };
        }
        
        public async Task<JsonResult> AddUsers(int chatId, HashSet<ContactDto> contacts)
        {
            var currentUserId = Convert.ToInt32(await _tokenService.ReadClaim(ClaimTypes.NameIdentifier));
            await ModeratorCheck(chatId, currentUserId);
            
            return new JsonResult(await _chatRepository.AddUsers(chatId, currentUserId, contacts)) { StatusCode = 200 };
        }
        
        public async Task<JsonResult> DeleteUsers(int chatId, HashSet<ContactDto> contacts)
        {
            var currentUserId = Convert.ToInt32(await _tokenService.ReadClaim(ClaimTypes.NameIdentifier));
            await ModeratorCheck(chatId, currentUserId);
            
            return new JsonResult(await _chatRepository.DeleteUsers(chatId, currentUserId, contacts)) { StatusCode = 200 };
        }

        public async Task<JsonResult> GetAll(string lang, string timeZoneId, int pageSize, int pageNumber)
        {
            var currentUserId = Convert.ToInt32(await _tokenService.ReadClaim(ClaimTypes.NameIdentifier));
            
            return new JsonResult(await _chatRepository.GetAll(currentUserId, timeZoneId, pageSize, pageNumber)) { StatusCode = 200 };
        }

        public async Task<JsonResult> Get(int chatId, string lang, string timeZoneId, int pageSize, int pageNumber)
        {
            var currentUserId = Convert.ToInt32(await _tokenService.ReadClaim(ClaimTypes.NameIdentifier));

            return new JsonResult(await _chatRepository.Get(chatId, currentUserId, timeZoneId, pageSize, pageNumber)) { StatusCode = 200 };
        }

        public async Task<KeyValuePair<byte[], string>> DownloadAttachment(int attachmentId)
        {
            var currentUserId = Convert.ToInt32(await _tokenService.ReadClaim(ClaimTypes.NameIdentifier));

            var response = await _atachmentRepository.Download(attachmentId, currentUserId);

            return response;
        }

        public async Task<JsonResult> GetAttachments(int chatId, int pageSize, int pageNumber)
        {
            var currentUserId = Convert.ToInt32(await _tokenService.ReadClaim(ClaimTypes.NameIdentifier));
            
            var attachments =  await _atachmentRepository.GetAll(chatId, currentUserId, pageSize, pageNumber);
            return new JsonResult(attachments) { StatusCode = 200 };
        }

        public async Task<JsonResult> Search(string target, string filter, string lang, string timeZoneId, int pageSize, int pageNumber, int? chatId)
        {
            var currentUserId = Convert.ToInt32(await _tokenService.ReadClaim(ClaimTypes.NameIdentifier));

            switch (target) 
            {
                case "messages":
                    return new JsonResult(await _chatRepository.SearchMessages(currentUserId, chatId, timeZoneId, filter, pageSize, pageNumber)) { StatusCode = 200 };

                case "chats":
                    return new JsonResult(await _chatRepository.SearchChats(currentUserId, timeZoneId, filter, pageSize, pageNumber)) { StatusCode = 200 };

                default:
                    throw new HttpStatusException("Available targets: messages, chats", "CHAT_SEARCH_WRONG_TARGET", HttpStatusCode.BadRequest);
            }
        }

    }
}
