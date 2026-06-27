using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ValleyClient.Enums;
using ValleyClient.Helpers;
using ValleyClient.Models;

namespace ValleyClient.Services
{
    public class NetworkService
    {
        public static NetworkService Instance { get; } = new();
        private NetworkService() { }

        private Task? _heartbeatLoopTask;
        private CancellationTokenSource? _heartbeatCts;
        private const int HeartbeatIntervalMs = 3000;
        private const int ReconnectDelayMs = 5000;
        // 默认本地服务端地址
        private const string DefaultServerIp = "127.0.0.1";
        private const int DefaultServerPort = 8080;

        public NetworkState CurrentNetworkState { get; private set; } = NetworkState.Disconnected;

        /// <summary>开启心跳循环，登录成功后调用</summary>
        public void StartHeartbeatLoop()
        {
            if (_heartbeatCts is not null) return;

            _heartbeatCts = new CancellationTokenSource();
            var token = _heartbeatCts.Token;

            _heartbeatLoopTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (CurrentNetworkState == NetworkState.Connected)
                        {
                            // SendHeartbeatPacket 是异步方法，必须 await
                            await NativeInteropService.Instance.SendHeartbeatPacket();
                            LogHelper.Debug("心跳包发送完成");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error("心跳发送异常，触发断线重连", ex);
                        await TriggerReconnect();
                    }

                    await Task.Delay(HeartbeatIntervalMs, token);
                }
            }, token);
        }

        /// <summary>停止心跳，登出/关闭程序调用</summary>
        public void StopHeartbeatLoop()
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts?.Dispose();
            _heartbeatCts = null;
            _heartbeatLoopTask = null;
        }

        /// <summary>主动触发断线重连流程</summary>
        private async Task TriggerReconnect()
        {
            StopHeartbeatLoop();
            CurrentNetworkState = NetworkState.Disconnected;
            SetNetworkState(CurrentNetworkState);

            await Task.Delay(ReconnectDelayMs);
            LogHelper.Info("开始执行断线重连");

            // 调用带默认参数的ConnectServer无参重载
            bool connectSuccess = await NativeInteropService.Instance.ConnectServer(DefaultServerIp, DefaultServerPort);
            if (connectSuccess)
            {
                CurrentNetworkState = NetworkState.Connected;
                SetNetworkState(CurrentNetworkState);
                StartHeartbeatLoop();
                LogHelper.Info("重连服务器成功，恢复心跳");
            }
            else
            {
                CurrentNetworkState = NetworkState.ReconnectFail;
                SetNetworkState(CurrentNetworkState);
                LogHelper.Warn("重连服务器失败，等待下一次重试");
            }
        }

        /// <summary>发送聊天消息封装</summary>
        public async Task SendChatMessage(long targetFriendId, ChatMessage message)
        {
            if (CurrentNetworkState != NetworkState.Connected)
            {
                LogHelper.Warn("发送消息失败：未连接服务器");
                return;
            }
            await NativeInteropService.Instance.SendChatMessagePacket(targetFriendId, message);
        }

        /// <summary>拉取服务端好友列表</summary>
        public async Task<List<UserModel>> RequestServerFriendList()
        {
            if (CurrentNetworkState != NetworkState.Connected)
                return new List<UserModel>();
            return await NativeInteropService.Instance.RequestFriendListPacket();
        }

        /// <summary>更新全局网络状态并推送事件</summary>
        public void SetNetworkState(NetworkState state)
        {
            CurrentNetworkState = state;
            NativeInteropService.Instance.OnNetworkStateUpdate?.Invoke(state);
        }
    }
}