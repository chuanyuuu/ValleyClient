using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValleyClient.Enums;

namespace ValleyClient.Models
{
    public class LoginResultDto
    {
        /// <summary>登录结果状态码</summary>
        public LoginCode Code { get; set; }

        /// <summary>身份凭证Token，登录成功下发</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>当前登录用户信息，成功才有值</summary>
        public UserModel? LoginUser { get; set; }
    }
}
