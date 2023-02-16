using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMUCS.DTOs.Chats;
using SMUCS.DTOs.Contacts;
using SMUCS.Models;
using SMUCS.Models.Chats;
using SMUCS.Repositories.Chats.Interfaces;
using SMUCS.Repositories.Users.Interfaces;
using SMUCS.Services.Contacts.Interfaces;
using SMUCS.Services.Users.interfaces;
using SMUCS.Utilities;
using SMUCS.Utilities.ErrorHandling.Exceptions;
using SMUCS.Utilities.Time;

namespace SMUCS.Repositories.Chats
{
    public class ChatRepository : IChatRepository
    {
        private readonly ModelContext _context;
        private readonly IUserRepository _usersService;
        private readonly IUserInfoService _userInfoService;
        private readonly IContactsService _contactsService;

        public ChatRepository(ModelContext context, IUserInfoService userInfoService, IUserRepository usersService, IContactsService contactsService)
        {
            _context = context;
            _usersService = usersService;
            _userInfoService = userInfoService;
            _contactsService = contactsService;
        }

        public async Task<JsonResult> Add(int currentUserId, HashSet<ContactDto> users)
        {
            var userIds = users
                .Where(c => c.ContactId != null)
                .Select(c => c.ContactId.GetValueOrDefault())
                .ToHashSet();

            if (userIds.Count == 0)
                throw new HttpStatusException("Can't create empty chat", "EMPTY_CHAT_ERR", HttpStatusCode.BadRequest);

            if (userIds.Contains(currentUserId))
                throw new HttpStatusException("Can't create chat with yourself", "CHAT_ADD_YOURSELF_ERR", HttpStatusCode.BadRequest);
            
            userIds.Add(currentUserId);
            
            var chat = (_context.Chats
                .Include(c => c.ChatUsers).ThenInclude(cu => cu.User)
                .ToList())
                .FirstOrDefault(c => userIds.SetEquals(c.ChatUsers.Select(cu => cu.UserId)));

            if (chat != null)
                return new JsonResult(chat) { StatusCode = 200 };

            var userNames = new List<string>();
            var newChat = new Chat();
            users = users.Prepend(new ContactDto(){ ContactId = currentUserId }).ToHashSet();

            foreach (var user in users)
            {
                var contactId = Convert.ToInt32(user.ContactId);
                
                if(contactId != currentUserId && ! await _contactsService.Any(contactId, "en"))
                    throw new HttpStatusException("User is not in your contacts", "USER_NOT_IN_CONTACTS_ERR", HttpStatusCode.BadRequest);
                
                var globalUser = await _usersService.Find(contactId);
                if (globalUser == null)
                    globalUser = await _usersService.Create(contactId);

                var fullUser = await _userInfoService.GetFullUserInfo(contactId);
                userNames.Add(string.Format("{0}", fullUser.RealName));
                
                newChat.ChatUsers.Add(new ChatUser() { User = globalUser, IsModerator = contactId == currentUserId} );
            }
            newChat.Name = String.Join(", ", userNames.ToArray());
            
            _context.Chats.Add(newChat);
            await _context.SaveChangesAsync();

            return new JsonResult(newChat) { StatusCode = 201 };
        }

        public async Task<Chat> Delete(int chatId, int userId)
        {
            var chatToDelete = await Find(chatId, userId);
            if (chatToDelete == null)
                throw new HttpStatusException("Chat not found", "CHAT_NOT_FOUND_ERR", HttpStatusCode.NotFound);
            
            foreach (var chatUser in chatToDelete.ChatUsers)
                _context.ChatUsers.Remove(chatUser);

            foreach (var message in chatToDelete.Messages)
                _context.Messages.Remove(message);

            _context.Chats.Remove(chatToDelete);
            await _context.SaveChangesAsync();

            return chatToDelete;
        }

        public async Task<HashSet<ChatUser>> AddUsers(int chatId, int userId, HashSet<ContactDto> contacts)
        {
            var chat = await Find(chatId, userId);
            if (chat == null)
                throw new HttpStatusException("Chat not found", "CHAT_NOT_FOUND_ERR", HttpStatusCode.NotFound);

            var output = new HashSet<ChatUser>();
            foreach (var contact in contacts)
            {
                // Move to external service with additional check if user exists in the main system
                var contactId = Convert.ToInt32(contact.ContactId);
                
                if(! await _contactsService.Any(contactId, "en"))
                    throw new HttpStatusException("User is not in your contacts", "USER_NOT_IN_CONTACTS_ERR", HttpStatusCode.BadRequest);
                
                if (chat.ChatUsers.FirstOrDefault(cu => cu.User.UserId == contactId) != null)
                    throw new HttpStatusException("User is already in chat", "USER_ALREADY_IN_CHAT_ERR", HttpStatusCode.BadRequest);
                
                var globalUser = await _usersService.Find(contactId);
                if (globalUser == null)
                    globalUser = await _usersService.Create(contactId);

                var fullUser = await _userInfoService.GetFullUserInfo(contactId);
                if (!chat.CustomName)
                    chat.Name += (string.Format(", {0}", fullUser.RealName));
                
                var chatUserToAdd = new ChatUser() { User = globalUser };
                chat.ChatUsers.Add(chatUserToAdd);
                output.Add(chatUserToAdd);
            }
            
            _context.Chats.Update(chat);
            await _context.SaveChangesAsync();
            
            return output;
        }

