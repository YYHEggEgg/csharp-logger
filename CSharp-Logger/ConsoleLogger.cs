using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YYHEggEgg.Logger.Utils;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
namespace YYHEggEgg.Logger
{
    public enum LogLevel
    {
        Debug = 0,
        Information = 1,
        Warning = 2,
        Error = 3
    }

    public static class Log
    {
        private static LoggerConfig _customConfig;
        public static LoggerConfig CustomConfig => _customConfig;

        #region Initialize
        private static bool _initialized = false;

        public static string _logPath;
        public static string LogPath => _logPath;
        private static StreamWriter logwriter;
#if DEBUG
        public static string _logPath_debug;
        public static string LogPath_debug => _logPath_debug;
        private static StreamWriter logwriter_debug;
#endif
        /// <summary>
        /// This method has SIDE EFFECT, so don't use <see cref="Console"/> after invoked it.
        /// <para/> If initialized before, the method will return immediately.
        /// </summary>
        public static void Initialize(LoggerConfig conf)
        {
            if (_initialized) return;

            _initialized = true;

            _customConfig = conf;
            RefreshLogTicks = 100;
            string dir = Environment.CurrentDirectory;
            Directory.CreateDirectory("logs");

            #region Handle Past Log
            if (File.Exists("logs/latest.log"))
            {
                FileInfo info = new("logs/latest.log");
                File.Move("logs/latest.log", $"logs/{info.LastWriteTime:yyyy-MM-dd_HH-mm-ss}.log");
            }
            if (File.Exists("logs/latest.debug.log"))
            {
                FileInfo info = new("logs/latest.debug.log");
                File.Move("logs/latest.debug.log", $"logs/{info.LastWriteTime:yyyy-MM-dd_HH-mm-ss}.debug.log");
            }
            #endregion

            #region Compress Past Logs
            List<string> logs = new();
            string belongDate = "";
            foreach (var logfile in Directory.EnumerateFiles("logs"))
            {
                // Get Date
                FileInfo loginfo = new(logfile);
                string thisdate;
                if (loginfo.Extension != ".log" || loginfo.Name.Length < 10
                    || !DateTime.TryParse(thisdate = loginfo.Name.Substring(0, 10), out _))
                {
                    continue;
                }
                #region auto compress logs
                if (thisdate != belongDate)
                {
                    if (belongDate != "")
                    {
                        string newzipfile = Tools.AddNumberedSuffixToPath(
                            $"logs/log.{belongDate}.zip");
                        Tools.CompressFiles(newzipfile, logs);
                        // Delete original files
#pragma warning disable CS0168 // 声明了变量，但从未使用过
                        try
                        {
                            foreach (var log in logs)
                            {
                                File.Delete(log);
                            }
                            logs = new();
                        }
                        catch (Exception ex)
                        {
#if DEBUG
                            Console.WriteLine(ex);
#endif
                        }
#pragma warning restore CS0168 // 声明了变量，但从未使用过
                    }
                    belongDate = thisdate;
                }
                #endregion
                logs.Add(logfile);
            }
            #endregion

            _logPath = "logs/latest.log";
            logwriter = new(_logPath, true);
            logwriter.AutoFlush = true;
#if DEBUG
            _logPath_debug = "logs/latest.debug.log";
            logwriter_debug = new(_logPath_debug, true);
#endif
            Task.Run(BackgroundUpdate);
        }

        private static void AssertInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException("Logger hasn't been initialized!");
        }
        #endregion

        #region Logger
        /// <summary>
        /// Put a log with Debug Level to the handle queue. Only output when built with DEBUG.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Dbug(string content, string? sender = null)
        {
            AssertInitialized();
#if DEBUG
            qlog.Enqueue(new LogDetail(content, LogLevel.Debug, sender));
#endif
        }

        /// <summary>
        /// Put a log with Info Level to the handle queue.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Info(string content, string? sender = null)
        {
            AssertInitialized();
            qlog.Enqueue(new LogDetail(content, LogLevel.Information, sender));
        }

        /// <summary>
        /// Put a log with Warning Level to the handle queue.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Warn(string content, string? sender = null)
        {
            AssertInitialized();
            qlog.Enqueue(new LogDetail(content, LogLevel.Warning, sender));
        }

        /// <summary>
        /// Put a log with Error Level to the handle queue.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Erro(string content, string? sender = null)
        {
            AssertInitialized();
            qlog.Enqueue(new LogDetail(content, LogLevel.Error, sender));
        }

        #region Background Refresh
        public struct LogDetail 
        {
            public LogLevel level;
            public string content;
            public string? sender;
            public DateTime create_time;

            public LogDetail(string con, LogLevel lvl, string? snd)
            {
                level = lvl;
                content = con;
                sender = snd;
                create_time = DateTime.Now;
            }
        }

        private static ConcurrentQueue<LogDetail> qlog = new();
        public static int RefreshLogTicks { get; set; }

