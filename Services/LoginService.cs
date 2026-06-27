using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using ValleyClient.Enums;
using ValleyClient.Helpers;
using ValleyClient.Models;
using ValleyClient.Services;

namespace ValleyClient.Services
{
    public class LoginService
    {
        public static LoginService Instance { get; } = new();
        private LoginService()
        {
            NativeInteropService.Instance.OnLoginResultCallback += HandleLoginResult;
        }

        public bool IsLogined { get; private set; }
        private string _loginAccount = string.Empty;
        private string _currentToken = string.Empty;

        // 外部赋值主窗口
        public static Window? MainWindow;

        #region 登录入口
        /// <summary>账号密码登录</summary>
        public async void LoginByAccount(string account, string password)
        {
            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
            {
                LogHelper.Warn("账号或密码不能为空，取消登录请求");
                return;
            }
            LogHelper.Info($"发起账号登录，账号：{account}");
            await NativeInteropService.Instance.RequestLoginByAccount(account, password);
        }

        /// <summary>本地Token自动登录</summary>
        public async Task<bool> AutoLoginByToken()
        {
            var credential = ConfigService.Instance.GetLoginCredential();
            if (string.IsNullOrEmpty(credential.Token))
            {
                LogHelper.Debug("本地无保存Token，自动登录失败");
                return false;
            }

            LogHelper.Info("使用本地保存Token发起自动登录");
            _currentToken = credential.Token;
            await NativeInteropService.Instance.RequestLoginByToken(_currentToken);
            return true;
        }
        #endregion

        #region 登录结果回调处理
        private void HandleLoginResult(LoginResultDto dto)
        {
            if (MainWindow is null)
            {
                LogHelper.Warn("主窗口实例为空，无法处理登录回调");
                return;
            }
            var dispatcher = MainWindow.DispatcherQueue;
            if (dispatcher is null)
            {
                LogHelper.Warn("窗口调度队列不存在，跳过登录结果处理");
                return;
            }

            dispatcher.TryEnqueue(async () =>
            {
                if (dto.Code == LoginCode.Success && dto.LoginUser != null)
                {
                    IsLogined = true;
                    _loginAccount = dto.LoginUser.Id.ToString();
                    _currentToken = dto.Token;
                    long userId = dto.LoginUser.Id;

                    LogHelper.Info($"登录成功，用户ID：{userId}，昵称：{dto.LoginUser.NickName}");

                    // 1. 保存当前登录用户到用户服务
                    UserService.Instance.SetCurrentUser(dto.LoginUser);
                    // 2. 持久化账号与Token到本地配置
                    ConfigService.Instance.SaveLoginCredential(_loginAccount, _currentToken);
                    // 3. SQLite聊天存储绑定当前用户，隔离多账号聊天记录
                    SqliteMessageStorage.Instance.SetLoginUser(userId);

                    // 4. 网络状态切换并启动心跳
                    NetworkService.Instance.SetNetworkState(NetworkState.Connected);
                    NetworkService.Instance.StartHeartbeatLoop();

                    // 5. 请求拉取好友列表
                    await NetworkService.Instance.RequestServerFriendList();
                    LogHelper.Debug("登录流程全部执行完毕");
                }
                else
                {
                    IsLogined = false;
                    LogHelper.Warn($"登录失败，错误码：{dto.Code}");

                    // 登录失败清空本地凭证与用户缓存
                    ConfigService.Instance.ClearLoginCredential();
                    UserService.Instance.ClearCurrentUser();
                }
            });
        }
        #endregion

        #region 退出登录
        public void Logout()
        {
            LogHelper.Info("执行登出全流程");
            IsLogined = false;
            _currentToken = string.Empty;
            _loginAccount = string.Empty;

            // 停止心跳、断开网关连接
            NetworkService.Instance.StopHeartbeatLoop();
            NativeInteropService.Instance.DisconnectGateway();

            // 清空全局业务缓存
            UserService.Instance.ClearCurrentUser();
            MessageService.Instance.ClearAllMessageCache();
            RoomService.Instance.ClearAllRoomData();
            ConfigService.Instance.ClearLoginCredential();

            LogHelper.Info("登出完成，所有本地缓存已清空");
        }
        #endregion
    }
}