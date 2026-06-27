using System;
using System.IO;
using System.Text;
using ValleyClient.Enums;

namespace ValleyClient.Helpers
{
    public static class LogHelper
    {

        private static string _logRootPath = string.Empty;
        private static string _todayLogFile = string.Empty;
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;

            FileHelper.CreateIfMissing(FileHelper.LogsDir);

            var localDataPath = Path.Combine(
            AppContext.BaseDirectory,
            "valley",
            "logs");

            _logRootPath = localDataPath;

            if (!Directory.Exists(_logRootPath))
            {
                Directory.CreateDirectory(_logRootPath);
            }
            string datastr = DateTime.Now.ToString("yyyy-MM-dd");
            _todayLogFile = Path.Combine(_logRootPath, $"log_{datastr}.txt");
            _initialized = true;
            Info($"日志系统初始化完成，日志文件路径：{_todayLogFile}");
        }

        private static void WriteLog(LogLevel level, string message, Exception ex = null)
        {
            if (!_initialized) return;

            StringBuilder sb = new StringBuilder();
            sb.Append($"[{DateTime.Now:HH:mm:ss}] [{level}] ");
            sb.Append(message);

            if (ex != null)
            {
                sb.AppendLine();
                sb.Append($"异常信息：{ex.Message}");
                sb.AppendLine();
                sb.Append($"堆栈：{ex.StackTrace}");
            }

            string logText = sb.ToString();

            Console.WriteLine(logText);

            try
            {
                using var fs = new FileStream(_todayLogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var sw = new StreamWriter(fs, Encoding.UTF8);
                sw.WriteLine(logText);
            }
            catch
            {
            }
        }

        public static void Debug(string msg) => WriteLog(LogLevel.Debug, msg);
        public static void Info(string msg) => WriteLog(LogLevel.Info, msg);
        public static void Warn(string msg) => WriteLog(LogLevel.Warn, msg);
        public static void Error(string msg, Exception ex = null) => WriteLog(LogLevel.Error, msg, ex);
    }
}
