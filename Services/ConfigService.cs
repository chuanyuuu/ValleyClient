using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ValleyClient.Helpers;
using ValleyClient.Models;

namespace ValleyClient.Services
{
    public class ConfigService
    {
        public static ConfigService Instance { get; } = new();
        private ConfigService() { }

        private readonly JsonSerializerOptions _jsonOpt = new() { WriteIndented = true };

        // 好友文件路径
        private string FriendFilePath => Path.Combine(FileHelper.ConfigDir, "friends.json");
        // 登录账号、Token
        private string LoginCredentialPath => Path.Combine(FileHelper.ConfigDir, "login_cred.json");
        // 软件全局设置
        private string AppSettingPath => Path.Combine(FileHelper.ConfigDir, "app_setting.json");

        #region 好友CRUD
        // 保存全量好友
        public void SaveFriendList(List<UserModel> friends)
        {
            string json = JsonSerializer.Serialize(friends, _jsonOpt);
            FileHelper.WriteAllText(FriendFilePath, json);
            LogHelper.Debug("好友列表已写入永久配置");
        }

        // 读取好友
        public List<UserModel> GetFriendList()
        {
            if (!FileHelper.FileExists(FriendFilePath))
                return new List<UserModel>();
            try
            {
                string json = FileHelper.ReadAllText(FriendFilePath);
                return JsonSerializer.Deserialize<List<UserModel>>(json, _jsonOpt) ?? new();
            }
            catch (Exception ex)
            {
                LogHelper.Error("读取好友配置失败", ex);
                return new();
            }
        }

        // 新增/更新单个好友
        public void AddOrUpdateFriend(UserModel friend)
        {
            var list = GetFriendList();
            list.RemoveAll(x => x.Id == friend.Id);
            list.Add(friend);
            SaveFriendList(list);
        }

        // 删除好友（同时清理该好友聊天缓存）
        public void DeleteFriend(long friendId)
        {
            var list = GetFriendList();
            list.RemoveAll(x => x.Id == friendId);
            SaveFriendList(list);
            // 同步清除该好友聊天记录、会话
            CacheService.Instance.ClearFriendChatCache(friendId);
        }
        #endregion

        // 登录凭证、软件配置方法省略，只放永久数据
        #region 登录凭证（记住账号、Token）
        /// <summary>保存登录账号与身份令牌</summary>
        public void SaveLoginCredential(string account, string token)
        {
            var model = new LoginCredentialModel
            {
                Account = account,
                Token = token
            };
            string json = JsonSerializer.Serialize(model, _jsonOpt);
            FileHelper.WriteAllText(LoginCredentialPath, json);
        }

        /// <summary>读取本地保存的登录信息</summary>
        public LoginCredentialModel GetLoginCredential()
        {
            if (!FileHelper.FileExists(LoginCredentialPath))
                return new LoginCredentialModel();
            try
            {
                string json = FileHelper.ReadAllText(LoginCredentialPath);
                return JsonSerializer.Deserialize<LoginCredentialModel>(json, _jsonOpt) ?? new();
            }
            catch
            {
                return new LoginCredentialModel();
            }
        }

        /// <summary>清空登录凭证（退出登录调用）</summary>
        public void ClearLoginCredential()
        {
            if (FileHelper.FileExists(LoginCredentialPath))
                FileHelper.DeleteFile(LoginCredentialPath);
        }
        #endregion

        #region 软件全局配置
        /// <summary>读取软件设置，无文件返回默认配置</summary>
        public AppSettingModel GetAppSetting()
        {
            if (!FileHelper.FileExists(AppSettingPath))
                return new AppSettingModel();
            try
            {
                string json = FileHelper.ReadAllText(AppSettingPath);
                return JsonSerializer.Deserialize<AppSettingModel>(json, _jsonOpt) ?? new();
            }
            catch
            {
                return new AppSettingModel();
            }
        }

        /// <summary>保存修改后的软件配置</summary>
        public void SaveAppSetting(AppSettingModel setting)
        {
            string json = JsonSerializer.Serialize(setting, _jsonOpt);
            FileHelper.WriteAllText(AppSettingPath, json);
        }
        #endregion
    }
}
