using System.Collections.Concurrent;
using System.Diagnostics;
using YYHEggEgg.Logger.Utils;

namespace YYHEggEgg.Logger
{
    public class BaseLogger
    {
        #region Background Console
        private static ConcurrentQueue<ColorLineResult> qconsole_strings = new();
        private static ConcurrentBag<BaseLogger> working_loggers = new ConcurrentBag<BaseLogger>();
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

        /// <summary>
        /// Determine whether a log file with <paramref name="fileIdentifier"/> is registered.
        /// </summary>
        /// <param name="fileIdentifier"></param>
        /// <returns></returns>
        public static bool LogFileExists(string fileIdentifier) => LogFileStream.LogFileExists(fileIdentifier);

        #region Initialize
        /// <summary>
        /// Initialize the logger. If initialized before, the method will return immediately.
        /// </summary>
        internal BaseLogger(LoggerConfig conf)
        {
            CustomConfig = conf;
            VerifyConfWithGlobal(conf);

            RefreshLogMilliseconds = 100;
            WorkDirectory = Tools.GetLoggerWorkingDir(conf);

            Task.Run(BackgroundUpdate);
        }

        private static void VerifyConfWithGlobal(LoggerConfig conf)
        {
            if (conf.Use_Console_Wrapper != Log.GlobalConfig.Use_Console_Wrapper)
            {
                throw new InvalidOperationException("The whole program should use only " +
                    "one Console implement, so LoggerConfig.Use_Console_Wrapper should be provided " +
                    "the same value when initializing any BaseLogger.");
            }
            if (conf.Use_Working_Directory != Log.GlobalConfig.Use_Working_Directory)
            {
                throw new InvalidOperationException("The whole program should have only " +
                    "one logs working directory, so LoggerConfig.Use_Working_Directory " +
                    "should be provided the same value when initializing any BaseLogger.");
            }
            if (conf.Max_Output_Char_Count != Log.GlobalConfig.Max_Output_Char_Count)
            {
                throw new InvalidOperationException("Currently, LoggerConfig.Max_Output_Char_Count " +
                    "should be provided the same value when initializing any BaseLogger " +
                    "because of performace and other reasons.");
            }
            if (conf.Enable_Detailed_Time != Log.GlobalConfig.Enable_Detailed_Time)
            {
                throw new InvalidOperationException("Currently, LoggerConfig.Enable_Detailed_Time " +
                   "should be provided the same value when initializing any BaseLogger " +
                   "because of performace and other reasons.");
            }
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
        public BaseLogger(LoggerConfig conf, string[] fileStreamNames)
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
            var fileIdentifier = newFileStreamConfig.FileIdentifier ?? "<null>";
            if (fileIdentifier == LogFileStream.GlobalLog_Reserved)
            {
                throw new ArgumentException("The name 'global' is reserved for latest.log" +
                    "and you can't create an instance with the same name.", fileIdentifier);
            }
            if (fileIdentifier == null)
            {
                throw new ArgumentException("A new log file instance should have a valid name.", fileIdentifier);
            }
            if (newFileStreamConfig.AllowAutoFallback && LogFileExists(fileIdentifier))
            {
                var created = LogFileStream.GetInitedInstance(fileIdentifier);
                if (!newFileStreamConfig.Equals(new LogFileConfig
                {
                    FileIdentifier = created.FileStreamName,
                    MinimumLogLevel = created.MinimumLogLevel,
                    MaximumLogLevel = created.MaximumLogLevel,
                    IsPipeSeparatedFile = created.IsPipeSeparatedFormat,
                    AutoFlushWriter = created.AutoFlushWriter,
                }))
                {
                    throw new ArgumentException("The fallback of creating a log file requires the LogFileConfig to be the same.", fileIdentifier);
                }
                AddLogFile(fileIdentifier);
            }
            else AddNewLogFileCore(newFileStreamConfig);
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

        #region Log Methods
        /// <summary>
        /// Put a log with Verbose Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Verbose"/>.
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
        /// Put a log with Debug Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Debug"/>.
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
        /// Put a log with Info Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Information"/>. 
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
        /// Put a log with Warning Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Warning"/>. 
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
        /// Put a log with Error Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Error"/>. 
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
        /// Put a log with a certain Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="logLevel">The <see cref="LogLevel"/> of this log message.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public void PushLog(string content, LogLevel logLevel, string? sender = null)
        {
            if (logLevel == LogLevel.None)
            {
                throw new InvalidOperationException("A log with LogLevel.None cannot be pushed and handled.");
            }
            if (!Enum.IsDefined(logLevel))
            {
                throw new ArgumentException("A log with an invalid level cannot be pushed and handled.", nameof(logLevel));
            }
            if (CustomConfig.Global_Minimum_LogLevel <= logLevel)
            {
                qlog.Enqueue(new LogDetail(content, logLevel, sender));
            }
        }

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Verbose Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="getcontent_func">
        /// The func used to get the log content. It'll be invoked afterwards by
        /// the logger's background refreshing task.
        /// </param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, the logger will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        public void Verb(Func<string> getcontent_func, string? sender = null,
            Action<Exception>? on_getcontent_error = null)
        {
            if (CustomConfig.Global_Minimum_LogLevel == LogLevel.Verbose)
            {
                qlog.Enqueue(new LogDetail(string.Empty, LogLevel.Verbose, sender, getcontent_func, on_getcontent_error));
            }
        }

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Debug Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="getcontent_func">
        /// The func used to get the log content. It'll be invoked afterwards by
        /// the logger's background refreshing task.
        /// </param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, the logger will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        public void Dbug(Func<string> getcontent_func, string? sender = null,
            Action<Exception>? on_getcontent_error = null)
        {
            if (CustomConfig.Global_Minimum_LogLevel <= LogLevel.Debug)
            {
                qlog.Enqueue(new LogDetail(string.Empty, LogLevel.Debug, sender, getcontent_func, on_getcontent_error));
            }
        }

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Information Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="getcontent_func">
        /// The func used to get the log content. It'll be invoked afterwards by
        /// the logger's background refreshing task.
        /// </param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, the logger will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        public void Info(Func<string> getcontent_func, string? sender = null,
            Action<Exception>? on_getcontent_error = null)
        {
            if (CustomConfig.Global_Minimum_LogLevel <= LogLevel.Information)
            {
                qlog.Enqueue(new LogDetail(string.Empty, LogLevel.Information, sender, getcontent_func, on_getcontent_error));
            }
        }

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Warning Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="getcontent_func">
        /// The func used to get the log content. It'll be invoked afterwards by
        /// the logger's background refreshing task.
        /// </param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, the logger will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        public void Warn(Func<string> getcontent_func, string? sender = null,
            Action<Exception>? on_getcontent_error = null)
        {
            if (CustomConfig.Global_Minimum_LogLevel <= LogLevel.Warning)
            {
                qlog.Enqueue(new LogDetail(string.Empty, LogLevel.Warning, sender, getcontent_func, on_getcontent_error));
            }
        }

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Error Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="getcontent_func">
        /// The func used to get the log content. It'll be invoked afterwards by
        /// the logger's background refreshing task.
        /// </param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, the logger will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        public void Erro(Func<string> getcontent_func, string? sender = null,
            Action<Exception>? on_getcontent_error = null)
        {
            if (CustomConfig.Global_Minimum_LogLevel <= LogLevel.Error)
            {
                qlog.Enqueue(new LogDetail(string.Empty, LogLevel.Error, sender, getcontent_func, on_getcontent_error));
            }
        }

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with a certain Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="getcontent_func">
        /// The func used to get the log content. It'll be invoked afterwards by
        /// the logger's background refreshing task.
        /// </param>
        /// <param name="logLevel">The <see cref="LogLevel"/> of this log message.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, the logger will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        public void PushLog(Func<string> getcontent_func, LogLevel logLevel, 
            string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            if (logLevel == LogLevel.None)
            {
                throw new InvalidOperationException("A log with LogLevel.None cannot be pushed and handled.");
            }
            if (!Enum.IsDefined(logLevel))
            {
                throw new ArgumentException("A log with an invalid level cannot be pushed and handled.", nameof(logLevel));
            }
            if (CustomConfig.Global_Minimum_LogLevel <= logLevel)
            {
                qlog.Enqueue(new LogDetail(string.Empty, logLevel, sender, getcontent_func, on_getcontent_error));
            }
        }
        #endregion

        #region Background Refresh
        internal struct LogDetail
        {
            public LogLevel level;
            public string content;
            public string? sender;
            public DateTime create_time;
            public Func<string>? getcontent_func;
            public Action<Exception>? on_getcontent_error;

            public LogDetail(string con, LogLevel lvl, string? snd, 
                Func<string>? getcontent_func = null, Action<Exception>? on_getcontent_error = null)
            {
                level = lvl;
                content = con;
                sender = snd;
                var nowtime = DateTime.Now;
                create_time = nowtime;
                this.getcontent_func = getcontent_func;
                this.on_getcontent_error = on_getcontent_error ?? new(ex =>
                {
                    Log.Warn($"Delay log content (pushed on {FormatTime(nowtime)}) get failed: {ex}", snd);
                });
            }

            internal static bool DetailedTimeFormat = false;
            internal static string FormatTime(DateTime t)
            {
                if (DetailedTimeFormat)
                {
                    int yyyy = t.Year;
                    int MM = t.Month;
                    int dd = t.Day;
                    int HH = t.Hour;
                    int mm = t.Minute;
                    int ss = t.Second;
                    long ticks = t.Ticks;
                    int millisec = t.Millisecond;
                    int microsec = (int)(ticks % 10000) / 10;
                    int nanosec_multiple100 = (int)(ticks % 10);
                    return $"{yyyy:D4}-{MM:D2}-{dd:D2} {HH:D2}:{mm:D2}:{ss:D2} {millisec:D3} {microsec:D3}{nanosec_multiple100}";
                }
                else return t.ToString("HH:mm:ss");
            }

            private ColorLineResult? _writeResult = null;
            public ColorLineResult GetWriteFileResult(bool is_pipeseparated_format)
            {
                if (_writeResult == null)
                {
                    string nowtime = FormatTime(create_time);
                    string header = GetFileLogInfo(level, sender, is_pipeseparated_format);
                    string res = $"{nowtime}{header}{content}";

                    _writeResult = ColorLineUtil.AnalyzeColorText(res);
                }
                return (ColorLineResult)_writeResult;
            }

            public ColorLineResult GetWriteConsoleResult(LoggerConfig conf)
            {
                string nowtime = FormatTime(create_time);
                string header = GetConsoleLogInfo(level, sender);
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

        private void InnerWriteLogs()
        {
            while (qlog.TryDequeue(out LogDetail log))
            {
                if (log.getcontent_func != null)
                {
                    log.content = log.getcontent_func();
                }
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
                    push_filestream.WriteLine(log.GetWriteFileResult(push_filestream.IsPipeSeparatedFormat), log.level);
                }
            }
        }
        #endregion

        /// <summary>
        /// Write info <[level]:[sender]> like <Info:KCP>. 
        /// </summary>
        private static string GetConsoleLogInfo(LogLevel level, string? sender)
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

        private static string GetFileLogInfo(LogLevel level, string? sender,
            bool is_pipeseparated_format)
        {
            if (is_pipeseparated_format)
            {
                switch (level)
                {
                    case LogLevel.Verbose:
                        return $"|Verb|{sender}|";
                    case LogLevel.Debug:
                        return $"|Dbug|{sender}|";
                    case LogLevel.Information:
                        return $"|Info|{sender}|";
                    case LogLevel.Warning:
                        return $"|Warn|{sender}|";
                    case LogLevel.Error:
                        return $"|Erro|{sender}|";
                    default:
                        throw new Exception("Unexpected enum value!");
                }
            }
            else
            {
                string loglevel_id;
                switch (level)
                {
                    case LogLevel.Verbose:
                        loglevel_id = "Verb";
                        break;
                    case LogLevel.Debug:
                        loglevel_id = "Dbug";
                        break;
                    case LogLevel.Information:
                        loglevel_id = "Info";
                        break;
                    case LogLevel.Warning:
                        loglevel_id = "Warn";
                        break;
                    case LogLevel.Error:
                        loglevel_id = "Erro";
                        break;
                    default:
                        throw new Exception("Unexpected enum value!");
                }
                if (sender == null) return $" <{loglevel_id}> ";
                else return $" <{loglevel_id}:{sender}> ";
            }
        }
        #endregion
    }
}
