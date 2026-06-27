using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;
using ValleyClient.Enums;
using ValleyClient.Models;

namespace ValleyClient.Services
{
    public class RoomService
    {
        public static RoomService Instance { get; } = new();
        private RoomService() { }

        // 当前所在房间
        private RoomModel? _currentRoom;
        // 全部房间列表缓存
        private readonly List<RoomModel> _roomList = new();

        #region 房间列表管理
        /// <summary>服务端返回房间全量列表，覆盖本地缓存</summary>
        public void RefreshAllRoomList(List<RoomModel> rooms)
        {
            _roomList.Clear();
            _roomList.AddRange(rooms);
        }

        /// <summary>获取全部房间</summary>
        public List<RoomModel> GetAllRoomList()
        {
            return _roomList.ToList();
        }

        /// <summary>根据房间Id查找房间</summary>
        public RoomModel? GetRoomById(long roomId)
        {
            return _roomList.FirstOrDefault(r => r.RoomId == roomId);
        }
        #endregion

        #region 当前房间操作
        /// <summary>进入房间后设置为当前房间</summary>
        public void SetCurrentRoom(RoomModel room)
        {
            _currentRoom = room;
        }

        /// <summary>获取当前所在房间，未进房返回null</summary>
        public RoomModel? GetCurrentRoom()
        {
            return _currentRoom;
        }

        /// <summary>退出房间，清空当前房间缓存</summary>
        public void LeaveCurrentRoom()
        {
            _currentRoom = null;
        }

        /// <summary>获取当前房间Id，不在房间返回0</summary>
        public long GetCurrentRoomId()
        {
            return _currentRoom?.RoomId ?? 0;
        }
        #endregion

        #region 房间成员管理
        /// <summary>刷新当前房间全部成员</summary>
        public void RefreshRoomMembers(List<RoomMemberModel> members)
        {
            if (_currentRoom == null) return;
            _currentRoom.Members.Clear();
            _currentRoom.Members.AddRange(members);
        }

        /// <summary>更新单个成员麦克风状态</summary>
        public void UpdateMemberAudioState(long userId, RoomMemberAudioState state)
        {
            if (_currentRoom == null) return;
            var member = _currentRoom.Members.FirstOrDefault(m => m.UserId == userId);
            if (member != null)
            {
                member.AudioState = state;
            }
        }

        /// <summary>成员加入房间</summary>
        public void AddRoomMember(RoomMemberModel member)
        {
            if (_currentRoom == null) return;
            var exist = _currentRoom.Members.FirstOrDefault(m => m.UserId == member.UserId);
            if (exist != null)
                _currentRoom.Members.Remove(exist);
            _currentRoom.Members.Add(member);
        }

        /// <summary>成员离开房间</summary>
        public void RemoveRoomMember(long userId)
        {
            if (_currentRoom == null) return;
            _currentRoom.Members.RemoveAll(m => m.UserId == userId);
        }

        /// <summary>获取当前房间所有成员</summary>
        public List<RoomMemberModel> GetCurrentRoomMembers()
        {
            return _currentRoom?.Members.ToList() ?? new List<RoomMemberModel>();
        }
        #endregion

        #region 网络请求封装（调用NativeInterop发包）
        /// <summary>请求服务端全房间列表</summary>
        public async void RequestAllRoomList()
        {
            await NativeInteropService.Instance.RequestAllRoomList();
        }

        /// <summary>请求加入指定房间</summary>
        public async void RequestJoinRoom(long roomId)
        {
            await NativeInteropService.Instance.RequestJoinRoom(roomId);
        }

        /// <summary>请求退出当前房间</summary>
        public async void RequestLeaveRoom()
        {
            long rid = GetCurrentRoomId();
            if (rid == 0) return;
            await NativeInteropService.Instance.RequestLeaveRoom(rid);
            LeaveCurrentRoom();
        }

        /// <summary>发送麦克风状态变更包</summary>
        public async void RequestChangeMicState(RoomMemberAudioState state)
        {
            long rid = GetCurrentRoomId();
            if (rid == 0) return;
            await NativeInteropService.Instance.RequestUpdateMicState(rid, state);
        }
        #endregion

        #region 清空全部房间缓存（登出时调用）
        public void ClearAllRoomData()
        {
            _currentRoom = null;
            _roomList.Clear();
        }
        #endregion
    }
}