        public async Task<HashSet<ChatUser>> DeleteUsers(int chatId, int userId, HashSet<ContactDto> contacts)
        {
            var chat = await Find(chatId, userId);
            if (chat == null)
                throw new HttpStatusException("Chat not found", "CHAT_NOT_FOUND_ERR", HttpStatusCode.NotFound);
            
            var output = new HashSet<ChatUser>();
            foreach (var contact in contacts)
            {
                // Move to external service with additional check if user exists in the main system
                var contactId = Convert.ToInt32(contact.ContactId);
                var chatUserToRemove = chat.ChatUsers.FirstOrDefault(cu => cu.User.UserId == contactId);
                if (chatUserToRemove == null)
                    throw new HttpStatusException("User is not in chat", "USER_NOT_IN_CHAT_ERR", HttpStatusCode.BadRequest);

                var fullUser = await _userInfoService.GetFullUserInfo(contactId);
                if (!chat.CustomName)
                    chat.Name = chat.Name.Replace(string.Format(", {0}", fullUser.RealName), "");
                
                output.Add(chatUserToRemove);
                _context.ChatUsers.Remove(chatUserToRemove);
            }
            
            await _context.SaveChangesAsync();

            return output;
        }

        public async Task<Chat> ChangeTitle(int chatId, int userId, string title)
        {
            var chat = await Find(chatId, userId);
            if (chat == null)
                throw new HttpStatusException("Chat not found", "CHAT_NOT_FOUND_ERR", HttpStatusCode.NotFound);

            chat.CustomName = true;
            chat.Name = title;
            _context.Chats.Update(chat);
            await _context.SaveChangesAsync();
            
            return chat;
        }

        public async Task<Message> AddMessage(int chatId, int senderId, string message, DateTime time, Attachment? attachment)
        {
            var chat = await Find(chatId, senderId);
            if (chat == null)
                throw new HttpStatusException("Chat not found", "CHAT_NOT_FOUND_ERR", HttpStatusCode.NotFound);

            var newMessage = new Message { SenderId = senderId, Text = message, Datetime = time, Status = "Sent"};
            if (attachment != null)
                newMessage.Attachments.Add(attachment);
            chat.Messages.Add(newMessage);

            _context.Chats.Update(chat);
            await _context.SaveChangesAsync();

            return newMessage;
        }

        public async Task<Message> UpdateMessageStatus(int messageId, string status)
        {
            var message = await GetMessage(messageId);

            message.Status = status;

            _context.Messages.Update(message);
            await _context.SaveChangesAsync();

            return message;
        }

        // TODO TimeZone
        public async Task<Message?> GetMessage(int messageId)
        {
            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.MessageId == messageId);

            if (message == null)
                throw new HttpStatusException("Message not found", "MESSAGE_NOT_FOUND_ERR", HttpStatusCode.NotFound);

            return message;
        }

        public async Task<bool> ChatExists(int chatId)
        {
            var chat = await _context.Chats.FirstOrDefaultAsync(c => c.ChatId == chatId);
            if (chat == null)
                return false;
            
            return true;
        }

        public async Task<Chat?> Find(int chatId, int userId)
        {
            var chat = (await FindAll(userId))
                .FirstOrDefault(c => c.ChatId == chatId);
            
            return chat;
        }
        
        public async Task<ChatUser?> FindChatUser(int chatId, int userId)
        {
            var chatUser = (await FindAll(userId))
                .Where(c => c.ChatId == chatId)
                .Select(c => c.ChatUsers.FirstOrDefault(cu => cu.UserId == userId))
                .FirstOrDefault();

            return chatUser;
        }

        public async Task<ICollection<Chat>> FindAll(int userId)
        {
            var chats = await _context.Chats
                .Include(c => c.ChatUsers).ThenInclude(cu => cu.User)
                .Include(c => c.Messages).ThenInclude(m => m.Attachments)
                .ToListAsync();

            if (chats.Count == 0)
                return new List<Chat>();

            return chats
                .Where(c => c.ChatUsers.Any(cu => cu.UserId == userId))
                .ToList();
        }

