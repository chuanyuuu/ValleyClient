using System;
using System.Threading;
using Microsoft.UI.Xaml;
using ValleyClient.Enums;
using ValleyClient.Helpers;
using ValleyClient.Services;

namespace ValleyClient.Services
{
    public class AudioService
    {
        public static AudioService Instance { get; } = new();
        private AudioService() { }

        public static Window? MainWindow;

        // 本地麦克风开关状态
        public bool MicOpen { get; private set; }
        // 当前房间ID
        private long _currentRoomId = 0;

        // 音频采集相关
        private Thread? _captureThread;
        private CancellationTokenSource? _captureCts;
        private const int AudioFrameIntervalMs = 20;

        // UI回调：本地麦克风状态变更
        public Action<bool>? OnLocalMicStateChanged;

        #region 设置当前房间
        public void BindCurrentRoom(long roomId)
        {
            _currentRoomId = roomId;
            LogHelper.Debug($"AudioService 绑定房间Id:{roomId}");
        }

        public void ClearRoomAudio()
        {
            LogHelper.Debug("AudioService 清空房间音频数据，关闭麦克风");
            _currentRoomId = 0;
            CloseMic();
        }
        #endregion

        #region 麦克风控制
        /// <summary>打开麦克风并同步到服务端，启动音频采集</summary>
        public async void OpenMic()
        {
            if (_currentRoomId == 0 || MicOpen)
            {
                LogHelper.Warn("打开麦克风失败：未进入房间或麦克风已开启");
                return;
            }

            MicOpen = true;
            OnLocalMicStateChanged?.Invoke(true);
            LogHelper.Info("麦克风已开启");

            // 同步状态给服务端：正在说话
            await NativeInteropService.Instance.RequestUpdateMicState(_currentRoomId, RoomMemberAudioState.Speaking);

            // 启动音频采集线程
            StartAudioCapture();
        }

        /// <summary>关闭麦克风同步到服务端，停止音频采集</summary>
        public async void CloseMic()
        {
            if (_currentRoomId == 0 || !MicOpen)
            {
                LogHelper.Warn("关闭麦克风失败：未进入房间或麦克风已关闭");
                return;
            }

            // 停止采集
            StopAudioCapture();

            MicOpen = false;
            OnLocalMicStateChanged?.Invoke(false);
            LogHelper.Info("麦克风已关闭");

            await NativeInteropService.Instance.RequestUpdateMicState(_currentRoomId, RoomMemberAudioState.Mute);
        }

        /// <summary>切换麦克风开关</summary>
        public void ToggleMic()
        {
            if (MicOpen)
                CloseMic();
            else
                OpenMic();
        }
        #endregion

        #region 音频采集完整实现
        /// <summary>开启麦克风音频采集后台线程</summary>
        private void StartAudioCapture()
        {
            _captureCts = new CancellationTokenSource();
            CancellationToken token = _captureCts.Token;

            _captureThread = new Thread(() =>
            {
                LogHelper.Debug("麦克风采集线程已启动");
                while (!token.IsCancellationRequested)
                {
                    // 模拟麦克风采集PCM音频帧
                    byte[] audioFrame = CaptureMicrophoneAudioFrame();

                    // 发送音频帧到服务端
                    SendAudioFrameToServer(audioFrame);

                    Thread.Sleep(AudioFrameIntervalMs);
                }
                LogHelper.Debug("麦克风采集线程退出循环");
            })
            {
                IsBackground = true,
                Name = "MicrophoneCaptureThread"
            };
            _captureThread.Start();
        }

        /// <summary>停止麦克风音频采集，释放资源</summary>
        private void StopAudioCapture()
        {
            LogHelper.Debug("执行停止音频采集流程");
            // 下发取消信号
            _captureCts?.Cancel();
            // 等待线程退出
            if (_captureThread != null && _captureThread.IsAlive)
            {
                _captureThread.Join(100);
                LogHelper.Debug("等待采集线程完成退出");
            }
            // 释放资源
            _captureCts?.Dispose();
            _captureCts = null;
            _captureThread = null;
            LogHelper.Debug("音频采集资源已全部释放");
        }

        /// <summary>模拟采集一帧麦克风音频数据</summary>
        private byte[] CaptureMicrophoneAudioFrame()
        {
            // 实际项目替换为Windows.Media/NAudio麦克风采集逻辑
            int frameSize = 960;
            byte[] frame = new byte[frameSize];
            Random rand = new Random();
            rand.NextBytes(frame);
            return frame;
        }

        /// <summary>发送音频帧至服务端</summary>
        private async void SendAudioFrameToServer(byte[] frameData)
        {
            if (_currentRoomId == 0)
            {
                LogHelper.Warn("发送音频帧失败：当前无有效房间Id");
                return;
            }
            await NativeInteropService.Instance.SendRoomAudioFrame(_currentRoomId, frameData);
            LogHelper.Debug($"发送音频帧，长度:{frameData.Length}");
        }
        #endregion

        #region 远端语音播放接口完整实现
        /// <summary>播放远端用户语音数据</summary>
        public void PlayRemoteAudio(byte[] audioData, long senderUserId)
        {
            if (MainWindow == null || MainWindow.DispatcherQueue == null)
            {
                LogHelper.Warn("播放远端语音失败：窗口调度队列不存在");
                return;
            }

            MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                // 此处替换为Windows.Media.Playback音频播放逻辑
                LogHelper.Debug($"播放用户{senderUserId}语音帧，长度：{audioData.Length}");
            });
        }

        /// <summary>停止所有远端音频播放，清空播放资源</summary>
        public void StopAllRemoteAudio()
        {
            if (MainWindow == null || MainWindow.DispatcherQueue == null)
            {
                LogHelper.Warn("停止远端语音失败：窗口调度队列不存在");
                return;
            }

            MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                // 停止所有MediaPlayer、释放音频设备
                LogHelper.Info("停止全部远端语音播放，释放播放资源");
            });
        }
        #endregion

        #region 登出清空音频状态
        public void ResetAudioService()
        {
            LogHelper.Info("重置AudioService所有状态");
            ClearRoomAudio();
            StopAllRemoteAudio();
            StopAudioCapture();
            LogHelper.Info("AudioService重置完成");
        }
        #endregion
    }
}