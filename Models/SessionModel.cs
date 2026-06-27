using System;

namespace ValleyClient.Models
{
    public class SessionModel
    {
        public long FriendId { get; set; }

        public string FriendName { get; set; } = string.Empty;

        public string LastMessage { get; set; } = string.Empty;

        public DateTime LastMsgTime { get; set; }

        public int UnReadCount { get; set; } = 0;
    }
}
