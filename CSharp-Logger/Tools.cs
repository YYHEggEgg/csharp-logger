﻿using System.IO.Compression;
using System.Reflection;

namespace YYHEggEgg.Logger.Utils
{
    // Code below are all Generated by ChatGPT.
    internal class Tools
    {
        /// <summary>
        /// Compress <paramref name="files"/> into <paramref name="zipFilePath"/>. 
        /// </summary>
        public static void CompressFiles(string zipFilePath, IEnumerable<string> files)
        {
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }

            using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    archive.CreateEntryFromFile(file, Path.GetFileName(file));
                }
            }
        }

        public static void CompressFiles(string zipFilePath, params string[] files)
            => CompressFiles(zipFilePath, files);

        /// <summary>
        /// Can be applied to both file and directory. Generate suffix like (1), (2) for the <paramref name="path"/> when the file/directory already exists.
        /// </summary>
        public static string AddNumberedSuffixToPath(string filePath)
        {
            /* 该方法首先检查给定路径是否已存在。
             * 如果是文件路径，则将文件名分离为文件名和扩展名，并在文件名后添加一个括号附加编号，直到找到可用的文件名。
             * 如果是目录路径，则附加数字后缀到目录名直到找到可用的目录名。
             * 例如，如果传入的参数是"C:\Users\Example\Desktop\test.txt"，
             * 如果该路径已经存在，则返回"C:\Users\Example\Desktop\test (1).txt"。 
             * 
             * 如果参数是"C:\Users\Example\Desktop\test"，
             * 如果该路径已经存在，则返回"C:\Users\Example\Desktop\test (1)"。 
             * 如果路径不存在，则返回原始路径。
             */
            if (File.Exists(filePath))
            {
                string directory = Path.GetDirectoryName(filePath) ?? "";
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string extension = Path.GetExtension(filePath);
                string newFilePath = filePath;
                int suffix = 1;

                while (File.Exists(newFilePath))
                {
                    newFilePath = Path.Combine(directory, string.Format("{0} ({1}){2}", fileName, suffix, extension));
                    suffix++;
                }

                return newFilePath;
            }
            else if (Directory.Exists(filePath))
            {
                string directoryName = Path.GetDirectoryName(filePath) ?? "";
                string directory = Path.Combine(directoryName, Path.GetFileName(filePath));
                string newDirectory = directory;
                int suffix = 1;

                while (Directory.Exists(newDirectory))
                {
                    newDirectory = Path.Combine(directoryName, string.Format("{0} ({1})", Path.GetFileName(filePath), suffix));
                    suffix++;
                }

                return newDirectory;
            }
            else
            {
                return filePath;
            }
        }

        /// <summary>
        /// 检查 <see cref="Log.GlobalConfig"/> 与 <see cref="conf"/> 间
        /// <see cref="LoggerConfig.Use_Working_Directory"/> 的一致性，
        /// 并返回日志工作目录。
        /// </summary>
        /// <param name="conf"></param>
        /// <returns></returns>
        public static string GetLoggerWorkingDir(LoggerConfig conf)
        {
            string? rtndir;
            if (conf.Use_Working_Directory)
                rtndir = Environment.CurrentDirectory;
            else
            {
                // If using dotnet .dll to launch,
                // Environment.ProgramPath will return the path of dotnet.exe
                string assemblyPath = AppContext.BaseDirectory;
                rtndir = Path.GetDirectoryName(assemblyPath);
                #region Fallback
                if (rtndir == null)
                {
                    rtndir = Environment.CurrentDirectory;
                }
                #endregion
            }
            if (conf.Use_Working_Directory != Log.GlobalConfig.Use_Working_Directory)
                throw new InvalidOperationException("To ensure the consistency of log filestream," +
                    "the whole program may only use the same log directory.");
            _logworkdir ??= rtndir;
            return _logworkdir;
        }
        private static string? _logworkdir = null;

        /// <summary>
        /// Check if running on one of Windows, macOS or Linux.
        /// </summary>
        public static bool CheckIfSupportedOS() =>
            OperatingSystem.IsWindows() || 
            OperatingSystem.IsMacOS() ||
            OperatingSystem.IsLinux();
    }
}
