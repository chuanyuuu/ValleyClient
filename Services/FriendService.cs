using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using ValleyClient.Enums;
using ValleyClient.Models;
using ValleyClient.Services;

namespace ValleyClient.Services
{
    public class FriendService
    {
        public static FriendService Instance { get; } = new();
        private FriendService()
        {
            // 注册网络推送回调
            NativeInteropService.Instance.OnFriendListResponse += OnReceiveFriendList;
            NativeInteropService.Instance.OnFriendOnlineStatusChanged += OnFriendStatusUpdate;
        }

        // 全局主窗口，用于子线程切UI更新
        public static Window? MainWindow;

        // 全部好友缓存
        private readonly List<UserModel> _friendList = new();

        // UI订阅事件：好友列表刷新 / 单个好友状态变更
        public Action? OnFriendListRefreshed;
        public Action<long, OnlineStatus>? OnFriendOnlineStateChanged;

        #region 获取好友数据
        /// <summary>获取全部好友副本</summary>
        public List<UserModel> GetAllFriends()
        {
            return _friendList.ToList();
        }

        /// <summary>根据用户Id查找好友</summary>
        public UserModel? GetFriendById(long userId)
        {
            return _friendList.FirstOrDefault(u => u.Id == userId);
        }

        /// <summary>判断该Id是否是好友</summary>
        public bool IsFriend(long userId)
        {
            return _friendList.Any(u => u.Id == userId);
        }
        #endregion

        #region 网络请求：拉取好友列表
        public async void RequestRefreshFriendList()
        {
            if (NetworkService.Instance.CurrentNetworkState != NetworkState.Connected)
                return;
            await NetworkService.Instance.RequestServerFriendList();
        }
        #endregion

        #region 服务端推送处理
        /// <summary>接收完整好友列表，覆盖本地缓存</summary>
        private void OnReceiveFriendList(List<UserModel> friends)
        {
            if (MainWindow == null || MainWindow.DispatcherQueue == null)
                return;

            MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                _friendList.Clear();
                _friendList.AddRange(friends);
                OnFriendListRefreshed?.Invoke();
            });
        }

        /// <summary>单个好友上下线状态更新</summary>
        private void OnFriendStatusUpdate(long friendId, OnlineStatus status)
        {
            if (MainWindow == null || MainWindow.DispatcherQueue == null)
                return;

            MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                var friend = GetFriendById(friendId);
                if (friend != null)
                {
                    friend.OnlineState = status;
                    OnFriendOnlineStateChanged?.Invoke(friendId, status);
                }
            });
        }
        #endregion

        #region 登出清空缓存
        public void ClearAllFriendData()
        {
            _friendList.Clear();
        }
        #endregion
    }
}