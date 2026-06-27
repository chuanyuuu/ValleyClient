using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValleyClient.Models
{
    public class AppSettingModel
    {
        // 是否开启消息提示音
        public bool EnableMsgSound { get; set; } = true;
        // 麦克风默认静音
        public bool AutoMuteMic { get; set; } = false;
    }
}
