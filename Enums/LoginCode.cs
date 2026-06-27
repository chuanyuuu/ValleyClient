namespace ValleyClient.Enums
{
    public enum LoginCode
    {
        Success = 0,
        AccountNotExist = 1,
        PasswordWrong = 2,
        TokenExpired = 3,
        ServerMaintain = 4,
        VersionTooLow = 5,
        AccountBan = 6
    }
}
