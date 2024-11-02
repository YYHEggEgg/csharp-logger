using YYHEggEgg.Logger.Utils;
using static System.Reflection.Metadata.BlobBuilder;

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
            CheckGlobalLoggerConfig(conf);
            if (_initialized) return;
            InitializeCore(conf);
        }

        private static void CheckGlobalLoggerConfig(LoggerConfig conf)
        {
            if (!conf.Enable_Disk_Operations)
            {
                if (conf.Customized_Global_LogFile_Config != null &&
                    conf.Customized_Global_LogFile_Config?.MinimumLogLevel != LogLevel.None)
                    throw new InvalidOperationException("If disabled disk operations for logger " +
                        "under the prorgam scope, either provide conf.Customized_Global_LogFile_Config " +
                        "with null, or a customized config with MinimumLogLevel set to LogLevel.None.");
                if (conf.Customized_Debug_LogFile_Config != null &&
                    conf.Customized_Debug_LogFile_Config?.MinimumLogLevel != LogLevel.None)
                    throw new InvalidOperationException("If disabled disk operations for logger " +
                        "under the prorgam scope, either provide conf.Customized_Debug_LogFile_Config " +
                        "with null, or a customized config with MinimumLogLevel set to LogLevel.None.");
            }
        }

        private static void InitializeCore(LoggerConfig conf)
        {
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
            _baseLogger = new BaseLogger(conf);

            if (conf.Enable_Disk_Operations)
            {
                LogFileStream.HandlePastLogs(Tools.GetLoggerWorkingDir(conf));

                var glbfileconf = conf.Customized_Global_LogFile_Config;
                if (glbfileconf?.MinimumLogLevel != LogLevel.None)
                {
                    _baseLogger.AddNewLogFileCore(new LogFileConfig
                    {
                        FileIdentifier = LogFileStream.GlobalLog_Reserved,
                        AutoFlushWriter = glbfileconf?.AutoFlushWriter ?? true,
                        MinimumLogLevel = glbfileconf?.MinimumLogLevel ?? LogLevel.Information,
                        MaximumLogLevel = glbfileconf?.MaximumLogLevel ?? LogLevel.Error,
                        IsPipeSeparatedFile = glbfileconf?.IsPipeSeparatedFile ?? conf.Is_PipeSeparated_Format,
                    });
                }
                var dbgfileconf = conf.Customized_Debug_LogFile_Config;
                if (dbgfileconf?.MinimumLogLevel != LogLevel.None)
                {
                    if (conf.Global_Minimum_LogLevel <= LogLevel.Debug)
                    {
                        _baseLogger.AddNewLogFileCore(new LogFileConfig
                        {
                            FileIdentifier = "debug",
                            AutoFlushWriter = dbgfileconf?.AutoFlushWriter ?? conf.Debug_LogWriter_AutoFlush,
                            MinimumLogLevel = dbgfileconf?.MinimumLogLevel ?? LogLevel.Verbose,
                            MaximumLogLevel = dbgfileconf?.MaximumLogLevel ?? LogLevel.Error,
                            IsPipeSeparatedFile = dbgfileconf?.IsPipeSeparatedFile ?? conf.Is_PipeSeparated_Format,
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Initialize the logger and add the provided history to 
        /// <see cref="ConsoleWrapper"/>. If initialized before, 
        /// the method will throw <see cref="InvalidOperationException"/>
        /// because .
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
        /// <exception cref="InvalidOperationException">
        /// Init History can only be valid when specifying 
        /// to use ConsoleWrapper.
        /// </exception>

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

        /// <inheritdoc cref="BaseLogger.AddLogFile(string)"/>
        public static void AddLogFile(string fileStreamName)
        {
            AssertInitialized();
            _baseLogger.AddLogFile(fileStreamName);
        }

        /// <inheritdoc cref="BaseLogger.AddLogFile(LogFileConfig)"/>
        public static void AddLogFile(LogFileConfig newFileStreamConfig)
        {
            AssertInitialized();
            _baseLogger.AddLogFile(newFileStreamConfig);
        }

        /// <inheritdoc cref="BaseLogger.Verb(string?, string?)"/>
        public static void Verb(string? content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Verb(content, sender);
        }

        /// <inheritdoc cref="BaseLogger.Verb(string?, DateTime, string?)"/>
        public static void Verb(string? content, DateTime logTime, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Verb(content, logTime, sender);
        }

        /// <inheritdoc cref="BaseLogger.Dbug(string?, string?)"/>
        public static void Dbug(string? content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Dbug(content, sender);
        }

        /// <inheritdoc cref="BaseLogger.Dbug(string?, DateTime, string?)"/>
        public static void Dbug(string? content, DateTime logTime, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Dbug(content, logTime, sender);
        }

        /// <inheritdoc cref="BaseLogger.Info(string?, string?)"/>
        public static void Info(string? content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Info(content, sender);
        }

        /// <inheritdoc cref="BaseLogger.Info(string?, DateTime, string?)"/>
        public static void Info(string? content, DateTime logTime, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Info(content, logTime, sender);
        }

        /// <inheritdoc cref="BaseLogger.Warn(string?, string?)"/>
        public static void Warn(string? content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Warn(content, sender);
        }

        /// <inheritdoc cref="BaseLogger.Warn(string?, DateTime, string?)"/>
        public static void Warn(string? content, DateTime logTime, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Warn(content, logTime, sender);
        }

        /// <inheritdoc cref="BaseLogger.Erro(string?, string?)"/>
        public static void Erro(string? content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Erro(content, sender);
        }

        /// <inheritdoc cref="BaseLogger.Erro(string?, DateTime, string?)"/>
        public static void Erro(string? content, DateTime logTime, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Erro(content, logTime, sender);
        }

        /// <inheritdoc cref="BaseLogger.PushLog(string?, LogLevel, string?)"/>
        public static void PushLog(string? content, LogLevel logLevel, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.PushLog(content, logLevel, sender);
        }

        /// <inheritdoc cref="BaseLogger.PushLog(string?, LogLevel, DateTime, string?)"/>
        public static void PushLog(string? content, LogLevel logLevel, DateTime logTime, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.PushLog(content, logLevel, logTime, sender);
        }

        /// <inheritdoc cref="BaseLogger.Verb(Func{string?}, string?, Action{Exception}?)"/>
        public static void Verb(Func<string?> getcontent_func, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Verb(getcontent_func, sender, on_getcontent_error);
        }

        /// <inheritdoc cref="BaseLogger.Verb(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public static void Verb(Func<string?> getcontent_func, DateTime logTime, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Verb(getcontent_func, logTime, sender, on_getcontent_error);
        }

        /// <inheritdoc cref="BaseLogger.Dbug(Func{string?}, string?, Action{Exception}?)"/>
        public static void Dbug(Func<string?> getcontent_func, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Dbug(getcontent_func, sender, on_getcontent_error);
        }

        /// <inheritdoc cref="BaseLogger.Dbug(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public static void Dbug(Func<string?> getcontent_func, DateTime logTime, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Dbug(getcontent_func, logTime, sender, on_getcontent_error);
        }

        /// <inheritdoc cref="BaseLogger.Info(Func{string?}, string?, Action{Exception}?)"/>
        public static void Info(Func<string?> getcontent_func, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Info(getcontent_func, sender, on_getcontent_error);
        }

        /// <inheritdoc cref="BaseLogger.Info(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public static void Info(Func<string?> getcontent_func, DateTime logTime, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Info(getcontent_func, logTime, sender, on_getcontent_error);
        }

        /// <inheritdoc cref="BaseLogger.Warn(Func{string?}, string?, Action{Exception}?)"/>
        public static void Warn(Func<string?> getcontent_func, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Warn(getcontent_func, sender, on_getcontent_error);
        }

        /// <inheritdoc cref="BaseLogger.Warn(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public static void Warn(Func<string?> getcontent_func, DateTime logTime, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Warn(getcontent_func, logTime, sender, on_getcontent_error);
        }

        /// <inheritdoc cref="BaseLogger.Erro(Func{string?}, string?, Action{Exception}?)"/>
        public static void Erro(Func<string?> getcontent_func, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Erro(getcontent_func, sender, on_getcontent_error);
        }

        /// <inheritdoc cref="BaseLogger.Erro(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public static void Erro(Func<string?> getcontent_func, DateTime logTime, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.Erro(getcontent_func, logTime, sender, on_getcontent_error);
        }

        /// <inheritdoc cref="BaseLogger.PushLog(Func{string?}, LogLevel, string?, Action{Exception}?)"/>
        public static void PushLog(Func<string?> getcontent_func, LogLevel logLevel, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.PushLog(getcontent_func, logLevel, sender, on_getcontent_error);
        }

        /// <inheritdoc cref="BaseLogger.PushLog(Func{string?}, LogLevel, DateTime, string?, Action{Exception}?)"/>
        public static void PushLog(Func<string?> getcontent_func, LogLevel logLevel, DateTime logTime, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            AssertInitialized();
            _baseLogger.PushLog(getcontent_func, logLevel, logTime, sender, on_getcontent_error);
        }
    }
}
#pragma warning restore CS8602 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
