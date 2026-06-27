using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using ValleyClient.Models;
using ValleyClient.Enums;
using ValleyClient.Helpers;

namespace ValleyClient.Services
{
    public class SqliteMessageStorage
    {
        public static SqliteMessageStorage Instance { get; } = new();
        private readonly string _dbPath;
        private string _currentSelfUserId = "0";

        private SqliteMessageStorage()
        {
            // 本地数据库文件路径：程序目录/Cache/ChatMessage.db
            string cacheDir = Path.Combine(AppContext.BaseDirectory, "Cache");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            _dbPath = Path.Combine(cacheDir, "ChatMessage.db");
            // 程序启动自动建表
            CreateTableIfNotExist();
        }

        /// <summary>切换登录用户，隔离不同账号聊天记录</summary>
        public void SetLoginUser(long userId)
        {
            _currentSelfUserId = userId.ToString();
        }

        #region 初始化建表
        private void CreateTableIfNotExist()
        {
            using var conn = GetConnection();
            conn.Open();
            string createSql = @"
CREATE TABLE IF NOT EXISTS chat_message (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    msg_id TEXT NOT NULL UNIQUE,
    self_user_id TEXT NOT NULL,
    target_friend_id TEXT NOT NULL,
    sender_id TEXT NOT NULL,
    sender_name TEXT NOT NULL DEFAULT '',
    content TEXT NOT NULL,
    msg_type INTEGER NOT NULL DEFAULT 0,
    is_self_send INTEGER NOT NULL DEFAULT 0,
    send_time TEXT NOT NULL,
    read_state INTEGER NOT NULL DEFAULT 0, -- 0未读 1已读
    create_at TEXT DEFAULT CURRENT_TIMESTAMP,
    update_at TEXT DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_self_target ON chat_message(self_user_id, target_friend_id);
CREATE INDEX IF NOT EXISTS idx_send_time ON chat_message(send_time);
";
            using var cmd = new SqliteCommand(createSql, conn);
            cmd.ExecuteNonQuery();
            LogHelper.Debug("SQLite 聊天记录表初始化完成");
        }
        #endregion

        #region 获取数据库连接
        private SqliteConnection GetConnection()
        {
            return new SqliteConnection($"Data Source={_dbPath}");
        }
        #endregion

        #region 插入单条消息
        public void InsertMessage(ChatMessage msg)
        {
            if (_currentSelfUserId == "0")
            {
                LogHelper.Warn("未登录用户，不保存聊天记录");
                return;
            }
            using var conn = GetConnection();
            conn.Open();
            string insertSql = @"
INSERT OR IGNORE INTO chat_message
(msg_id, self_user_id, target_friend_id, sender_id, sender_name, content, msg_type, is_self_send, send_time, read_state)
VALUES
(@MsgId, @SelfId, @TargetId, @SenderId, @SenderName, @Content, @MsgType, @IsSelf, @SendTime, @ReadState);
";
            using var cmd = new SqliteCommand(insertSql, conn);
            cmd.Parameters.AddWithValue("@MsgId", msg.MsgId.ToString());
            cmd.Parameters.AddWithValue("@SelfId", _currentSelfUserId);
            cmd.Parameters.AddWithValue("@TargetId", msg.TargetFriendId.ToString());
            cmd.Parameters.AddWithValue("@SenderId", msg.SenderId.ToString());
            cmd.Parameters.AddWithValue("@SenderName", msg.SenderName);
            cmd.Parameters.AddWithValue("@Content", msg.Content);
            cmd.Parameters.AddWithValue("@MsgType", (int)msg.MsgType);
            cmd.Parameters.AddWithValue("@IsSelf", msg.IsSelfSend ? 1 : 0);
            cmd.Parameters.AddWithValue("@SendTime", msg.SendTime.ToString("yyyy-MM-dd HH:mm:ss"));
            // 自己发的消息默认已读，对方消息默认未读
            cmd.Parameters.AddWithValue("@ReadState", msg.IsSelfSend ? 1 : 0);

            cmd.ExecuteNonQuery();
        }
        #endregion

        #region 分页获取好友聊天记录（时间正序，旧消息在前）
        public List<ChatMessage> GetFriendMessageHistory(long friendId, int pageSize = 50, int pageIndex = 0)
        {
            var list = new List<ChatMessage>();
            using var conn = GetConnection();
            conn.Open();
            string querySql = @"
SELECT * FROM chat_message
WHERE self_user_id = @SelfId AND target_friend_id = @TargetId
ORDER BY send_time ASC
LIMIT @PageSize OFFSET @Offset;
";
            using var cmd = new SqliteCommand(querySql, conn);
            cmd.Parameters.AddWithValue("@SelfId", _currentSelfUserId);
            cmd.Parameters.AddWithValue("@TargetId", friendId.ToString());
            cmd.Parameters.AddWithValue("@PageSize", pageSize);
            cmd.Parameters.AddWithValue("@Offset", pageIndex * pageSize);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var model = new ChatMessage
                {
                    MsgId = long.Parse(reader["msg_id"].ToString()),
                    SenderId = long.Parse(reader["sender_id"].ToString()),
                    SenderName = reader["sender_name"].ToString(),
                    TargetFriendId = long.Parse(reader["target_friend_id"].ToString()),
                    Content = reader["content"].ToString(),
                    MsgType = (MessageType)Convert.ToInt32(reader["msg_type"]),
                    IsSelfSend = Convert.ToInt32(reader["is_self_send"]) == 1,
                    SendTime = DateTime.Parse(reader["send_time"].ToString())
                };
                list.Add(model);
            }
            return list;
        }
        #endregion

