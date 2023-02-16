using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMUCS.DTOs.Chats;
using SMUCS.DTOs.Contacts;
using SMUCS.Models.Chats;
using SMUCS.Services.Chats.Interfaces;
using SMUCS.Utilities.Responses;
using Swashbuckle.AspNetCore.Annotations;

namespace SMUCS.Controllers.Chats
{
    /// <summary>
    /// ### Controller that is working with chats, chatUsers, messages, attachments  
    /// All methods are working with the chats, messages, attachments for the current user only for safety reason
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ChatsController : ControllerBase
    {
        private readonly IChatsService _chatsService;

        public ChatsController(IChatsService chatsService)
        {
            _chatsService = chatsService;
        }

        /// <summary>
        /// Creates a chat with the users from the list of contacts
        /// </summary>
        /// <param name="lang">**en**, **ar**</param>
        /// <param name="contacts">List of contacts</param>
        /// <returns>
        /// </returns>
        /// <remarks>
        /// ### Name Generation
        /// By default newely created chat has generated property **Name**, which generates like ``` realName, realName2, ... ```
        /// ### SignalR
        /// After calling this method, the ``` /chat/ChatCreated ``` should be called, see more info in the ``` ChatHub ``` section
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | EMPTY_CHAT_ERR | 400 | Can't create empty chat |
        /// | CHAT_ADD_YOURSELF_ERR   | 400 | Can't create chat with yourself |
        /// | USER_NOT_IN_CONTACTS_ERR | 402 | User is not in your contacts |
        /// | EXT_GLOBAL_USER_NOT_FOUND_ERR | 404 | User not found in main system |
        /// | EXT_CONTACT_GET_ERR | 400 | Error, while getting contacts |
        /// </remarks>
        [Authorize]
        [HttpPost]
        [SwaggerResponse(200, "Chat already exists", typeof(Chat))]
        [SwaggerResponse(201, "Chat created", typeof(Chat))]
        [SwaggerResponse(400, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        [SwaggerResponse(402, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        public async Task<JsonResult> Create([FromHeader] string lang, [FromBody] HashSet<ContactDto> contacts)
        {
            return await _chatsService.Create(lang, contacts);
        }

        /// <summary>
        /// Adds a message to the chat
        /// </summary>
        /// <param name="chatId">Id of chat</param>
        /// <param name="message">Text of the message</param>
        /// <param name="attachment">
        /// #### Optional
        /// Attachment to the message
        /// </param>
        /// <returns></returns>
        /// <remarks>
        /// ### Message status
        /// By default the message is created with status **Sent**, which could change after chatUser has seen the message in method ``` /chat/Seen ```, see more info in the ``` ChatHub ``` section
        /// ### SignalR
        /// After calling this method, the ``` /chat/Sent ``` should be called, see more info in the ``` ChatHub ``` section
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | EXT_ATTACHMENT_UPLOAD_ERR | 400 | Error while uploading a file |
        /// | CHAT_NOT_FOUND_ERR | 404 | Chat not found |
        /// </remarks>
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = 100_000_000_000)]
        [RequestSizeLimit(100_000_000_000)]
        [Authorize]
        [HttpPost("AddMessage")]
        [SwaggerResponse(200, "Message added", typeof(Message))]
        [SwaggerResponse(400, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        [SwaggerResponse(404, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        public async Task<JsonResult> AddMessage([FromQuery] int chatId, [FromQuery] string message, [FromForm] IFormFile? attachment, [FromHeader] string? timeZoneId)
        {
            if(timeZoneId == null)
                timeZoneId = TimeZoneInfo.Local.Id;

            return await _chatsService.AddMessage(chatId, message, attachment, timeZoneId);
        }

        /// <summary>
        /// Views attachment
        /// </summary>
        /// <param name="attachmentId">Id of attachment</param>
        /// <returns></returns>
        /// <remarks>
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | ATTACHMENT_NOT_FOUND_ERR | 404 | Attachment not found |
        /// | EXT_ATTACHMENT_VIEW_ERR | 400 | Attachment view error |
        /// | CHAT_NOT_FOUND_ERR | 404 | Chat not found |
        /// </remarks>
        [Authorize]
        [HttpGet("ViewAttachment")]
        [SwaggerResponse(200, "Attachment is displayed", typeof(FileContentResult))]
        [SwaggerResponse(400, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        [SwaggerResponse(404, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        public async Task<FileContentResult> ViewAttachment([FromQuery] int attachmentId)
        {
            return await DownloadAttachment(attachmentId);
        }

        /// <summary>
        /// Downloads attachment
        /// </summary>
        /// <param name="attachmentId">Id of attachment</param>
        /// <returns></returns>
        /// <remarks>
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | ATTACHMENT_NOT_FOUND_ERR | 404 | Attachment not found |
        /// | EXT_ATTACHMENT_VIEW_ERR | 400 | Attachment view error |
        /// | CHAT_NOT_FOUND_ERR | 404 | Chat not found |
        /// </remarks>
        [Authorize]
        [HttpGet("DownloadAttachment")]
        [SwaggerResponse(200, "Attachment is downloaded", typeof(FileContentResult))]
        [SwaggerResponse(400, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        [SwaggerResponse(404, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        public async Task<FileContentResult> DownloadAttachment([FromQuery] int attachmentId)
        {
            var pair = await _chatsService.DownloadAttachment(attachmentId);

            return File(pair.Key, pair.Value);
        }

        /// <summary>
        /// Returns all attachments as paginated list
        /// </summary>
        /// <param name="chatId">Id of chat</param>
        /// <param name="pageSize">
        /// #### Optional
        /// #### Size of page
        /// </param>
        /// <param name="pageNumber">
        /// #### Optional
        /// #### Number of page
        /// </param>
        /// <returns></returns>
        /// <remarks>
        /// ### Usage
        /// Use **pageSize** to regulate the size of returned page and **pageNumber** to get specific page of the paginated list
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | CHAT_NOT_FOUND_ERR | 404 | Chat not found |
        /// </remarks>
        [Authorize]
        [HttpGet("GetAttachments")]
        [ProducesResponseType(typeof(ICollection<Attachment>), 200)]
        [SwaggerResponse(404, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        public async Task<JsonResult> GetAttachments([FromQuery] int chatId, [FromQuery] int pageSize = 5, [FromQuery] int pageNumber = 1)
        {
            return await _chatsService.GetAttachments(chatId, pageSize, pageNumber);
        }

        /// <summary>
        /// Deletes the chat
        /// </summary>
        /// <param name="lang">**en**, **ar**</param>
        /// <param name="chatId">Id of chat</param>
        /// <returns></returns>
        /// <remarks>
        /// ### Authorization
        /// chatUser, who is calling this method, needs to be **isModerator**
        /// ### SignalR
        /// After calling this method, the ``` /chat/ChatDeleted ``` should be called, see more info in the ``` ChatHub ``` section
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | CHAT_NOT_FOUND_ERR | 404 | Chat not found |
        /// | CHATUSER_NOT_MODER_ERR | 402 | Chat user is not moderator |
        /// | CHATUSER_NOT_FOUND_ERR | 404 | Chat user is not found |
        /// </remarks>
        [Authorize]
        [HttpDelete]
        [SwaggerResponse(200, "Chat deleted", typeof(Chat))]
        [SwaggerResponse(402, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        [SwaggerResponse(404, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        public async Task<JsonResult> Delete([FromHeader] string lang, [FromQuery] int chatId)
        {
            return await _chatsService.Delete(lang, chatId);
        }

        /// <summary>
        /// Changes the name of chat
        /// </summary>
        /// <param name="chatId">Id of chat</param>
        /// <param name="title">The new custom name</param>
        /// <returns></returns>
        /// <remarks>
        /// ### Name Generation
        /// Sets **CustomName** to **true**, which stops the name generation
        /// ### SignalR
        /// After calling this method, the ``` /chat/ChatUpdated ``` should be called, see more info in the ``` ChatHub ``` section
        /// ### Authorization
        /// chatUser, who is calling this method, needs to be **isModerator**
        /// ### Remarks
        /// Changes the **Name** property of chat
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | CHAT_NOT_FOUND_ERR | 404 | Chat not found |
        /// | CHATUSER_NOT_MODER_ERR | 402 | Chat user is not moderator |
        /// | CHATUSER_NOT_FOUND_ERR | 404 | Chat user is not found |
        /// </remarks>
        [Authorize]
        [HttpPost("ChangeTitle")]
        [SwaggerResponse(200, "Name changed", typeof(Chat))]
        [SwaggerResponse(402, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        [SwaggerResponse(404, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        public async Task<JsonResult> ChangeTitle([FromQuery] int chatId, [FromQuery] string title)
        {
            return await _chatsService.ChangeTitle(chatId, title);
        }

        /// <summary>
        /// Adds users to chat
        /// </summary>
        /// <param name="chatId">Id of chat</param>
        /// <param name="contacts">Users from contacts to add</param>
        /// <returns></returns>
        /// <remarks>
        /// ### Name Generation
        /// If the **CustomName** is **false**, will be generated like ``` realName, realName2, ... ``` with new users added
        /// ### SignalR
        /// After calling this method, the ``` /chat/ChatUsersAdded ``` should be called, see more info in the ``` ChatHub ``` section
        /// ### Authorization
        /// chatUser, who is calling this method, needs to be **isModerator**
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | CHAT_NOT_FOUND_ERR | 404 | Chat not found |
        /// | USER_NOT_IN_CONTACTS_ERR | 402 | User is not in your contacts |
        /// | USER_ALREADY_IN_CHAT_ERR | 400 | User is already in chat |
        /// </remarks>
        [Authorize]
        [HttpPost("AddUsers")]
        [SwaggerResponse(200, "Users were added", typeof(Chat))]
        [SwaggerResponse(400, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        [SwaggerResponse(402, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        [SwaggerResponse(404, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        public async Task<JsonResult> AddUsers([FromQuery] int chatId, [FromBody] HashSet<ContactDto> contacts)
        {
            return await _chatsService.AddUsers(chatId, contacts);
        }

        /// <summary>
        /// Deletes users from chat
        /// </summary>
        /// <param name="chatId">Id of chat</param>
        /// <param name="contacts">Users from contacts to delete</param>
        /// <returns></returns>
        /// <remarks>
        /// ### Name Generation
        /// If the **CustomName** is **false**, will be generated like ``` realName, realName2, ... ``` with the users removed
        /// ### SignalR
        /// After calling this method, the ``` /chat/ChatUsersDeleted ``` should be called, see more info in the ``` ChatHub ``` section
        /// ### Authorization
        /// chatUser, who is calling this method, needs to be **isModerator**
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | CHAT_NOT_FOUND_ERR | 404 | Chat not found |
        /// | USER_NOT_IN_CONTACTS_ERR | 402 | User is not in your contacts |
        /// | USER_NOT_IN_CHAT_ERR | 400 | User is not in chat |
        /// </remarks>
        [Authorize]
        [HttpDelete("DeleteUsers")]
        [SwaggerResponse(200, "Users were deleted", typeof(Chat))]
        [SwaggerResponse(400, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        [SwaggerResponse(402, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        [SwaggerResponse(404, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        public async Task<JsonResult> DeleteUsers([FromQuery] int chatId, [FromBody] HashSet<ContactDto> contacts)
        {
            return await _chatsService.DeleteUsers(chatId, contacts);
        }

        /// <summary>
        /// Returns the paginated list of all chats
        /// </summary>
        /// <param name="lang">**en**, **ar**</param>
        /// <param name="pageSize">
        /// #### Optional
        /// #### Size of page
        /// </param>
        /// <param name="pageNumber">
        /// #### Optional
        /// #### Number of page
        /// </param>
        /// <returns></returns>
        /// <remarks>
        /// ### Remarks
        /// Can not be null, returned as empty  
        /// Only shows the last message
        /// </remarks>
        [Authorize]
        [HttpGet("GetAll")]
        [SwaggerResponse(200, "Paginated list of chats", typeof(ICollection<ChatResultDto>))]
        public async Task<JsonResult> GetAll([FromHeader] string lang, [FromHeader] string? timeZoneId, [FromQuery] int pageSize=10, [FromQuery] int pageNumber=1)
        {
            if (timeZoneId == null)
                timeZoneId = TimeZoneInfo.Local.Id;

            return await _chatsService.GetAll(lang, timeZoneId, pageSize, pageNumber);
        }
        
        /// <summary>
        /// Get the chat
        /// </summary>
        /// <param name="chatId">Id of chat</param>
        /// <param name="lang">**en**, **ar**</param>
        /// <param name="pageSize">
        /// #### Optional
        /// #### Size of page
        /// </param>
        /// <param name="pageNumber">
        /// #### Optional
        /// #### Number of page
        /// </param>
        /// <returns></returns>
        /// <remarks>
        /// ### Remarks
        /// Can not be null, returned as empty  
        /// Shows all messages
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | CHAT_NOT_FOUND_ERR | 404 | Chat not found |
        /// </remarks>
        [Authorize]
        [HttpGet("Get")]
        [SwaggerResponse(200, "Chat", typeof(Chat))]
        [SwaggerResponse(404, "Error occured. Check remarks for more info", typeof(ErrorResponse))]
        public async Task<JsonResult> Get([FromQuery] int chatId, [FromHeader] string lang, [FromHeader] string? timeZoneId, [FromQuery] int pageSize = 25, [FromQuery] int pageNumber = 1)
        {
            if (timeZoneId == null)
                timeZoneId = TimeZoneInfo.Local.Id;

            return await _chatsService.Get(chatId, lang, timeZoneId, pageSize, pageNumber);
        }

        /// <summary>
        /// Search through chats or messages
        /// </summary>
        /// <param name="target">**messages**, **chats**</param>
        /// <param name="lang">"en", "ar"</param>
        /// <param name="pageSize">
        /// #### Optional
        /// #### Size of page
        /// </param>
        /// <param name="pageNumber">
        /// #### Optional
        /// #### Number of page
        /// </param>
        /// <param name="chatId">
        /// #### Optional
        /// Only used when target is **messages**
        /// </param>
        /// <param name="filter">
        /// #### Optional
        /// #### Filter for the string query
        /// By default will be empty string, which results into no filter
        /// </param>
        /// <returns></returns>
        /// <remarks>
        /// ### Remarks
        /// On target **chats** return contains the chats with the last message only  
        /// Will search automaticaly through users realNames and contactNames**(for current user)** fetched from the main system
        /// ### Usage
        /// Use **pageSize** to regulate the size of returned page and **pageNumber** to get specific page of the paginated list
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | CHAT_SEARCH_WRONG_TARGET | 400 | Available targets: messages, chats |
        /// | EXT_GLOBAL_USER_NOT_FOUND_ERR | 404 | User not found in main system |
        /// | EXT_CONTACT_GET_ERR | 400 | Error, while getting contacts |
        /// </remarks>
        [Authorize]
        [HttpGet("Search")]
        [SwaggerResponse(200, "Paginated list of chats / messages", typeof(ICollection<Chat>))]
        public async Task<JsonResult> Search([FromQuery] string target, [FromHeader] string lang, [FromHeader] string? timeZoneId, [FromQuery] int pageSize = 10, [FromQuery] int pageNumber = 1,[FromQuery] int? chatId = null, [FromQuery] string? filter = null)
        {
            if (timeZoneId == null)
                timeZoneId = TimeZoneInfo.Local.Id;

            if (filter == null)
                filter = "";

            return await _chatsService.Search(target, filter, lang, timeZoneId, pageSize, pageNumber, chatId);
        }
    }
}
