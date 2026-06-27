using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ValleyClient.Helpers;
using ValleyClient.Models;

namespace ValleyClient.Services
{
    public class CacheService
    {
        // 单例全局访问
        public static CacheService Instance { get; } = new CacheService();
        private CacheService() { }

        // Json序列化配置
        private readonly JsonSerializerOptions _jsonOpt = new JsonSerializerOptions
        {
            WriteIndented = true,
            AllowTrailingCommas = true
        };

        #region 文件路径定义
        // 全部会话列表文件
        private string SessionJsonPath => Path.Combine(FileHelper.CacheDir, "sessions.json");

        // 获取单个好友聊天记录完整路径
        private string GetSingleChatPath(long friendId)
        {
            string chatFolder = Path.Combine(FileHelper.CacheDir, "ChatRecords");
            FileHelper.CreateIfMissing(chatFolder);
            return Path.Combine(chatFolder, $"chat_{friendId}.json");
        }
        #endregion

        #region 会话列表管理（左侧聊天会话）
        /// <summary>保存全量会话列表</summary>
        public void SaveAllSessions(List<SessionModel> sessionList)
        {
            string json = JsonSerializer.Serialize(sessionList, _jsonOpt);
            FileHelper.WriteAllText(SessionJsonPath, json);
        }

        /// <summary>读取全部本地会话</summary>
        public List<SessionModel> GetAllSessions()
        {
            if (!FileHelper.FileExists(SessionJsonPath))
                return new List<SessionModel>();

            try
            {
                string jsonText = FileHelper.ReadAllText(SessionJsonPath);
                return JsonSerializer.Deserialize<List<SessionModel>>(jsonText, _jsonOpt) ?? new List<SessionModel>();
            }
            catch (Exception ex)
            {
                LogHelper.Error("读取会话缓存失败", ex);
                return new List<SessionModel>();
            }
        }

        /// <summary>更新单个会话，新消息自动置顶</summary>
        public void UpdateSingleSession(SessionModel targetSession)
        {
            var list = GetAllSessions();
            // 移除旧会话
            list.RemoveAll(item => item.FriendId == targetSession.FriendId);
            // 插入到最顶部
            list.Insert(0, targetSession);
            SaveAllSessions(list);
        }

        /// <summary>删除指定好友会话</summary>
        public void DeleteSession(long friendId)
        {
            var list = GetAllSessions();
            list.RemoveAll(x => x.FriendId == friendId);
            SaveAllSessions(list);
        }
        #endregion

        #region 聊天记录增删查改
        /// <summary>保存一条消息，自动更新会话最后消息</summary>
        public void AppendChatMessage(long friendId, ChatMessage message)
        {
            var history = GetFriendChatHistory(friendId);
            history.Add(message);

            // 写入本地聊天文件
            string json = JsonSerializer.Serialize(history, _jsonOpt);
            FileHelper.WriteAllText(GetSingleChatPath(friendId), json);

            // 同步更新会话面板展示
            UpdateSingleSession(new SessionModel
            {
                FriendId = friendId,
                FriendName = message.IsSelfSend ? string.Empty : message.SenderName,
                LastMessage = message.Content,
                LastMsgTime = message.SendTime
            });
        }

        /// <summary>读取指定好友全部历史聊天</summary>
        public List<ChatMessage> GetFriendChatHistory(long friendId)
        {
            string path = GetSingleChatPath(friendId);
            if (!FileHelper.FileExists(path))
                return new List<ChatMessage>();

            try
            {
                string json = FileHelper.ReadAllText(path);
                return JsonSerializer.Deserialize<List<ChatMessage>>(json, _jsonOpt) ?? new List<ChatMessage>();
            }
            catch
            {
                return new List<ChatMessage>();
            }
        }

        /// <summary>清空单个好友全部聊天记录+会话</summary>
        public void ClearFriendChatCache(long friendId)
        {
            FileHelper.DeleteFile(GetSingleChatPath(friendId));
            DeleteSession(friendId);
        }
        #endregion

        #region 缓存一键清理（设置页面调用）
        /// <summary>清理所有临时缓存：聊天记录、会话，Config目录好友/账号完全保留</summary>
        public void ClearAllTempCache()
        {
            FileHelper.ClearCacheFolder();
            LogHelper.Info("临时缓存清理完成，好友、登录配置未删除");
        }
        #endregion
    }
}
