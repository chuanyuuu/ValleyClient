using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ValleyClient.Enums;
using ValleyClient.Helpers;
using ValleyClient.Models;

namespace ValleyClient.Services
{
    public class NativeInteropService
    {
        #region 单例
        public static NativeInteropService Instance { get; } = new();
        private NativeInteropService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNameCaseInsensitive = true
            };
        }
        #endregion

        #region 配置与私有字段
        private readonly JsonSerializerOptions _jsonOptions;
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _receiveLoopCts;
        private Task? _receiveTask;
        private const int BufferSize = 4096;
        private string _serverUrl = string.Empty;
        #endregion

        #region 服务端推送回调事件（全部定义齐全）
        /// <summary>收到私聊消息推送</summary>
        public Action<ChatMessage>? OnReceivePrivateChatMsg;
        /// <summary>好友在线状态变更</summary>
        public Action<long, OnlineStatus>? OnFriendOnlineStatusChanged;

        /// <summary>网络状态变更 旧名称（修复：存在定义）</summary>
        public Action<NetworkState>? OnNetworkStateUpdate;
        /// <summary>网络状态变更 新名称</summary>
        public Action<NetworkState>? OnGlobalNetworkStateChanged;

        /// <summary>登录结果返回</summary>
        public Action<LoginResultDto>? OnLoginResultCallback;
        /// <summary>服务端返回好友列表数据</summary>
        public Action<List<UserModel>>? OnFriendListResponse;
        #endregion

        #region 私有工具：同时触发新旧网络状态事件
        private void InvokeNetworkStateEvent(NetworkState state)
        {
            OnGlobalNetworkStateChanged?.Invoke(state);
            OnNetworkStateUpdate?.Invoke(state);
        }
        #endregion

        #region 连接网关（ConnectServer 别名，参数齐全）
        /// <summary>兼容旧名 ConnectServer</summary>
        public async Task<bool> ConnectServer(string ip, int port)
        {
            return await ConnectGateway(ip, port);
        }

        public async Task<bool> ConnectGateway(string ip, int port)
        {
            try
            {
                DisconnectGateway();
                _serverUrl = $"ws://{ip}:{port}";
                _webSocket = new ClientWebSocket();

                InvokeNetworkStateEvent(NetworkState.Connecting);
                await _webSocket.ConnectAsync(new Uri(_serverUrl), CancellationToken.None);
                InvokeNetworkStateEvent(NetworkState.Connected);

                _receiveLoopCts = new CancellationTokenSource();
                _receiveTask = ReceiveMessageLoopAsync(_receiveLoopCts.Token);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error("连接服务端失败", ex);
                InvokeNetworkStateEvent(NetworkState.Disconnected);
                return false;
            }
        }

        /// <summary>主动断开连接</summary>
        public void DisconnectGateway()
        {
            try
            {
                _receiveLoopCts?.Cancel();
                _receiveLoopCts?.Dispose();
                _receiveLoopCts = null;

                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    _ = _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "客户端主动退出", CancellationToken.None);
                }
                _webSocket?.Dispose();
                _webSocket = null;
                _receiveTask = null;

                InvokeNetworkStateEvent(NetworkState.Disconnected);
            }
            catch (Exception ex)
            {
                LogHelper.Error("断开连接异常", ex);
            }
        }
        #endregion

        #region 接收循环 & 包分发
        private async Task ReceiveMessageLoopAsync(CancellationToken token)
        {
            var buffer = new byte[BufferSize];
            while (!token.IsCancellationRequested && _webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        DisconnectGateway();
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        DispatchServerPacket(json);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogHelper.Error("接收服务端消息异常", ex);
                    InvokeNetworkStateEvent(NetworkState.ReconnectFail);
                    break;
                }
            }
        }

        private void DispatchServerPacket(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("PacketType", out var typeEl) || !root.TryGetProperty("Data", out var dataEl))
                    return;

                string packetType = typeEl.GetString() ?? string.Empty;
                string dataJson = dataEl.GetRawText();

                switch (packetType)
                {
                    case "LoginResult":
                        var loginResult = JsonSerializer.Deserialize<LoginResultDto>(dataJson, _jsonOptions);
                        if (loginResult != null) OnLoginResultCallback?.Invoke(loginResult);
                        break;
                    case "PrivateChatMsg":
                        var chatMsg = JsonSerializer.Deserialize<ChatMessage>(dataJson, _jsonOptions);
                        if (chatMsg != null) OnReceivePrivateChatMsg?.Invoke(chatMsg);
                        break;
                    case "UserOnlineState":
                        var stateObj = JsonSerializer.Deserialize<Dictionary<string, long>>(dataJson, _jsonOptions);
                        long uid = stateObj["UserId"];
                        OnlineStatus status = (OnlineStatus)stateObj["Status"];
                        OnFriendOnlineStatusChanged?.Invoke(uid, status);
                        break;
                    case "FriendList":
                        var friendList = JsonSerializer.Deserialize<List<UserModel>>(dataJson, _jsonOptions);
                        if (friendList != null) OnFriendListResponse?.Invoke(friendList);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("解析服务端数据包失败", ex);
            }
        }
        #endregion

        #region 底层通用发包
        private async Task SendPacketAsync(string packetType, object data)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                LogHelper.Warn("发包失败：未连接服务端");
                return;
            }

            var packet = new { PacketType = packetType, Data = data };
            string json = JsonSerializer.Serialize(packet, _jsonOptions);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            await _webSocket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None);
        }

        /// <summary>发送房间音频帧数据包</summary>
        public async Task SendRoomAudioFrame(long roomId, byte[] frameData)
        {
            await SendPacketAsync("RoomAudioFrame", new
            {
                RoomId = roomId,
                FrameData = frameData
            });
        }
        #endregion

        #region 对外业务接口（补齐所有兼容别名，解决 SendHeartbeatPacket 报错）
        /// <summary>账号密码登录</summary>
        public async Task RequestLoginByAccount(string account, string password)
        {
            await SendPacketAsync("LoginAccount", new { Account = account, Password = password });
        }

        /// <summary>Token登录</summary>
        public async Task RequestLoginByToken(string token)
        {
            await SendPacketAsync("LoginToken", new { Token = token });
        }

        /// <summary>心跳 标准方法</summary>
        public async Task SendHeartbeat()
        {
            await SendPacketAsync("Heartbeat", new { });
        }
        /// <summary>兼容旧调用名 SendHeartbeatPacket（修复找不到定义报错）</summary>
        public async Task SendHeartbeatPacket()
        {
            await SendHeartbeat();
        }

        /// <summary>发送私聊消息 标准</summary>
        public async Task SendPrivateChatMsg(long targetFriendId, ChatMessage msg)
        {
            await SendPacketAsync("SendPrivateMsg", new { TargetId = targetFriendId, Message = msg });
        }
        /// <summary>兼容旧名 SendChatMessagePacket</summary>
        public async Task SendChatMessagePacket(long targetFriendId, ChatMessage msg)
        {
            await SendPrivateChatMsg(targetFriendId, msg);
        }

        /// <summary>请求好友列表 标准</summary>
        public async Task<List<UserModel>> RequestFriendList()
        {
            await SendPacketAsync("GetFriendList", new { });
            // 该方法仅发送请求，数据通过 OnFriendListResponse 回调返回，上层不要用返回值接收列表
            return new List<UserModel>();
        }
        /// <summary>兼容旧名 RequestFriendListPacket</summary>
        public async Task<List<UserModel>> RequestFriendListPacket()
        {
            return await RequestFriendList();
        }

        #region 房间相关
        public async Task RequestAllRoomList()
        {
            await SendPacketAsync("GetAllRoomList", new { });
        }
        public async Task RequestJoinRoom(long roomId)
        {
            await SendPacketAsync("JoinRoom", new { RoomId = roomId });
        }
        public async Task RequestLeaveRoom(long roomId)
        {
            await SendPacketAsync("LeaveRoom", new { RoomId = roomId });
        }
        public async Task RequestUpdateMicState(long roomId, RoomMemberAudioState state)
        {
            await SendPacketAsync("UpdateMicState", new { RoomId = roomId, State = state });
        }
        #endregion
        #endregion
    }
}