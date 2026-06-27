using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValleyClient.Models
{
    public class RoomModel
    {
        public long RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public long OwnerId { get; set; }
        public List<RoomMemberModel> Members { get; set; } = new();
    }
}