        public async Task<Chat> Get(int chatId, int userId, string timeZoneId, int pageSize, int pageNumber)
        {
            var chat = _context.Chats
                .Include(c => c.ChatUsers).ThenInclude(cu => cu.User)
                .Where(c => c.ChatUsers.Any(cu => cu.UserId == userId))
                .Include(c => c.Messages).ThenInclude(m => m.Attachments)
                .FirstOrDefault(c => c.ChatId == chatId);

            if (chat == null)
                throw new HttpStatusException("Chat not found", "CHAT_NOT_FOUND_ERR", HttpStatusCode.NotFound);

            IQueryable<Message> messages = _context.Messages
                .Include(m => m.Attachments)
                .AsNoTracking()
                .Where(m => m.ChatId == chat.ChatId)
                .OrderByDescending(m => m.MessageId);

            var result = new Chat { ChatId = chat.ChatId, 
                Name = chat.Name, 
                ChatUsers = chat.ChatUsers, 
                Messages = await PaginatedList<Message>.CreateAsync(messages, pageNumber, pageSize) };

            foreach(var message in result.Messages)
            {
                if (message.Datetime.HasValue)
                    message.Datetime = TimeConverter.ConvertFromUtc(message.Datetime.Value, timeZoneId);
            }

            return result;
        }

        public async Task<ICollection<Message>> SearchMessages(int userId, int? chatId, string timeZoneId, string filter, int pageSize, int pageNumber)
        {
            var chats = await FindAll(userId);

            IQueryable<Message> messages = _context.Messages
                .Include(m => m.Attachments)
                .AsNoTracking()
                .Where(m => chats.Contains(m.Chat))
                .OrderByDescending(m => m.MessageId)
                .Where(m => m.Text != null && m.Text.ToUpper().Contains(filter.ToUpper()));

            var result = await PaginatedList<Message>.CreateAsync(messages, pageNumber, pageSize);

            foreach (var message in result)
            {
                if (message.Datetime.HasValue)
                    message.Datetime = TimeConverter.ConvertFromUtc(message.Datetime.Value, timeZoneId);
            }

            if (chatId != null)
                return result
                    .Where(m => m.ChatId == chatId)
                    .ToList();
                
            return result;
        }

        public async Task<ICollection<ChatResultDto>> GetAll(int userId, string timeZoneId, int pageSize, int pageNumber)
        {
            IQueryable<ChatResultDto> chats = _context.Chats
                .Include(c => c.ChatUsers).ThenInclude(cu => cu.User)
                .Include(c => c.Messages).ThenInclude(m => m.Attachments)
                .AsNoTracking()
                .Where(c => c.ChatUsers.Any(cu => cu.UserId == userId))
                .Select(c => new ChatResultDto
                {
                    ChatId = c.ChatId,
                    Name = c.Name,
                    CustomName = c.CustomName,
                    ChatUsers = c.ChatUsers,
                    LastMessage = c.Messages.OrderByDescending(m => m.MessageId).FirstOrDefault()
                })
                .Where(c => c.LastMessage != null)
                .OrderByDescending(c => c.LastMessage != null  ? c.LastMessage.MessageId : 0);

            if (! await chats.AnyAsync())
                return new List<ChatResultDto>();

            var result = await PaginatedList<ChatResultDto>.CreateAsync(chats, pageNumber, pageSize);

            foreach (var chat in result)
            {
                if (chat.LastMessage != null && chat.LastMessage.Datetime.HasValue)
                    chat.LastMessage.Datetime = TimeConverter.ConvertFromUtc(chat.LastMessage.Datetime.Value, timeZoneId);
            }

            return result;
        }
        
        public async Task<ICollection<ChatResultDto>> SearchChats(int userId, string timeZoneId, string filter, int pageSize, int pageNumber)
        {
            var chats = (await GetAll(userId, timeZoneId, pageSize, pageNumber))
                .ToHashSet();
            
            if (chats.Count == 0)
                return new List<ChatResultDto>();
            foreach (var user in chats.SelectMany(c => c.ChatUsers))
            {
                var fullUser = await _userInfoService.GetFullUserInfo(user.UserId);
                user.User = fullUser;
            }

            var chatNameFilter = chats
                .Where(c => c.Name.ToUpper().Contains(filter.ToUpper()))
                .ToHashSet();
            
            var chatUsers = chats
                .SelectMany(c => c.ChatUsers.Where(cu => cu.User.RealName.ToUpper().Contains(filter.ToUpper()) 
                                                         || cu.User.ContactName != null && cu.User.ContactName.ToUpper().Contains(filter.ToUpper())))
                .ToHashSet();

            var chatUsersFilter = chats
                .Where(c => chatUsers.Any(cu => cu.ChatId == c.ChatId))
                .ToHashSet();

            return chatNameFilter.Concat(chatUsersFilter)
                .Distinct()
                .OrderByDescending(c => c.LastMessage != null ? c.LastMessage.MessageId : 0)
                .ToHashSet();
        }
    }
}