        private static async Task BackgroundUpdate()
        {
            if (!qlog.TryDequeue(out LogDetail _log))
            {
                await Task.Delay(RefreshLogTicks);
                await Task.Run(BackgroundUpdate);
                return;
            }

            if (_customConfig.Use_Console_Wrapper)
            {
                List<string> logs = new(qlog.Count + 1);
                logs.Add(WriteLog(_log));
                while (qlog.TryDequeue(out LogDetail log))
                {
                    logs.Add(WriteLog(log));
                }
                ConsoleWrapper.WriteLine(logs);
            }
            else
            {
                WriteColorLine(WriteLog(_log));
                while (qlog.TryDequeue(out LogDetail log))
                {
                    WriteColorLine(WriteLog(log));
                }
            }

            await Task.Delay(RefreshLogTicks);
            await Task.Run(BackgroundUpdate);
        }
        #endregion

        /// <summary>
        /// Not responsible for Console operations but only write lines to file.
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        private static string WriteLog(LogDetail log)
        {
            Debug.Assert(_customConfig.Use_Console_Wrapper);
            string nowtime = log.create_time.ToString("HH:mm:ss");
            string header = GetLogInfo(log.level, log.sender);
            string res = $"{nowtime}{header}{log.content}";
            if (Tools.TryRemoveColorInfo(res, out string fileres))
            {
#if DEBUG
                if (log.level != LogLevel.Debug)
#endif
                    logwriter.WriteLine(fileres);
#if DEBUG
                logwriter_debug.WriteLine(fileres);
#endif
            }
            else
            {
                logwriter.WriteLine($"{nowtime}{header}{GetLogInfo(LogLevel.Error, "Logger")}" +
                    $"Content has caused exception when resolving color, ex={fileres}; content={res};");
            }

            if (_customConfig.Max_Output_Char_Count < 0 || log.content.Length < _customConfig.Max_Output_Char_Count)
                return res;
            else
                return $"{nowtime}{header}[content too long (> Maximum line length of {_customConfig.Max_Output_Char_Count}), so not output to console]";
        }

        /// <summary>
        /// Write info <[level]:[sender]> like <Info:KCP>. 
        /// </summary>
        private static string GetLogInfo(LogLevel level, string? sender)
        {
            string rtn = " <";
            switch (level)
            {
                case LogLevel.Debug:
                    rtn += "<color=Cyan>Dbug</color>";
                    break;
                case LogLevel.Information:
                    rtn += "<color=Blue>Info</color>";
                    break;
                case LogLevel.Warning:
                    rtn += "<color=Yellow>Warn</color>";
                    break;
                case LogLevel.Error:
                    rtn += "<color=Red>Erro</color>";
                    break;
            }
            if (sender != null)
            {
                rtn += $":{sender}> ";
            }
            else
            {
                rtn += $"> ";
            }
            return rtn;
        }

        #region Console implement
        private static void WriteColorLine(string input)
        {
            try
            {
                // Generated by ChatGPT
                Console.ForegroundColor = ConsoleColor.White; //设置默认白色字体颜色
                int startIndex = 0;

                while (true)
                {
                    int colorStart = input.IndexOf("<color=", startIndex); //查找下一个彩色文字的起始位置
                    if (colorStart == -1) //若未找到，输出剩余部分并退出循环
                    {
                        Console.Write(input.Substring(startIndex, input.Length - startIndex));
                        break;
                    }

                    int colorEnd = input.IndexOf(">", colorStart); //查找彩色文字的结束位置
                    if (colorEnd == -1) //若未找到，输出剩余部分并退出循环
                    {
                        Console.Write(input.Substring(startIndex, input.Length - startIndex));
                        break;
                    }

                    string colorCode = input.Substring(colorStart + 7, colorEnd - colorStart - 7); //提取颜色代码

                    if (Enum.TryParse(colorCode, out ConsoleColor color)) //尝试将字符串颜色代码解析为ConsoleColor枚举类型
                    {
                        Console.Write(input.Substring(startIndex, colorStart - startIndex)); //输出彩色文字前的部分
                        Console.ForegroundColor = color; //设置字体颜色
                        int textStart = colorEnd + 1;
                        int textEnd = input.IndexOf("</color>", textStart); //查找彩色文字结束标记
                        if (textEnd == -1) //若未找到，输出剩余部分并退出循环
                        {
                            Console.Write(input.Substring(textStart, input.Length - textStart));
                            break;
                        }
                        Console.Write(input.Substring(textStart, textEnd - textStart)); //输出彩色文字
                        Console.ForegroundColor = ConsoleColor.White; //恢复默认字体颜色
                        startIndex = textEnd + 8; //继续查找下一个彩色文字的起始位置
                    }
                    else //解析失败，跳过此次查找
                    {
                        startIndex = colorEnd + 1;
                    }
                }
                Console.WriteLine();
            }
            catch
            {
                Erro("Fatal Error. Search for \"resolving color\" in log file for more infomation.", "Logger");
            }
        }
        #endregion
        #endregion
    }
}
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