        #region 标记该好友所有消息为已读
        public void MarkAllRead(long friendId)
        {
            using var conn = GetConnection();
            conn.Open();
            string updateSql = @"
UPDATE chat_message
SET read_state = 1
WHERE self_user_id = @SelfId AND target_friend_id = @TargetId AND read_state = 0;
";
            using var cmd = new SqliteCommand(updateSql, conn);
            cmd.Parameters.AddWithValue("@SelfId", _currentSelfUserId);
            cmd.Parameters.AddWithValue("@TargetId", friendId.ToString());
            cmd.ExecuteNonQuery();
        }
        #endregion

        #region 获取好友未读消息数量
        public int GetUnreadCount(long friendId)
        {
            using var conn = GetConnection();
            conn.Open();
            string countSql = @"
SELECT COUNT(1) FROM chat_message
WHERE self_user_id = @SelfId AND target_friend_id = @TargetId AND read_state = 0;
";
            using var cmd = new SqliteCommand(countSql, conn);
            cmd.Parameters.AddWithValue("@SelfId", _currentSelfUserId);
            cmd.Parameters.AddWithValue("@TargetId", friendId.ToString());
            var res = cmd.ExecuteScalar();
            return Convert.ToInt32(res);
        }
        #endregion

        #region 清空单个好友本地聊天记录
        public void ClearFriendMessage(long friendId)
        {
            using var conn = GetConnection();
            conn.Open();
            string delSql = @"
DELETE FROM chat_message
WHERE self_user_id = @SelfId AND target_friend_id = @TargetId;
";
            using var cmd = new SqliteCommand(delSql, conn);
            cmd.Parameters.AddWithValue("@SelfId", _currentSelfUserId);
            cmd.Parameters.AddWithValue("@TargetId", friendId.ToString());
            cmd.ExecuteNonQuery();
            LogHelper.Debug($"已清空好友{friendId}本地聊天记录");
        }
        #endregion

        #region 清空当前登录账号全部聊天记录（登出调用）
        public void ClearAllSelfMessage()
        {
            using var conn = GetConnection();
            conn.Open();
            string delSql = "DELETE FROM chat_message WHERE self_user_id = @SelfId;";
            using var cmd = new SqliteCommand(delSql, conn);
            cmd.Parameters.AddWithValue("@SelfId", _currentSelfUserId);
            cmd.ExecuteNonQuery();
            LogHelper.Debug("已清空当前账号全部本地聊天记录");
        }
        #endregion
    }
}