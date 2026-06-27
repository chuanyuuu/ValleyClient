using System;
using System.IO;

namespace ValleyClient.Helpers
{
    public static class FileHelper
    {
        private static readonly string BaseDir = AppContext.BaseDirectory;

        public static string Root => Path.Combine(BaseDir, "valley");
        public static string LogsDir => Path.Combine(Root, "logs");
        public static string ConfigDir => Path.Combine(Root, "config");
        public static string CacheDir => Path.Combine(Root, "cache");

        public static void CreateAllDirs()
        {
            CreateIfMissing(Root);
            CreateIfMissing(LogsDir);
            CreateIfMissing(ConfigDir);
            CreateIfMissing(CacheDir);
        }


        public static void CreateIfMissing(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    Console.WriteLine($"已创建目录：{path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"目录创建失败 {path} | {ex.Message}");
            }
        }

        public static bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        public static void WriteAllText(string filePath, string content)
        {
            try
            {
                File.WriteAllText(filePath, content);
            }
            catch (Exception ex)
            {
                LogHelper.Error($"写入文件失败 {filePath}", ex);
            }
        }

        public static string ReadAllText(string filePath, string defaultReturn = "")
        {
            try
            {
                if (!FileExists(filePath))
                    return defaultReturn;
                return File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                LogHelper.Error($"读取文件失败 {filePath}", ex);
                return defaultReturn;
            }
        }

        public static void ClearCacheFolder()
        {
            try
            {
                var dir = new DirectoryInfo(CacheDir);
                foreach (var file in dir.GetFiles())
                    file.Delete();
                foreach (var folder in dir.GetDirectories())
                    folder.Delete(true);
                LogHelper.Info("缓存目录已清空");
            }
            catch (Exception ex)
            {
                LogHelper.Error("清空缓存失败", ex);
            }
        }

        public static void DeleteFile(string path)
        {
            try
            {
                if (FileExists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                LogHelper.Error($"删除文件失败 {path}", ex);
            }
        }
    }
}
