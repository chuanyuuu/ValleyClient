using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValleyClient.Enums;

namespace ValleyClient.Models
{
    public class RoomMemberModel
    {
        public long UserId { get; set; }
        public string NickName { get; set; } = string.Empty;
        public RoomMemberAudioState AudioState { get; set; }
    }
}
