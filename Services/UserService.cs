using ValleyClient.Enums;
using ValleyClient.Models;

namespace ValleyClient.Services
{
    public class UserService
    {
        public static UserService Instance { get; } = new();
        private UserService() { }

        // 当前登录用户缓存
        private UserModel? _currentUser;

        /// <summary>设置当前登录用户（登录成功调用）</summary>
        public void SetCurrentUser(UserModel user)
        {
            _currentUser = user;
        }

        /// <summary>获取当前登录用户，未登录返回null</summary>
        public UserModel? GetCurrentUser()
        {
            return _currentUser;
        }

        /// <summary>获取用户ID，未登录返回0</summary>
        public long GetCurrentUserId()
        {
            return _currentUser?.Id ?? 0;
        }

        /// <summary>获取用户昵称，未登录返回空字符串</summary>
        public string GetCurrentUserName()
        {
            return _currentUser?.NickName ?? string.Empty;
        }

        /// <summary>更新用户在线状态</summary>
        public void UpdateUserOnlineState(OnlineStatus status)
        {
            if (_currentUser != null)
            {
                _currentUser.OnlineState = status;
            }
        }

        /// <summary>清空当前用户（退出登录调用）</summary>
        public void ClearCurrentUser()
        {
            _currentUser = null;
        }
    }
}
