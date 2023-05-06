using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YYHEggEgg.Logger.Utils;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
namespace YYHEggEgg.Logger
{
    public enum LogLevel
    {
        // Some of details here refer to: https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel
        /// <summary>
        /// Logs that contain the most detailed messages. These messages may contain sensitive application data. These messages are disabled by default and should never be enabled in a production environment.
        /// </summary>
        Verbose = -1,
        /// <summary>
        /// Logs that are used for interactive investigation during development. These logs should primarily contain information useful for debugging and have no long-term value.
        /// </summary>
        Debug = 0,
        /// <summary>
        /// Logs that are used for interactive investigation during development. These logs should primarily contain information useful for debugging and have no long-term value.
        /// </summary>
        Information = 1,
        /// <summary>
        /// Logs that highlight an abnormal or unexpected event in the application flow, but do not otherwise cause the application execution to stop.
        /// </summary>
        Warning = 2,
        /// <summary>
        /// Logs that highlight an abnormal or unexpected event in the application flow, but do not otherwise cause the application execution to stop.
        /// </summary>
        Error = 3,
        // Fatal = 4,
        /// <summary>
        /// Not used for writing log messages. Specifies that a logging category should not write any messages.
        /// </summary>
        None = 5
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

        public static string _logPath_debug;
        public static string LogPath_debug => _logPath_debug;
        private static StreamWriter logwriter_debug;

