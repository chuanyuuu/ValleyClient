using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValleyClient.Enums;

namespace ValleyClient.Models
{
    public class UserModel
    {
        public long Id { get; set; }

        public string NickName { get; set; } = string.Empty;

        public string AvatarText { get; set; } = string.Empty;
        public string AvatarUri { get; set; } = string.Empty;

        public OnlineStatus OnlineState { get; set; } = OnlineStatus.Offline;

        public string Signature { get; set; } = string.Empty;

        public DateTime AddFriendTime { get; set; }
    }
}
