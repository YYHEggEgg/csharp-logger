using YYHEggEgg.Logger.Utils;

#pragma warning disable CS8602 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
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
        /// Logs that track the general flow of the application. These logs should have long-term value.
        /// </summary>
        Information = 1,
        /// <summary>
        /// Logs that highlight an abnormal or unexpected event in the application flow, but do not otherwise cause the application execution to stop.
        /// </summary>
        Warning = 2,
        /// <summary>
        /// Logs that highlight when the current flow of execution is stopped due to a failure. These should indicate a failure in the current activity, not an application-wide failure.
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
        private static LoggerConfig _global_customConfig;
        public static LoggerConfig GlobalConfig 
        {
            get
            {
                AssertInitialized(false);
                return _global_customConfig;
            }
        }

        /// <summary>
        /// The default color used by logs in Console. 
        /// </summary>
        public static ConsoleColor GlobalDefaultColor
        {
            get => ColorLineUtil.DefaultColor;
            set 
            {
                if (!Enum.IsDefined(value))
                {
                    throw new InvalidOperationException("Should be a valid ConsoleColor enum value.");
                }
                ColorLineUtil.DefaultColor = value;
            }
        }

        #region Initialize
        private static bool _initialized = false;
        private static BaseLogger? _baseLogger = null;
        public static BaseLogger GlobalBasedLogger
        {
            get
            {
                AssertInitialized(true);
                return _baseLogger ?? throw new Exception("Logger initialize bug.");
            }
        }

        /// <summary>
        /// Initialize the logger. If initialized before, the method will return immediately.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The current OS is not supported to output logs to 
        /// the console. To continue use logging on this OS,
        /// set <see cref="LoggerConfig.Console_Minimum_LogLevel"/>
        /// to <see cref="LogLevel.None"/>.
        /// <para/> or <para/> 
        /// <see cref="ConsoleWrapper"/> and any features
        /// related to console is not supported on current OS.
        /// To continue use logging on this OS, set
        /// <see cref="LoggerConfig.Use_Console_Wrapper"/>
        /// to <see cref="false"/>.
        /// </exception>
        public static void Initialize(LoggerConfig conf)
        {
            if (_initialized) return;

            if (conf.Console_Minimum_LogLevel != LogLevel.None 
                && !Tools.CheckIfSupportedOS())
            {
                throw new InvalidOperationException(
                    "Current OS is not supported to output logs " +
                    "to the console. To continue use logging on " +
                    "this OS, set LoggerConfig.Console_Minimum_LogLevel " +
                    "to LogLevel.None.");
            }

            if (conf.Use_Console_Wrapper) ConsoleWrapper.Initialize();

            _global_customConfig = conf;
            _initialized = true;

            BaseLogger.LogDetail.DetailedTimeFormat = conf.Enable_Detailed_Time;
            
            LogFileStream.HandlePastLogs(Tools.GetLoggerWorkingDir(conf));
            
            _baseLogger = new BaseLogger(conf);
            _baseLogger.AddNewLogFileCore(new LogFileConfig
            {
                FileIdentifier = "global",
                AutoFlushWriter = true,
                MinimumLogLevel = LogLevel.Information,
                MaximumLogLevel = LogLevel.Error,
                IsPipeSeparatedFile = conf.Is_PipeSeparated_Format,
            });
            if (conf.Global_Minimum_LogLevel <= LogLevel.Debug)
            {
                _baseLogger.AddNewLogFileCore(new LogFileConfig
                {
                    FileIdentifier = "debug",
                    AutoFlushWriter = conf.Debug_LogWriter_AutoFlush,
                    MinimumLogLevel = LogLevel.Verbose,
                    MaximumLogLevel = LogLevel.Error,
                    IsPipeSeparatedFile = conf.Is_PipeSeparated_Format,
                });
            }
        }

        public static void Initialize(LoggerConfig conf, IEnumerable<string> initHistory)
        {
            if (!conf.Use_Console_Wrapper)
                throw new InvalidOperationException("Init History can only be valid when specifying to use ConsoleWrapper.");
            ConsoleWrapper.Initialize(initHistory);
            Initialize(conf);
        }

        internal static void AssertInitialized(bool demand_baselogger_inited = true)
        {
            if (!_initialized)
                throw new InvalidOperationException("Logger hasn't been initialized!");
            if (demand_baselogger_inited && _baseLogger == null)
                throw new Exception("Logger init unknown error");
        }
        #endregion

        /// <summary>
        /// Get a channel instance that can write all logs with the same <paramref name="sender"/>.
        /// </summary>
        /// <param name="sender">The sender provided to the channel. It can be
        /// modified with <see cref="LoggerChannel.LogSender"/>.</param>
        /// <returns></returns>
        public static LoggerChannel GetChannel(string? sender)
        {
            AssertInitialized(true);
#pragma warning disable CS8604 // 引用类型参数可能为 null。
            return new LoggerChannel(_baseLogger, sender);
#pragma warning restore CS8604 // 引用类型参数可能为 null。
        }

        /// <summary>
        /// Add an existing <see cref="LogFileStream"/> to the logger.
        /// </summary>
        /// <param name="fileStreamName"></param>
        public static void AddLogFile(string fileStreamName)
        {
            AssertInitialized();
            _baseLogger.AddLogFile(fileStreamName);
        }

        /// <summary>
        /// Create a log file and build a log file stream for it, with the given <see cref="LogFileConfig"/>.
        /// </summary>
        /// <param name="newFileStreamConfig"></param>
        /// <exception cref="ArgumentException"></exception>
        public static void AddLogFile(LogFileConfig newFileStreamConfig)
        {
            AssertInitialized();
            _baseLogger.AddLogFile(newFileStreamConfig);
        }

        /// <summary>
        /// Put a log with Verbose Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Verbose"/>.
        /// Notice that messages put here will only be written to <c>latest.debug.log</c>.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>ded to use <see cref="nameof"/> to provide this param.</param>
        public static void Verb(string content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Verb(content, sender);
        }

        /// <summary>
        /// Put a log with Debug Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Debug"/>.
        /// Notice that messages put here will only be written to <c>latest.debug.log</c>.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>ded to use <see cref="nameof"/> to provide this param.</param>
        public static void Dbug(string content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Dbug(content, sender);
        }

        /// <summary>
        /// Put a log with Info Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Information"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>ded to use <see cref="nameof"/> to provide this param.</param>
        public static void Info(string content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Info(content, sender);
        }

        /// <summary>
        /// Put a log with Warning Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Warning"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>ded to use <see cref="nameof"/> to provide this param.</param>
        public static void Warn(string content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Warn(content, sender);
        }

        /// <summary>
        /// Put a log with Error Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Error"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>ded to use <see cref="nameof"/> to provide this param.</param>
        public static void Erro(string content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Erro(content, sender);
        }

        /// <summary>
        /// Put a log with a certain Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="logLevel">The <see cref="LogLevel"/> of this log message.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void PushLog(string content, LogLevel logLevel, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.PushLog(content, logLevel, sender);
        }
        
        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Verbose Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>.
        /// Notice that messages put here will only be written to <c>latest.debug.log</c>.
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
        public static void Verb(Func<string> getcontent_func, string? sender = null,
            Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Verb(getcontent_func, sender, on_getcontent_error);
        }

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Debug Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>.
        /// Notice that messages put here will only be written to <c>latest.debug.log</c>.
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
        public static void Dbug(Func<string> getcontent_func, string? sender = null,
            Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Dbug(getcontent_func, sender, on_getcontent_error);
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
        public static void Info(Func<string> getcontent_func, string? sender = null,
            Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Info(getcontent_func, sender, on_getcontent_error);
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
        public static void Warn(Func<string> getcontent_func, string? sender = null,
            Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Warn(getcontent_func, sender, on_getcontent_error);
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
        public static void Erro(Func<string> getcontent_func, string? sender = null,
            Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Erro(getcontent_func, sender, on_getcontent_error);
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
        public static void PushLog(Func<string> getcontent_func, LogLevel logLevel, 
            string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.PushLog(getcontent_func, logLevel, sender, on_getcontent_error);
        }
    }
}
#pragma warning restore CS8602 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