        /// <summary>
        /// Initialize the logger. If initialized before, the method will return immediately.
        /// </summary>
        public static void Initialize(LoggerConfig conf)
        {
            if (_initialized) return;

            _initialized = true;

            _customConfig = conf;
            if (conf.Use_Console_Wrapper) ConsoleWrapper.Initialize();
            RefreshLogTicks = 100;
            string? dir = null;
            if (conf.Use_Working_Directory) 
                dir = Environment.CurrentDirectory;
            else 
            {
                // If using dotnet .dll to launch,
                // Environment.ProgramPath will return the path of dotnet.exe
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                dir = Path.GetDirectoryName(assemblyPath);
                #region Fallback 
                if (dir == null)
                {
                    dir = Environment.CurrentDirectory;
                    // qlog will be handled when BackgroundUpdate started, so it's a safe invoke
                    Warn($"Assembly directory isn't accessible so fallback to Working directory ({dir})!", "Logger");
                }
                #endregion
            }
            Directory.CreateDirectory($"{dir}/logs");

            #region Handle Past Log
            if (File.Exists($"{dir}/logs/latest.log"))
            {
                FileInfo info = new($"{dir}/logs/latest.log");
                File.Move($"{dir}/logs/latest.log", $"{dir}/logs/{info.LastWriteTime:yyyy-MM-dd_HH-mm-ss}.log");
            }
            if (File.Exists($"{dir}/logs/latest.debug.log"))
            {
                FileInfo info = new($"{dir}/logs/latest.debug.log");
                File.Move($"{dir}/logs/latest.debug.log", $"{dir}/logs/{info.LastWriteTime:yyyy-MM-dd_HH-mm-ss}.debug.log");
            }
            #endregion

            #region Compress Past Logs
            List<string> logs = new();
            string belongDate = "";
            foreach (var logfile in Directory.EnumerateFiles($"{dir}/logs"))
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
                            $"{dir}/logs/log.{belongDate}.zip");
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
                            if (_customConfig.Global_Minimum_LogLevel <= LogLevel.Debug) Console.WriteLine(ex);
                        }
#pragma warning restore CS0168 // 声明了变量，但从未使用过
                    }
                    belongDate = thisdate;
                }
                #endregion
                logs.Add(logfile);
            }
            #endregion

            _logPath = $"{dir}/logs/latest.log";
            logwriter = new(_logPath, true);
            logwriter.AutoFlush = true;
            if (conf.Global_Minimum_LogLevel <= LogLevel.Debug)
            {
                _logPath_debug = $"{dir}/logs/latest.debug.log";
                logwriter_debug = new(_logPath_debug, true);
            }
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
        /// Put a log with Verbose Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Verbose"/>. Notice that messages put here will only be written to <c>latest.debug.log</c>.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Verb(string content, string? sender = null)
        {
            AssertInitialized();
            if (_customConfig.Global_Minimum_LogLevel == LogLevel.Verbose)
            {
                qlog.Enqueue(new LogDetail(content, LogLevel.Verbose, sender));
            }
        }

        /// <summary>
        /// Put a log with Debug Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Debug"/>. Notice that messages put here will only be written to <c>latest.debug.log</c>.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Dbug(string content, string? sender = null)
        {
            AssertInitialized();
            if (_customConfig.Global_Minimum_LogLevel <= LogLevel.Debug)
            {
                qlog.Enqueue(new LogDetail(content, LogLevel.Debug, sender));
            }
        }

        /// <summary>
        /// Put a log with Info Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Information"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Info(string content, string? sender = null)
        {
            AssertInitialized();
            if (_customConfig.Global_Minimum_LogLevel <= LogLevel.Information)
            {
                qlog.Enqueue(new LogDetail(content, LogLevel.Information, sender));
            }
        }

        /// <summary>
        /// Put a log with Warning Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Warning"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Warn(string content, string? sender = null)
        {
            AssertInitialized();
            if (_customConfig.Global_Minimum_LogLevel <= LogLevel.Warning)
            {
                qlog.Enqueue(new LogDetail(content, LogLevel.Warning, sender));
            }
        }

        /// <summary>
        /// Put a log with Error Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Error"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Erro(string content, string? sender = null)
        {
            AssertInitialized();
            if (_customConfig.Global_Minimum_LogLevel <= LogLevel.Error)
            {
                qlog.Enqueue(new LogDetail(content, LogLevel.Error, sender));
            }
        }

        #region Background Refresh
        private struct LogDetail 
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
        /// <summary>
        /// It should refer to milliseconds not ticks, but it won't change since it has been published.
        /// </summary>
        public static int RefreshLogTicks { get; set; }
        private static Stopwatch watch = new();
        // see https://learn.microsoft.com/zh-cn/dotnet/api/system.threading.tasks.task.delay?view=net-7.0#system-threading-tasks-task-delay(system-timespan)
        private static readonly TimeSpan waitStandard = TimeSpan.FromMilliseconds(15);

        private static async Task BackgroundUpdate()
        {
            if (!qlog.TryDequeue(out LogDetail _log))
            {
                await Task.Delay(RefreshLogTicks);
                await Task.Run(BackgroundUpdate);
                return;
            }

            watch.Start();

            if (_customConfig.Use_Console_Wrapper)
            {
                string _consoleOutput = WriteLog(_log);
                if (_log.level >= _customConfig.Console_Minimum_LogLevel)
                {
                    ConsoleWrapper.WriteLine(_consoleOutput);
                }
                while (qlog.TryDequeue(out LogDetail log))
                {
                    string consoleOutput = WriteLog(log);
                    if (log.level >= _customConfig.Console_Minimum_LogLevel)
                    {
                        ConsoleWrapper.WriteLine(consoleOutput);
                    }
                }
            }
            else
            {
                string _consoleOutput = WriteLog(_log);
                if (_log.level >= _customConfig.Console_Minimum_LogLevel)
                {
                    WriteColorLine(_consoleOutput);
                }
                while (qlog.TryDequeue(out LogDetail log))
                {
                    string consoleOutput = WriteLog(log);
                    if (log.level >= _customConfig.Console_Minimum_LogLevel)
                    {
                        WriteColorLine(consoleOutput);
                    }
                }
            }

            watch.Stop();
            TimeSpan relaxSpan = TimeSpan.FromMilliseconds(RefreshLogTicks) - watch.Elapsed;
            if (relaxSpan >= waitStandard) await Task.Delay(relaxSpan);
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
            string nowtime = log.create_time.ToString("HH:mm:ss");
            string header = GetLogInfo(log.level, log.sender);
            string res = $"{nowtime}{header}{log.content}";
            if (Tools.TryRemoveColorInfo(res, out string fileres))
            {
                if (_customConfig.Global_Minimum_LogLevel <= LogLevel.Debug)
                {
                    if (log.level >= LogLevel.Information)
                        logwriter.WriteLine(fileres);
                    logwriter_debug.WriteLine(fileres);
                }
                else
                {
                    logwriter.WriteLine(fileres);
                }
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
                case LogLevel.Verbose:
                    rtn += "<color=Magenta>Verb</color>";
                    break;
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
