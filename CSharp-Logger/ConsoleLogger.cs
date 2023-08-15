using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using YYHEggEgg.Logger.Utils;
using System.Globalization;
using System.IO.Compression;

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
        public static LoggerConfig GlobalConfig => _global_customConfig;

        #region Initialize
        private static bool _initialized = false;
        private static BaseLogger? _baseLogger = null;
        public static BaseLogger GlobalBasedLogger 
        {
            get
            {
                AssertInitialized();
                return _baseLogger ?? throw new Exception("Logger initialize bug.");
            }
        }

        /// <summary>
        /// Initialize the logger. If initialized before, the method will return immediately.
        /// </summary>
        public static void Initialize(LoggerConfig conf)
        {
            if (_initialized) return;

            _initialized = true;

            _global_customConfig = conf;
            if (conf.Use_Console_Wrapper) ConsoleWrapper.Initialize();

            LogFileStream.HandlePastLogs(Tools.GetLoggerWorkingDir(conf));
            
            _baseLogger = new BaseLogger(conf);
            _baseLogger.AddNewLogFileCore(new LogFileConfig
            {
                FileIdentifier = "global",
                AutoFlushWriter = true,
                MinimumLogLevel = LogLevel.Information,
                MaximumLogLevel = LogLevel.Error,
            });
            if (conf.Global_Minimum_LogLevel <= LogLevel.Debug)
            {
                _baseLogger.AddNewLogFileCore(new LogFileConfig
                {
                    FileIdentifier = "debug",
                    AutoFlushWriter = conf.Debug_LogWriter_AutoFlush,
                    MinimumLogLevel = LogLevel.Verbose,
                    MaximumLogLevel = LogLevel.Error,
                });
            }
        }

        internal static void AssertInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException("Logger hasn't been initialized!");
            if (_baseLogger == null)
                throw new Exception("Logger init unknown error");
        }
        #endregion

        /// <summary>
        /// Put a log with Verbose Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Verbose"/>. Notice that messages put here will only be written to <c>latest.debug.log</c>.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Verb(string content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Verb(content, sender);
        }

        /// <summary>
        /// Put a log with Debug Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Debug"/>. Notice that messages put here will only be written to <c>latest.debug.log</c>.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Dbug(string content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Dbug(content, sender);
        }

        /// <summary>
        /// Put a log with Info Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Information"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Info(string content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Info(content, sender);
        }

        /// <summary>
        /// Put a log with Warning Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Warning"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Warn(string content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Info(content, sender);
        }

        /// <summary>
        /// Put a log with Error Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <see cref="LogLevel.Error"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void Erro(string content, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.Erro(content, sender);
        }

        /// <summary>
        /// Put a log with a certain Level to the handle queue. Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/> <= <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="logLevel">The <see cref="LogLevel"/> of this log message.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public static void PushLog(string content, LogLevel logLevel, string? sender = null)
        {
            AssertInitialized();
            _baseLogger.PushLog(content, logLevel, sender);
        }
    }
}
#pragma warning restore CS8602 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
