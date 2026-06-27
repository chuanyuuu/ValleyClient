using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using ValleyClient.Enums;
using ValleyClient.Helpers;
using ValleyClient.Models;
using ValleyClient.Services;

namespace ValleyClient.Services
{
    public class MessageService
    {
        public static MessageService Instance { get; } = new();
        private MessageService()
        {
            NativeInteropService.Instance.OnReceivePrivateChatMsg += OnReceiveRemotePrivateMsg;
        }

        public static Window? MainWindow;
        private readonly Dictionary<long, List<ChatMessage>> _privateMessageCache = new();
        public Action<long, ChatMessage>? OnNewPrivateMessageArrived;

        // 默认兜底头像
        private readonly string _defaultAvatar = "ms-appx:///Assets/ControlImages/PersonPicture.png";

        #region 缓存读写（改造：优先读内存，无数据自动加载数据库）
        /// <summary>获取好友聊天历史（内存优先，无缓存从SQLite加载）</summary>
        public List<ChatMessage> GetFriendChatHistory(long friendId, int pageSize = 100)
        {
            // 内存存在直接返回副本
            if (_privateMessageCache.TryGetValue(friendId, out var cacheList))
                return cacheList.ToList();

            // 内存无缓存，从本地数据库加载
            var dbList = SqliteMessageStorage.Instance.GetFriendMessageHistory(friendId, pageSize);
            _privateMessageCache[friendId] = dbList;
            LogHelper.Debug($"从数据库加载好友{friendId}聊天记录，共{dbList.Count}条");
            return dbList.ToList();
        }

        private void AddMessageToCache(long targetId, ChatMessage msg)
        {
            if (!_privateMessageCache.ContainsKey(targetId))
                _privateMessageCache[targetId] = new List<ChatMessage>();
            _privateMessageCache[targetId].Add(msg);
        }

        /// <summary>清空单个好友内存缓存 + 删除本地数据库记录</summary>
        public void ClearFriendMessageCache(long friendId)
        {
            if (_privateMessageCache.ContainsKey(friendId))
                _privateMessageCache.Remove(friendId);
            // 同步删除数据库
            SqliteMessageStorage.Instance.ClearFriendMessage(friendId);
            LogHelper.Debug($"清空好友{friendId}内存缓存与本地数据库记录");
        }

        /// <summary>清空当前账号所有内存缓存 + 数据库全部聊天记录</summary>
        public void ClearAllMessageCache()
        {
            _privateMessageCache.Clear();
            SqliteMessageStorage.Instance.ClearAllSelfMessage();
            LogHelper.Debug("清空当前账号全部聊天缓存与本地数据库记录");
        }
        #endregion

        #region 新增未读消息相关接口
        /// <summary>获取好友未读消息数量</summary>
        public int GetFriendUnreadCount(long friendId)
        {
            return SqliteMessageStorage.Instance.GetUnreadCount(friendId);
        }

        /// <summary>打开聊天窗口，将该好友所有消息标记为已读</summary>
        public void MarkFriendAllMessageRead(long friendId)
        {
            SqliteMessageStorage.Instance.MarkAllRead(friendId);
            LogHelper.Debug($"好友{friendId}所有消息已标记为已读");
        }
        #endregion

        #region 发送消息（新增：发送后自动存入SQLite持久化）
        public async void SendPrivateMessage(long targetFriendId, string content, MessageType msgType = MessageType.Text)
        {
            long selfId = UserService.Instance.GetCurrentUserId();
            string selfName = UserService.Instance.GetCurrentUserName();
            if (selfId == 0 || string.IsNullOrEmpty(selfName))
                return;

            ChatMessage msg = new ChatMessage
            {
                MsgId = DateTime.Now.Ticks,
                SenderId = selfId,
                SenderName = selfName,
                TargetFriendId = targetFriendId,
                Content = content,
                MsgType = msgType,
                SendTime = DateTime.Now,
                IsSelfSend = true
            };

            AddMessageToCache(targetFriendId, msg);
            // 持久化存入SQLite
            SqliteMessageStorage.Instance.InsertMessage(msg);
            OnNewPrivateMessageArrived?.Invoke(targetFriendId, msg);
            await NetworkService.Instance.SendChatMessage(targetFriendId, msg);
        }
        #endregion

        #region 接收消息 + 动态头像通知（新增：接收远端消息自动存入SQLite）
        private void OnReceiveRemotePrivateMsg(ChatMessage msg)
        {
            if (MainWindow is null || MainWindow.DispatcherQueue is null)
            {
                LogHelper.Warn("推送通知失败：窗口调度队列为空");
                return;
            }

            long senderId = msg.SenderId;
            MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                AddMessageToCache(senderId, msg);
                // 接收对方消息，持久化存入本地数据库
                SqliteMessageStorage.Instance.InsertMessage(msg);
                OnNewPrivateMessageArrived?.Invoke(senderId, msg);

                // 自己发的消息不弹通知
                if (msg.IsSelfSend)
                {
                    LogHelper.Debug("自身消息，跳过系统通知");
                    return;
                }

                // 1. 根据发送人ID获取好友信息
                var friend = FriendService.Instance.GetFriendById(senderId);

                // 2. 动态判断头像
                string avatarPath = _defaultAvatar;
                if (friend != null && !string.IsNullOrWhiteSpace(friend.AvatarUri))
                {
                    avatarPath = friend.AvatarUri;
                }

                Uri avatarUri;
                try
                {
                    avatarUri = new Uri(avatarPath);
                }
                catch
                {
                    // 头像地址格式错误，降级默认图
                    avatarUri = new Uri(_defaultAvatar);
                }

                // 消息内容截断
                string showText = msg.Content.Length > 40 ? $"{msg.Content[..40]}..." : msg.Content;

                // 构建通知：名称、内容、对应发送者头像
                AppNotification notification = new AppNotificationBuilder()
                    .AddText(msg.SenderName)
                    .AddText(showText)
                    .SetAppLogoOverride(avatarUri, AppNotificationImageCrop.Circle)
                    .SetAudioEvent(AppNotificationSoundEvent.Default)
                    .SetTimeStamp(DateTime.Now)
                    .BuildNotification();

                AppNotificationManager.Default.Show(notification);
                LogHelper.Info($"收到 {msg.SenderName} 消息，通知头像地址：{avatarPath}");
            });
        }
        #endregion
    }
}