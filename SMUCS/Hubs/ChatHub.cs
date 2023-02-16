using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalRSwaggerGen.Attributes;
using SMUCS.Repositories.Chats.Interfaces;
using SMUCS.Models.Chats;
using SMUCS.Utilities.ErrorHandling.Exceptions;
using SMUCS.Repositories.Notifications.Interfaces;
using SMUCS.Repositories.Users.Interfaces;

namespace SMUCS.Hubs
{
    [Authorize]
    [SignalRHub("/chat")]
    public class ChatHub : Hub
    {
        private readonly IChatRepository _chatRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IUserRepository _userRepository;

        public ChatHub(IChatRepository chatRepository, IUserRepository userRepository, INotificationRepository notificationRepository)
        {
            _chatRepository = chatRepository;
            _userRepository = userRepository;
            _notificationRepository = notificationRepository;
        }

        /// <summary>
        /// Called when user is connected to the signalR hub
        /// </summary>
        /// <remarks>
        /// ### Remarks
        /// Setting the status of global user as **Online** then calling the front-end method to notify all users of changed status  
        /// Adding user to all SignalR chat groups
        /// ### Methods called on front-end
        /// ``` updateOnlineStatus(userId:string, status:string) ``` which should update the online status of global user
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | USER_NOT_FOUND_ERR | 404 | User not found |
        /// </remarks>
        [SignalRMethod(description: "### Remarks \n Setting the status of global user as **Online** then calling the front-end method to notify all users of changed status \n Adding user to all SignalR chat groups \n ### Methods called on front-end \n ``` updateOnlineStatus(userId:string, status:string) ``` which should update the online status of global user \n ### Possible errors \n | ErrorCode | Status | Description | \n | ----------- | ----------- | ----------- | \n | USER_NOT_FOUND_ERR | 404 | User not found |")]
        public override async Task OnConnectedAsync()
        {
            var currentUserId = Convert.ToInt32(Context.UserIdentifier);
            
            var user = await _userRepository.Find(currentUserId);
            if (user == null)
                throw new HttpStatusException("User not found", "USER_NOT_FOUND_ERR", HttpStatusCode.NotFound);
            user.Status = "Online";
            user.ConnectionId = Context.ConnectionId;
            await _userRepository.Update(user);
            
            var chats =  await _chatRepository.FindAll(currentUserId);
            foreach (var chat in chats)
            {
                // Connect to chat groups
                await Groups.AddToGroupAsync(Context.ConnectionId, chat.ChatId.ToString());

                await Clients.Group(chat.ChatId.ToString())
                .SendAsync("updateOnlineStatus", currentUserId.ToString(), "Online");
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when user is disconnected from the signalR hub
        /// </summary>
        /// <remarks>
        /// ### Remarks
        /// Setting the status of global user as **Offline** then calling the front-end method to notify all users of changed status  
        /// Removing user from all SignalR chat groups
        /// ### Methods called on front-end
        /// ``` updateOnlineStatus(userId:string, status:string) ``` which should update the online status of global user
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | USER_NOT_FOUND_ERR | 404 | User not found |
        /// </remarks>
        [SignalRMethod(description: "### Remarks \n  Setting the status of global user as **Offline** then calling the front-end method to notify all users of changed status \n  Removing user from all SignalR chat groups \n  ### Methods called on front-end \n  ``` updateOnlineStatus(userId:string, status:string) ``` which should update the online status of global user \n  ### Possible errors \n  | ErrorCode | Status | Description | \n  | ----------- | ----------- | ----------- | \n  | USER_NOT_FOUND_ERR | 404 | User not found |")]
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var currentUserId = Convert.ToInt32(Context.UserIdentifier);

            var user = await _userRepository.Find(currentUserId);
            if (user == null)
                throw new HttpStatusException("User not found", "USER_NOT_FOUND_ERR", HttpStatusCode.NotFound);
            user.Status = "Offline";
            user.ConnectionId = null;
            await _userRepository.Update(user);
            
            var chats = await _chatRepository.FindAll(currentUserId);
            foreach (var chat in chats)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, chat.ChatId.ToString());
                
                await Clients.Group(chat.ChatId.ToString())
                .SendAsync("updateOnlineStatus", currentUserId.ToString(), "Offline");
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Called when the new chat is created
        /// </summary>
        /// <param name="chatId">Id of chat</param>
        /// <remarks>
        /// ### Remarks
        /// Adding all users that are not **Offline** to SignalR chat group and calling the method to update all chats from the stored **ConnectionId** in **DB**
        /// ### Methods called on front-end
        /// ``` updateChats() ``` which should update the list of chats
        /// </remarks>
        [SignalRMethod(description: "### Remarks \n  Adding all users that are not **Offline** to SignalR chat group and calling the method to update all chats from the stored **ConnectionId** in **DB** \n  ### Methods called on front-end \n  ``` updateChats() ``` which should update the list of chats")]
        public async Task ChatCreated(string chatId)
        {
            var currentUserId = Convert.ToInt32(Context.UserIdentifier);
            
            var chat = await _chatRepository
                .Find(Convert.ToInt32(chatId), currentUserId);

            if (chat == null)
                throw new HttpStatusException("Chat not found", "CHAT_NOT_FOUND_ERR", HttpStatusCode.NotFound);

            foreach (var chatUser in chat.ChatUsers)
            {
                if (chatUser.User.Status != "Offline" && chatUser.User.ConnectionId != null)
                {
                    await Groups.AddToGroupAsync(chatUser.User.ConnectionId, chat.ChatId.ToString());
                    await Clients.Client(chatUser.User.ConnectionId)
                        .SendAsync("updateChats");
                }
            }
        }

        /// <summary>
        /// Called when the chat is deleted
        /// </summary>
        /// <param name="chat">Chat object</param>
        /// <remarks>
        /// ### Remarks
        /// Removing all users that are not **Offline** to SignalR chat group and calling the method to update all chats from the stored **ConnectionId** in **DB**
        /// ### Methods called on front-end
        /// ``` updateChats() ``` which should update the list of chats
        /// </remarks>
        [SignalRMethod(description: "### Remarks \n Removing all users that are not **Offline** to SignalR chat group and calling the method to update all chats from the stored **ConnectionId** in **DB** \n ### Methods called on front-end \n ``` updateChats() ``` which should update the list of chats \n")]
        public async Task ChatDeleted(Chat chat)
        {
            foreach (var chatUser in chat.ChatUsers)
            {
                if (chatUser.User.Status != "Offline" && chatUser.User.ConnectionId != null)
                {
                    await Groups.RemoveFromGroupAsync(chatUser.User.ConnectionId, chat.ChatId.ToString());
                    await Clients.Client(chatUser.User.ConnectionId)
                        .SendAsync("updateChats");
                }
            }
        }

        /// <summary>
        /// Called when the chat is updated
        /// </summary>
        /// <param name="chatId">Id of chat</param>
        /// <remarks>
        /// ### Remarks
        /// Calling the method to update the specific chat for the whole SignalR chat group
        /// ### Methods called on front-end
        /// ``` updateChat(chatId:string) ``` which should update the specific chat
        /// </remarks>
        [SignalRMethod(description: "### Remarks \n Calling the method to update the specific chat for the whole SignalR chat group \n ### Methods called on front-end \n ``` updateChat(chatId:string) ``` which should update the specific chat \n")]
        public async Task ChatUpdated(string chatId)
        {
            await Clients.Group(chatId)
                .SendAsync("updateChat", chatId);
        }

        /// <summary>
        /// Called when the current global user is updated
        /// </summary>
        /// <remarks>
        /// ### Remarks
        /// Calling the method to update the specific global user in all SignalR chat groups
        /// ### Methods called on front-end
        /// ``` updateOnlineStatus(userId:string, status:string) ``` which should update the onlne status of specific user
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | USER_NOT_FOUND_ERR | 404 | User not found |
        /// </remarks>
        [SignalRMethod(description: "### Methods called on front-end \n ``` updateOnlineStatus(userId:string, status:string) ``` which should update the onlne status of specific user \n ### Possible errors \n | ErrorCode | Status | Description | \n | ----------- | ----------- | ----------- | \n | USER_NOT_FOUND_ERR | 404 | User not found | \n")]
        public async Task GlobalUserUpdated()
        {
            var currentUserId = Convert.ToInt32(Context.UserIdentifier);
            var user = await _userRepository.Find(currentUserId);
            if (user == null)
                throw new HttpStatusException("User not found", "USER_NOT_FOUND", HttpStatusCode.NotFound);

            var chats = await _chatRepository.FindAll(currentUserId);
            foreach (var chat in chats)
            {
                await Clients.Group(chat.ChatId.ToString())
                .SendAsync("updateOnlineStatus", currentUserId.ToString(), user.Status);
            }
        }

        /// <summary>
        /// Called when the chatUsers are added
        /// </summary>
        /// <param name="chatId">Id of chat</param>
        /// <param name="chatUsersToAdd">List of chatUsers</param>
        /// <remarks>
        /// ### Remarks
        /// First adds the new users to SignalR chat group  
        /// Calling the method to update the specific chat for the whole SignalR chat group
        /// ### Methods called on front-end
        /// ``` updateChat(chatId:string) ``` which should update the specific chat
        /// </remarks>
        [SignalRMethod(description: "### Remarks \n First adds the new users to SignalR chat group  \n Calling the method to update the specific chat for the whole SignalR chat group \n ### Methods called on front-end \n ``` updateChat(chatId:string) ``` which should update the specific chat \n")]
        public async Task ChatUsersAdded(string chatId, HashSet<ChatUser> chatUsersToAdd)
        {
            foreach (var chatUser in chatUsersToAdd)
            {
                if (chatUser.User.Status != "Offline" && chatUser.User.ConnectionId != null)
                {
                    await Groups.AddToGroupAsync(chatUser.User.ConnectionId, chatId);
                }
            }
            await Clients.Group(chatId)
                .SendAsync("updateChat", chatId);
        }

        /// <summary>
        /// Called when the chatUsers are deleted
        /// </summary>
        /// <param name="chatId">Id of chat</param>
        /// <param name="chatUsersToDelete">List of chatUsers</param>
        /// <remarks>
        /// ### Remarks
        /// First deletes the deleted users from SignalR chat group  
        /// Calling the method to update the specific chat for the whole SignalR chat group
        /// ### Methods called on front-end
        /// ``` updateChat(chatId:string) ``` which should update the specific chat
        /// </remarks>
        [SignalRMethod(description: "### Remarks \n First deletes the deleted users from SignalR chat group  \n Calling the method to update the specific chat for the whole SignalR chat group \n ### Methods called on front-end \n ``` updateChat(chatId:string) ``` which should update the specific chat \n")]
        public async Task ChatUsersDeleted(string chatId, HashSet<ChatUser> chatUsersToDelete)
        {
            foreach (var chatUser in chatUsersToDelete)
            {
                if (chatUser.User.Status != "Offline" && chatUser.User.ConnectionId != null)
                {
                    await Groups.RemoveFromGroupAsync(chatUser.User.ConnectionId, chatId);
                }
            }
            await Clients.Group(chatId)
                .SendAsync("updateChat", chatId);
        }

        /// <summary>
        /// Called after message is sent
        /// </summary>
        /// <param name="messageObj">Message object</param>
        /// <returns></returns>
        /// <remarks>
        /// ### Remarks
        /// First **LastActivity** is updated  
        /// Then new notification is added to **DB** and called ``` notify(userId:string, message:Message) ```  
        /// Then ``` broadcastMessage(chatId:string, userId:string, message:Message) ``` is called
        /// Then ``` messageStatus(chatId:string, messageId:string, status:string) ``` is set to **Sent**
        /// ### Methods called on front-end
        /// ``` notify(message:Message) ``` which should notify the user with push-notification  
        /// ``` broadcastMessage(chatId:string, userId:string, message:Message) ``` which should broadcast the message to chat  
        /// ``` messageStatus(chatId:string, messageId:string, status:string) ``` which update the message status  
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | USER_NOT_FOUND_ERR | 404 | User not found |
        /// | CHAT_NOT_FOUND_ERR | 404 | Chat not found |
        /// </remarks>
        [SignalRMethod(description: "### Remarks \n First **LastActivity** is updated  \n Then new notification is added to **DB** and called ``` notify(userId:string, message:Message) ```  \n Then ``` broadcastMessage(chatId:string, userId:string, message:Message) ``` is called  \n Then ``` messageStatus(chatId:string, messageId:string, status:string) ``` is set to **Sent** \n ### Methods called on front-end \n ``` notify(userId:string, message:Message) ``` which should notify the user with push-notification  \n ``` broadcastMessage(chatId:string, userId:string, message:Message) ``` which should broadcast the message to chat  \n ``` messageStatus(chatId:string, messageId:string, status:string) ``` which update the message status  \n ### Possible errors \n | ErrorCode | Status | Description | \n | ----------- | ----------- | ----------- | \n | USER_NOT_FOUND_ERR | 404 | User not found | \n | CHAT_NOT_FOUND_ERR | 404 | Chat not found | \n")]
        public async Task Sent(Message messageObj)
        {
            var currentUserId = Convert.ToInt32(Context.UserIdentifier);

            // Notification
            var chat = await _chatRepository.Find(messageObj.ChatId, currentUserId);
            if (chat == null)
                throw new HttpStatusException("Chat not found", "CHAT_NOT_FOUND_ERR", HttpStatusCode.NotFound);
            foreach (var chatUser in chat.ChatUsers) 
            {
                if (chatUser.UserId == currentUserId)
                    continue;

                await _notificationRepository.Create(chatUser.UserId, messageObj);

                if (chatUser.User.Status == "Offline")
                    continue;

                await Clients.Client(chatUser.User.ConnectionId)
                    .SendAsync("notify", messageObj);
            }

            // Broadcasting message and status
            await Clients.Group(messageObj.ChatId.ToString())
                .SendAsync("broadcastMessage", messageObj.ChatId, currentUserId.ToString(), messageObj);
            
            await Clients.Group(messageObj.ChatId.ToString())
                .SendAsync("messageStatus", messageObj.ChatId, messageObj.MessageId, "Sent");
        }

        /// <summary>
        /// Called when the user started typing
        /// </summary>
        /// <param name="chatId">Id of chat</param>
        /// <param name="yes">Started typing or ended typing</param>
        /// <returns></returns>
        /// <remarks>
        /// ### Remarks
        /// IsTyping is shown is the chat with the user
        /// ### Methods called on front-end
        /// ``` tempMessage(chatId:string, userId:string, message:string) ``` which should send temp message to chat  
        /// ``` clearTempMessage(chatId:string, userId:string) ``` which should clear temp message in chat
        /// </remarks>
        [SignalRMethod(description: "### Remarks \n IsTyping is shown is the chat with the user \n  ### Methods called on front-end \n  ``` tempMessage(chatId:string, userId:string, message:string) ``` which should send temp message to chat  \n  ``` clearTempMessage(chatId:string, userId:string) ``` which should clear temp message in chat")]
        public async Task IsTyping(string chatId, bool yes)
        {
            var currentUserId = Convert.ToInt32(Context.UserIdentifier);
            
            if (yes)
                await Clients.Group(chatId)
                    .SendAsync("tempMessage", chatId, currentUserId.ToString(), "is typing...");
            else
                await Clients.Group(chatId)
                    .SendAsync("clearTempMessage", chatId, currentUserId.ToString());
        }

        /// <summary>
        /// Called after message is seen
        /// </summary>
        /// <param name="chatId">Id of chat</param>
        /// <param name="messageId">Id of message</param>
        /// <returns></returns>
        /// <remarks>
        /// ### Remarks
        /// First **Status** of message is updated in **DB**  
        /// Then ``` messageStatus(chatId:string, messageId:string, status:string) ``` is set to **Seen**
        /// ### Methods called on front-end
        /// ``` messageStatus(chatId:string, messageId:string, status:string) ``` which update the message status
        /// ### Possible errors
        /// | ErrorCode | Status | Description |
        /// | ----------- | ----------- | ----------- |
        /// | MESSAGE_NOT_FOUND_ERR | 404 | Message not found |
        /// </remarks>
        [SignalRMethod(description: "### Remarks \n First **Status** of message is updated in **DB**  \n Then ``` messageStatus(chatId:string, messageId:string, status:string) ``` is set to **Seen** \n  ### Methods called on front-end \n  ``` messageStatus(chatId:string, messageId:string, status:string) ``` which update the message status \n  ### Possible errors \n  | ErrorCode | Status | Description | \n  | ----------- | ----------- | ----------- | \n  | MESSAGE_NOT_FOUND_ERR | 404 | Message not found |")]
        public async Task Seen(string chatId, string messageId)
        {
            if (Convert.ToInt32(Context.UserIdentifier) == (await _chatRepository.GetMessage(Convert.ToInt32(messageId))).SenderId)
                return;

            await _chatRepository.UpdateMessageStatus(Convert.ToInt32(messageId), "Seen");
            
            await Clients.Group(chatId)
                .SendAsync("messageStatus", chatId, messageId, "Seen");
        }
    }
}