using System.Collections.Concurrent;
using System.Diagnostics;
using YYHEggEgg.Logger.Utils;

namespace YYHEggEgg.Logger
{
    public class BaseLogger
    {
        #region Background Console
        private static ConcurrentQueue<ColorLineResult> qconsole_strings = new();
        private static ConcurrentBag<BaseLogger> working_loggers = new();
        static BaseLogger()
        {
            AppDomain.CurrentDomain.ProcessExit += GlobalClearup;
            Task.Run(BackgroundWriteConsole);
        }

        private static bool _global_loggers_ending = false;
        private static bool _console_ending = false;
        private static bool _console_cleared_up = false;
        /// <summary>
        /// Console.Write will pend as the user selected content in the console. But the file I/O shouldn't be pended.
        /// </summary>
        /// <returns></returns>
        private static async Task BackgroundWriteConsole()
        {
            while (true)
            {
                if (qconsole_strings.IsEmpty)
                {
                    if (_console_ending)
                    {
                        _console_cleared_up = true;
                        return;
                    }
                    await Task.Delay(50);
                    continue;
                }

                bool UseConsoleWrapper = Log.GlobalConfig.Use_Console_Wrapper;

                while (qconsole_strings.TryDequeue(out var color_str))
                {
                    if (UseConsoleWrapper) ConsoleWrapper.WriteLine(color_str);
                    else color_str.WriteToConsole();
                }
            }
        }
        #endregion

        #region Global Clear Up
        private static int clearup_finished_count = 0;
        private static object clearup_finished_count_lck = "4.0 coming!";
        internal static bool _clearup_completed => clearup_finished_count >= working_loggers.Count;

        private static void GlobalClearup(object? o, EventArgs e)
        {
            _global_loggers_ending = true;
            int total = 0;
            while ((!_clearup_completed) && total <= 1000)
            {
                Thread.Sleep(50);
                total += 50;
            }
            while ((Log.GlobalConfig.Use_Console_Wrapper &&
                !ConsoleWrapper._clearup_completed && (total <= 1500))
                || (!Log.GlobalConfig.Use_Console_Wrapper &&
                    !_console_cleared_up && (total <= 1500)))
            {
                Thread.Sleep(50);
                total += 50;
            }
        }
        #endregion

        public readonly LoggerConfig CustomConfig;
        private readonly ConcurrentBag<LogFileStream> writeTargetFiles = new();
        private readonly string WorkDirectory;

        #region Initialize
        /// <summary>
        /// Initialize the logger. If initialized before, the method will return immediately.
        /// </summary>
        internal BaseLogger(LoggerConfig conf)
        {
            CustomConfig = conf;
            if (conf.Use_Console_Wrapper) ConsoleWrapper.Initialize();
            RefreshLogMilliseconds = 100;
            WorkDirectory = Tools.GetLoggerWorkingDir(conf);

            Task.Run(BackgroundUpdate);
        }

