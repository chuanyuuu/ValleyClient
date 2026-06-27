using System;
using ValleyClient.Enums;

namespace ValleyClient.Models;

/// <summary>私聊消息实体，客户端与服务端通信统一数据包</summary>
public class ChatMessage
{
    /// <summary>消息唯一标识（本地使用时间戳Ticks，服务端下发使用服务端自增Id）</summary>
    public long MsgId { get; set; }

    /// <summary>发送者用户Id</summary>
    public long SenderId { get; set; }

    /// <summary>发送者昵称</summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>接收好友Id</summary>
    public long TargetFriendId { get; set; }

    /// <summary>消息文本内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>消息类型：文本/图片/语音/系统通知</summary>
    public MessageType MsgType { get; set; }

    /// <summary>消息发送时间</summary>
    public DateTime SendTime { get; set; }

    /// <summary>是否为当前登录用户自己发送的消息</summary>
    public bool IsSelfSend { get; set; }
}