        #region .ctors
        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1)
            : this(conf)
        {
            AddLogFile(newFileStreamConf1);
        }
        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1, LogFileConfig newFileStreamConf2)
            : this(conf)
        {
            AddLogFile(newFileStreamConf1);
            AddLogFile(newFileStreamConf2);
        }
        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1, LogFileConfig newFileStreamConf2, LogFileConfig newFileStreamConf3)
            : this(conf)
        {
            AddLogFile(newFileStreamConf1);
            AddLogFile(newFileStreamConf2);
            AddLogFile(newFileStreamConf3);
        }
        public BaseLogger(LoggerConfig conf, IEnumerable<LogFileConfig> newFileStreamConfs_enumerable)
            : this(conf)
        {
            foreach (var newFileStreamConf in newFileStreamConfs_enumerable)
                AddLogFile(newFileStreamConf);
        }
        public BaseLogger(LoggerConfig conf, params LogFileConfig[] newFileStreamConfs)
            : this(conf, newFileStreamConfs_enumerable: newFileStreamConfs) { }

        public BaseLogger(LoggerConfig conf, string fileStreamName1)
            : this(conf)
        {
            AddLogFile(fileStreamName1);
        }
        public BaseLogger(LoggerConfig conf, string fileStreamName1, string fileStreamName2)
            : this(conf)
        {
            AddLogFile(fileStreamName1);
            AddLogFile(fileStreamName2);
        }
        public BaseLogger(LoggerConfig conf, string fileStreamName1, string fileStreamName2, string fileStreamName3)
            : this(conf)
        {
            AddLogFile(fileStreamName1);
            AddLogFile(fileStreamName2);
            AddLogFile(fileStreamName3);
        }
        public BaseLogger(LoggerConfig conf, IEnumerable<string> fileStreamNames_enumerable)
            : this(conf)
        {
            foreach (var fileStreamName in fileStreamNames_enumerable)
                AddLogFile(fileStreamName);
        }
        public BaseLogger(LoggerConfig conf, params string[] fileStreamNames)
            : this(conf, fileStreamNames_enumerable: fileStreamNames) { }

        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1, string fileStreamName1)
            : this(conf)
        {
            AddLogFile(newFileStreamConf1);
            AddLogFile(fileStreamName1);
        }
        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1, LogFileConfig newFileStreamConf2, string fileStreamName1)
            : this(conf)
        {
            AddLogFile(newFileStreamConf1);
            AddLogFile(newFileStreamConf2);
            AddLogFile(fileStreamName1);
        }
        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1, string fileStreamName1, string fileStreamName2)
            : this(conf)
        {
            AddLogFile(newFileStreamConf1);
            AddLogFile(fileStreamName1);
            AddLogFile(fileStreamName2);
        }
        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1, LogFileConfig newFileStreamConf2, string fileStreamName1, string fileStreamName2)
            : this(conf)
        {
            AddLogFile(newFileStreamConf1);
            AddLogFile(newFileStreamConf2);
            AddLogFile(fileStreamName1);
            AddLogFile(fileStreamName2);
        }
        public BaseLogger(LoggerConfig conf, IEnumerable<LogFileConfig> newFileStreamConfs_enumerable, IEnumerable<string> fileStreamNames_enumerable)
            : this(conf)
        {
            foreach (var newFileStreamConf in newFileStreamConfs_enumerable)
                AddLogFile(newFileStreamConf);
            foreach (var fileStreamName in fileStreamNames_enumerable)
                AddLogFile(fileStreamName);
        }
        public BaseLogger(LoggerConfig conf, LogFileConfig[] newFileStreamConfs, string[] fileStreamNames)
            : this(conf, newFileStreamConfs_enumerable: newFileStreamConfs, fileStreamNames_enumerable: fileStreamNames) { }
        #endregion

        /// <summary>
        /// Add an existing <see cref="LogFileStream"/> to the logger.
        /// </summary>
        /// <param name="fileStreamName"></param>
        public void AddLogFile(string fileStreamName)
        {
            writeTargetFiles.Add(LogFileStream.GetInitedInstance(fileStreamName));
        }

        /// <summary>
        /// Create a log file and build a log file stream for it, with the given <see cref="LogFileConfig"/>.
        /// </summary>
        /// <param name="newFileStreamConfig"></param>
        /// <exception cref="ArgumentException"></exception>
        public void AddLogFile(LogFileConfig newFileStreamConfig)
        {
            if (newFileStreamConfig.FileIdentifier == LogFileStream.GlobalLog_Reserved)
            {
                throw new ArgumentException("The name 'global' is reserved for latest.log" +
                    "and you can't create an instance with the same name.");
            }
            AddNewLogFileCore(newFileStreamConfig);
        }

        internal void AddNewLogFileCore(LogFileConfig conf)
        {
            writeTargetFiles.Add(new LogFileStream(WorkDirectory, conf));
        }
        #endregion

        #region Logger
        /// <summary>
        /// Get a channel instance that can write all logs with the same <paramref name="sender"/>.
        /// </summary>
        /// <param name="sender">The sender provided to the channel. It can be
        /// modified with <see cref="LoggerChannel.LogSender"/>.</param>
        /// <returns></returns>
        public LoggerChannel GetChannel(string? sender)
        {
            return new LoggerChannel(this, sender);
        }

        /// <summary>
        /// Put a log with Verbose Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Verbose"/>. Notice that messages put here will only be written to <c>latest.debug.log</c>.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public void Verb(string content, string? sender = null)
        {
            if (CustomConfig.Global_Minimum_LogLevel == LogLevel.Verbose)
            {
                qlog.Enqueue(new LogDetail(content, LogLevel.Verbose, sender));
            }
        }

        /// <summary>
        /// Put a log with Debug Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Debug"/>. Notice that messages put here will only be written to <c>latest.debug.log</c>.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public void Dbug(string content, string? sender = null)
        {
            if (CustomConfig.Global_Minimum_LogLevel <= LogLevel.Debug)
            {
                qlog.Enqueue(new LogDetail(content, LogLevel.Debug, sender));
            }
        }

        /// <summary>
        /// Put a log with Info Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Information"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public void Info(string content, string? sender = null)
        {
            if (CustomConfig.Global_Minimum_LogLevel <= LogLevel.Information)
            {
                qlog.Enqueue(new LogDetail(content, LogLevel.Information, sender));
            }
        }

        /// <summary>
        /// Put a log with Warning Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Warning"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public void Warn(string content, string? sender = null)
        {
            if (CustomConfig.Global_Minimum_LogLevel <= LogLevel.Warning)
            {
                qlog.Enqueue(new LogDetail(content, LogLevel.Warning, sender));
            }
        }

        /// <summary>
        /// Put a log with Error Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Error"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public void Erro(string content, string? sender = null)
        {
            if (CustomConfig.Global_Minimum_LogLevel <= LogLevel.Error)
            {
                qlog.Enqueue(new LogDetail(content, LogLevel.Error, sender));
            }
        }

        /// <summary>
        /// Put a log with a certain Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="logLevel">The <see cref="LogLevel"/> of this log message.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public void PushLog(string content, LogLevel logLevel, string? sender = null)
        {
            if (CustomConfig.Global_Minimum_LogLevel <= logLevel)
            {
                qlog.Enqueue(new LogDetail(content, logLevel, sender));
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

            private ColorLineResult? _writeResult = null;
            public ColorLineResult GetWriteFileResult()
            {
                if (_writeResult == null)
                {
                    string nowtime = create_time.ToString("HH:mm:ss");
                    string header = GetLogInfo(level, sender);
                    string res = $"{nowtime}{header}{content}";

                    _writeResult = ColorLineUtil.AnalyzeColorText(res);
                }
                return (ColorLineResult)_writeResult;
            }

            public ColorLineResult GetWriteConsoleResult(LoggerConfig conf)
            {
                string nowtime = create_time.ToString("HH:mm:ss");
                string header = GetLogInfo(level, sender);
                string res = $"{nowtime}{header}{content}";

                if (conf.Max_Output_Char_Count < 0 || content.Length < conf.Max_Output_Char_Count)
                    return ColorLineUtil.AnalyzeColorText(res);
                else
                    return ColorLineUtil.AnalyzeColorText(
                        $"{nowtime}{header}[content too long (> Maximum line length of " +
                        $"{conf.Max_Output_Char_Count}), so not output to console]");
            }
        }

        private ConcurrentQueue<LogDetail> qlog = new();
        public int RefreshLogMilliseconds { get; set; }
        private Stopwatch watch = new();
        // see https://learn.microsoft.com/zh-cn/dotnet/api/system.threading.tasks.task.delay?view=net-7.0#system-threading-tasks-task-delay(system-timespan)
        private readonly TimeSpan waitStandard = TimeSpan.FromMilliseconds(15);

        private async Task BackgroundUpdate()
        {
            while (true)
            {
                if (qlog.IsEmpty)
                {
                    if (_global_loggers_ending)
                    {
                        lock (clearup_finished_count_lck) clearup_finished_count++;
                        return;
                    }
                    await Task.Delay(RefreshLogMilliseconds);
                    continue;
                }

                watch.Restart();
                InnerWriteLogs();

                watch.Stop();
                TimeSpan relaxSpan = TimeSpan.FromMilliseconds(RefreshLogMilliseconds) - watch.Elapsed;
                if (relaxSpan >= waitStandard) await Task.Delay(relaxSpan);
            }
        }

        #region Write Methods
        private void InnerWriteLogs()
        {
            while (qlog.TryDequeue(out LogDetail log))
            {
                var consoleOutput = log.GetWriteConsoleResult(CustomConfig);
                if (log.level >= CustomConfig.Console_Minimum_LogLevel)
                {
                    if (CustomConfig.Use_Console_Wrapper)
                    {
                        ConsoleWrapper.WriteLine(consoleOutput);
                    }
                    else
                    {
                        qconsole_strings.Enqueue(consoleOutput);
                    }
                }

                foreach (var push_filestream in writeTargetFiles)
                {
                    push_filestream.WriteLine(log.GetWriteFileResult(), log.level);
                }
            }
        }
        #endregion
        #endregion

        private ColorLineResult GetWriteLog(LogDetail log)
        {
            string nowtime = log.create_time.ToString("HH:mm:ss");
            string header = GetLogInfo(log.level, log.sender);
            string res = $"{nowtime}{header}{log.content}";

            if (CustomConfig.Max_Output_Char_Count < 0 || log.content.Length < CustomConfig.Max_Output_Char_Count)
                return ColorLineUtil.AnalyzeColorText(res);
            else
                return ColorLineUtil.AnalyzeColorText(
                    $"{nowtime}{header}[content too long (> Maximum line length of {CustomConfig.Max_Output_Char_Count}), so not output to console]");
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
        #endregion
    }
}
