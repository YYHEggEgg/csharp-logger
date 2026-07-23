using Internal.ReadLine.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;
using YYHEggEgg.Logger.Utils;

namespace YYHEggEgg.Logger
{
    public class BaseLogger
    {
        #region Background Console
        private static readonly ConcurrentQueue<ColorLineResult> qconsole_strings = new();
        private static readonly ConcurrentDictionary<BaseLogger, byte> working_loggers =
            new(ReferenceEqualityComparer.Instance);
        private static readonly object loggerRegistrationLock = new();
        private static readonly SemaphoreSlim consoleWriteSignal = new(0, 1);
        private static readonly Task consoleWriterTask;
        static BaseLogger()
        {
            AppDomain.CurrentDomain.ProcessExit += GlobalCleanup;
            consoleWriterTask = Task.Run(BackgroundWriteConsole);
        }

        /// <summary>
        /// Forces the static constructor to run without requiring an initialized
        /// global Log instance. ConsoleWrapper uses this to register cleanup in
        /// the correct order when it is initialized on its own.
        /// </summary>
        internal static void EnsureCleanupRegistered() { }

        internal static volatile bool _global_loggers_ending;
        private static volatile bool _console_cleared_up;
        private static readonly IConsole _absConsole = new Console2();
        /// <summary>
        /// Console.Write will pend as the user selected content in the console. But the file I/O shouldn't be pended.
        /// </summary>
        /// <returns></returns>
        private static async Task BackgroundWriteConsole()
        {
            try
            {
                while (true)
                {
                    while (qconsole_strings.TryDequeue(out var colorString))
                    {
                        try
                        {
                            // Only non-wrapper loggers enqueue here, so consulting
                            // Log.GlobalConfig would be both redundant and unsafe
                            // when ConsoleWrapper was initialized independently.
                            colorString.WriteToConsole(_absConsole);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"BaseLogger console writer failed: {ex}");
                        }
                    }

                    if (_global_loggers_ending && _cleanup_completed && qconsole_strings.IsEmpty)
                        return;

                    await consoleWriteSignal.WaitAsync(50).ConfigureAwait(false);
                }
            }
            finally
            {
                _console_cleared_up = true;
            }
        }

        private static void SignalConsoleWriter()
        {
            try
            {
                consoleWriteSignal.Release();
            }
            catch (SemaphoreFullException)
            {
                // A signal is already pending.
            }
        }
        #endregion

        #region Global Clear Up
        private const int GlobalCleanupTimeoutMilliseconds = 5000;
        private const int LogDrainTimeoutMilliseconds = 4500;
        private static int cleanup_finished_count;
        private static int registered_logger_count;
        private static int cleanup_started;
        private static long outstanding_log_count;
        private static bool global_log_production_completed;
        private static readonly AsyncLocal<int> workerLogProcessingDepth = new();
        internal static bool _cleanup_completed =>
            Volatile.Read(ref cleanup_finished_count) >= Volatile.Read(ref registered_logger_count);

        private static void GlobalCleanup(object? o, EventArgs e)
        {
            if (Interlocked.Exchange(ref cleanup_started, 1) != 0) return;

            var cleanupWatch = Stopwatch.StartNew();
            lock (loggerRegistrationLock)
            {
                _global_loggers_ending = true;
                foreach (var logger in working_loggers.Keys)
                    logger.SignalLoggerWorker();
            }
            SignalConsoleWriter();

            // Process-exit rejects new external logs, but callbacks already
            // accepted by a logger may still derive logs into another logger.
            // Keep every worker alive until that transitive outstanding count
            // reaches zero, then close all producer gates atomically.
            // Reserve a small part of the global budget for best-effort file
            // flushing and console draining even when one callback is stuck.
            WaitUntil(TryCompleteGlobalLogProduction, cleanupWatch,
                LogDrainTimeoutMilliseconds);
            WaitUntil(() => _cleanup_completed, cleanupWatch,
                LogDrainTimeoutMilliseconds);
            FlushPendingLogFiles(cleanupWatch);
            SignalConsoleWriter();

            // Logger workers are the producers for ConsoleWrapper. Only stop
            // the wrapper after they have drained, preserving the final tail.
            if (ConsoleWrapper.IsInitialized)
            {
                ConsoleWrapper.RequestShutdown();
                int remaining = Math.Max(0,
                    GlobalCleanupTimeoutMilliseconds - (int)cleanupWatch.ElapsedMilliseconds);
                ConsoleWrapper.WaitForCleanup(TimeSpan.FromMilliseconds(remaining));
            }

            // Usually only one console consumer has data, but waiting for both
            // also covers applications that initialized ConsoleWrapper directly
            // and later created a non-wrapper BaseLogger.
            WaitUntil(() => _console_cleared_up, cleanupWatch);
        }

        private static void WaitUntil(Func<bool> condition, Stopwatch cleanupWatch,
            int timeoutMilliseconds = GlobalCleanupTimeoutMilliseconds)
        {
            while (!condition() && cleanupWatch.ElapsedMilliseconds < timeoutMilliseconds)
                Thread.Sleep(20);
        }

        private static bool TryCompleteGlobalLogProduction()
        {
            lock (loggerRegistrationLock)
            {
                if (global_log_production_completed)
                    return true;
                if (Volatile.Read(ref outstanding_log_count) != 0)
                    return false;

                global_log_production_completed = true;
                foreach (var logger in working_loggers.Keys)
                    logger.CompleteLogProduction();
                return true;
            }
        }

        private static void FlushPendingLogFiles(Stopwatch cleanupWatch)
        {
            int remaining = Math.Max(0,
                GlobalCleanupTimeoutMilliseconds - (int)cleanupWatch.ElapsedMilliseconds);
            if (remaining == 0) return;

            Task flushTask = Task.Run(() =>
                LogFileStream.FlushAllPendingWrites(ex =>
                    Debug.WriteLine($"Final log file flush failed: {ex}")));
            try
            {
                flushTask.Wait(remaining);
            }
            catch (AggregateException ex)
            {
                Debug.WriteLine($"Final log file flush task failed: {ex.Flatten()}");
            }
        }
        #endregion

        public readonly LoggerConfig CustomConfig;
        private readonly ConcurrentBag<LogFileStream> writeTargetFiles = new();
        private readonly ConcurrentBag<LogFileStream> ownedWriteTargetFiles = new();
        private readonly ConcurrentBag<LogFileStream> referencedWriteTargetFiles = new();
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
        internal BaseLogger(LoggerConfig conf, bool verifyWithGlobal = true)
        {
            CustomConfig = conf;
            if (verifyWithGlobal)
                VerifyConfWithGlobal(conf);

            RefreshLogMilliseconds = 100;
            WorkDirectory = verifyWithGlobal
                ? Tools.GetLoggerWorkingDir(conf)
                : Tools.GetLoggerWorkingDirForGlobalInitialization(conf);

            lock (loggerRegistrationLock)
            {
                if (_global_loggers_ending)
                    throw new InvalidOperationException("Cannot create a logger while global cleanup is running.");
                if (!working_loggers.TryAdd(this, 0))
                    throw new InvalidOperationException("The logger is already registered.");
                Interlocked.Increment(ref registered_logger_count);
            }
            try
            {
                backgroundUpdateTask = Task.Run(BackgroundUpdate);
            }
            catch
            {
                working_loggers.TryRemove(this, out _);
                // Keep the historical registration/completion counters balanced
                // even if the runtime cannot schedule this logger's worker.
                Interlocked.Increment(ref cleanup_finished_count);
                throw;
            }
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
            if (conf.Enable_Disk_Operations != Log.GlobalConfig.Enable_Disk_Operations)
            {
                throw new InvalidOperationException("LoggerConfig.Enable_Disk_Operations requires " +
                    "unity under the program scope. If you just want to disable the default " +
                    "log files, consider LoggerConfig.Customized_Global_LogFile_Config.");
            }
        }

        #region .ctors
        private BaseLogger(LoggerConfig conf,
            IEnumerable<LogFileConfig>? newFileStreamConfs,
            IEnumerable<string>? fileStreamNames, byte initializeFiles)
            : this(conf)
        {
            try
            {
                if (newFileStreamConfs != null)
                {
                    foreach (var newFileStreamConf in newFileStreamConfs)
                        AddLogFile(newFileStreamConf);
                }
                if (fileStreamNames != null)
                {
                    foreach (var fileStreamName in fileStreamNames)
                        AddLogFile(fileStreamName);
                }
            }
            catch
            {
                // A constructor that throws never exposes this logger. Close
                // its worker and roll back only the file streams it created;
                // referenced pre-existing streams remain owned by their creator.
                AbortInitialization();
                throw;
            }
        }

        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1)
            : this(conf, new[] { newFileStreamConf1 }, null, 0) { }
        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1, LogFileConfig newFileStreamConf2)
            : this(conf, new[] { newFileStreamConf1, newFileStreamConf2 }, null, 0) { }
        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1, LogFileConfig newFileStreamConf2, LogFileConfig newFileStreamConf3)
            : this(conf, new[] { newFileStreamConf1, newFileStreamConf2, newFileStreamConf3 }, null, 0) { }
        public BaseLogger(LoggerConfig conf, IEnumerable<LogFileConfig> newFileStreamConfs_enumerable)
            : this(conf, newFileStreamConfs_enumerable, null, 0) { }
        public BaseLogger(LoggerConfig conf, params LogFileConfig[] newFileStreamConfs)
            : this(conf, newFileStreamConfs_enumerable: newFileStreamConfs) { }

        public BaseLogger(LoggerConfig conf, string fileStreamName1)
            : this(conf, null, new[] { fileStreamName1 }, 0) { }
        public BaseLogger(LoggerConfig conf, string fileStreamName1, string fileStreamName2)
            : this(conf, null, new[] { fileStreamName1, fileStreamName2 }, 0) { }
        public BaseLogger(LoggerConfig conf, string fileStreamName1, string fileStreamName2, string fileStreamName3)
            : this(conf, null, new[] { fileStreamName1, fileStreamName2, fileStreamName3 }, 0) { }
        public BaseLogger(LoggerConfig conf, IEnumerable<string> fileStreamNames_enumerable)
            : this(conf, null, fileStreamNames_enumerable, 0) { }
        public BaseLogger(LoggerConfig conf, string[] fileStreamNames)
            : this(conf, fileStreamNames_enumerable: fileStreamNames) { }

        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1, string fileStreamName1)
            : this(conf, new[] { newFileStreamConf1 }, new[] { fileStreamName1 }, 0) { }
        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1, LogFileConfig newFileStreamConf2, string fileStreamName1)
            : this(conf, new[] { newFileStreamConf1, newFileStreamConf2 },
                new[] { fileStreamName1 }, 0) { }
        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1, string fileStreamName1, string fileStreamName2)
            : this(conf, new[] { newFileStreamConf1 },
                new[] { fileStreamName1, fileStreamName2 }, 0) { }
        public BaseLogger(LoggerConfig conf, LogFileConfig newFileStreamConf1, LogFileConfig newFileStreamConf2, string fileStreamName1, string fileStreamName2)
            : this(conf, new[] { newFileStreamConf1, newFileStreamConf2 },
                new[] { fileStreamName1, fileStreamName2 }, 0) { }
        public BaseLogger(LoggerConfig conf, IEnumerable<LogFileConfig> newFileStreamConfs_enumerable, IEnumerable<string> fileStreamNames_enumerable)
            : this(conf, newFileStreamConfs_enumerable, fileStreamNames_enumerable, 0) { }
        public BaseLogger(LoggerConfig conf, LogFileConfig[] newFileStreamConfs, string[] fileStreamNames)
            : this(conf, newFileStreamConfs_enumerable: newFileStreamConfs, fileStreamNames_enumerable: fileStreamNames) { }
        #endregion

        /// <summary>
        /// Add an existing <see cref="LogFileStream"/> to the logger.
        /// </summary>
        /// <param name="fileStreamName"></param>
        public void AddLogFile(string fileStreamName)
        {
            var fileStream = LogFileStream.GetInitedInstance(fileStreamName);
            referencedWriteTargetFiles.Add(fileStream);
            writeTargetFiles.Add(fileStream);
        }

        /// <summary>
        /// Create a log file and build a log file stream for it, with the given <see cref="LogFileConfig"/>.
        /// </summary>
        /// <param name="newFileStreamConfig"></param>
        /// <exception cref="ArgumentException"></exception>
        public void AddLogFile(LogFileConfig newFileStreamConfig)
        {
            if (!Log.GlobalConfig.Enable_Disk_Operations)
            {
                throw new InvalidOperationException("Disk operations are forcefully disabled " +
                    "globally, so no new log files should be created.");
            }
            string? fileIdentifier = newFileStreamConfig.FileIdentifier;
            if (fileIdentifier == null)
            {
                throw new ArgumentException("A new log file instance should have a valid name.",
                    nameof(newFileStreamConfig));
            }
            if (fileIdentifier == LogFileStream.GlobalLog_Reserved)
            {
                throw new ArgumentException("The name 'global' is reserved for latest.log " +
                    "and you can't create an instance with the same name.",
                    nameof(newFileStreamConfig));
            }
            if (newFileStreamConfig.AllowAutoFallback)
            {
                var fileStream = LogFileStream.CreateOrAcquire(
                    WorkDirectory, newFileStreamConfig, out bool created);
                if (created)
                    ownedWriteTargetFiles.Add(fileStream);
                else
                    referencedWriteTargetFiles.Add(fileStream);
                writeTargetFiles.Add(fileStream);
            }
            else AddNewLogFileCore(newFileStreamConfig);
        }

        internal void AddNewLogFileCore(LogFileConfig conf)
        {
            var fileStream = new LogFileStream(WorkDirectory, conf);
            ownedWriteTargetFiles.Add(fileStream);
            writeTargetFiles.Add(fileStream);
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
        /// <inheritdoc cref="Verb(string?, DateTime, string?)"/>
        public void Verb(string? content, string? sender = null) =>
            Verb(content, DateTime.Now, sender);

        /// <summary>
        /// Put a log with Verbose Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Verbose"/>.
        /// </summary>
        /// <inheritdoc cref="PushLog(string?, LogLevel, DateTime, string?)"/>
        public void Verb(string? content, DateTime logTime, string? sender = null) =>
            PushKnownLogEnumUnchecked(content, LogLevel.Verbose, logTime, sender);

        /// <inheritdoc cref="Dbug(string?, DateTime, string?)"/>
        public void Dbug(string? content, string? sender = null) =>
            Dbug(content, DateTime.Now, sender);

        /// <summary>
        /// Put a log with Debug Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Debug"/>.
        /// </summary>
        /// <inheritdoc cref="PushLog(string?, LogLevel, DateTime, string?)"/>
        public void Dbug(string? content, DateTime logTime, string? sender = null) =>
            PushKnownLogEnumUnchecked(content, LogLevel.Debug, logTime, sender);

        /// <inheritdoc cref="Info(string?, DateTime, string?)"/>
        public void Info(string? content, string? sender = null) =>
            Info(content, DateTime.Now, sender);

        /// <summary>
        /// Put a log with Info Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Information"/>.
        /// </summary>
        /// <inheritdoc cref="PushLog(string?, LogLevel, DateTime, string?)"/>
        public void Info(string? content, DateTime logTime, string? sender = null) =>
            PushKnownLogEnumUnchecked(content, LogLevel.Information, logTime, sender);

        /// <inheritdoc cref="Warn(string?, DateTime, string?))"/>
        public void Warn(string? content, string? sender = null) =>
            Warn(content, DateTime.Now, sender);

        /// <summary>
        /// Put a log with Warning Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Warning"/>.
        /// </summary>
        /// <inheritdoc cref="PushLog(string?, LogLevel, DateTime, string?)"/>
        public void Warn(string? content, DateTime logTime, string? sender = null) =>
            PushKnownLogEnumUnchecked(content, LogLevel.Warning, logTime, sender);

        /// <inhericdoc cref="Erro(string?, DateTime, string?)"/>
        public void Erro(string? content, string? sender = null) =>
            Erro(content, DateTime.Now, sender);
        /// <summary>
        /// Put a log with Error Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Error"/>.
        /// </summary>
        /// <inheritdoc cref="PushLog(string?, LogLevel, DateTime, string?)"/>
        public void Erro(string? content, DateTime logTime, string? sender = null) =>
            PushKnownLogEnumUnchecked(content, LogLevel.Error, logTime, sender);

        /// <inheritdoc cref="PushLog(string?, LogLevel, DateTime, string?))"/>
        public void PushLog(string? content, LogLevel logLevel, string? sender = null) => PushLog(content, logLevel, DateTime.Now, sender);

        /// <summary>
        /// Put a log with a certain Level to the handle queue.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="logLevel">The <see cref="LogLevel"/> of this log message.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="logTime">The time this log claim to be.</param>
        public void PushLog(string? content, LogLevel logLevel, DateTime logTime, string? sender = null)
        {
            if (logLevel == LogLevel.None)
            {
                throw new InvalidOperationException("A log with LogLevel.None cannot be pushed and handled.");
            }
            if (!Enum.IsDefined(logLevel))
            {
                throw new ArgumentException("A log with an invalid level cannot be pushed and handled.", nameof(logLevel));
            }
            PushKnownLogEnumUnchecked(content, logLevel, logTime, sender);
        }

        private void PushKnownLogEnumUnchecked(string? content, LogLevel logLevel, DateTime logTime, string? sender)
        {
            if (CustomConfig.Global_Minimum_LogLevel <= logLevel)
            {
                EnqueueLog(new LogDetail(content, logLevel, sender, logTime));
            }
        }

        /// <inheritdoc cref="Verb(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public void Verb(Func<string?> getcontent_func, string? sender = null, Action<Exception>? on_getcontent_error = null) =>
            Verb(getcontent_func, DateTime.Now, sender, on_getcontent_error);

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Verbose Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>.
        /// </summary>
        /// <inheritdoc cref="PushLog(Func{string?}, LogLevel, DateTime, string?, Action{Exception}?)"/>
        public void Verb(Func<string?> getcontent_func, DateTime logTime, string? sender = null, Action<Exception>? on_getcontent_error = null) =>
            PushCallbackLogEnumUnchecked(getcontent_func, LogLevel.Verbose, logTime, sender, on_getcontent_error);


        /// <inheritdoc cref="Dbug(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public void Dbug(Func<string?> getcontent_func, string? sender = null, Action<Exception>? on_getcontent_error = null) =>
            Dbug(getcontent_func, DateTime.Now, sender, on_getcontent_error);

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Debug Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>.
        /// </summary>
        /// <inheritdoc cref="PushLog(Func{string?}, LogLevel, DateTime, string?, Action{Exception}?)"/>
        public void Dbug(Func<string?> getcontent_func, DateTime logTime, string? sender = null, Action<Exception>? on_getcontent_error = null) =>
            PushCallbackLogEnumUnchecked(getcontent_func, LogLevel.Debug, logTime, sender, on_getcontent_error);

        /// <inheritdoc cref="Info(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public void Info(Func<string?> getcontent_func, string? sender = null, Action<Exception>? on_getcontent_error = null) =>
            Info(getcontent_func, DateTime.Now, sender, on_getcontent_error);

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Information Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>.
        /// </summary>
        /// <inheritdoc cref="PushLog(Func{string?}, LogLevel, DateTime, string?, Action{Exception}?)"/>
        public void Info(Func<string?> getcontent_func, DateTime logTime, string? sender = null, Action<Exception>? on_getcontent_error = null) =>
            PushCallbackLogEnumUnchecked(getcontent_func, LogLevel.Information, logTime, sender, on_getcontent_error);

        /// <inheritdoc cref="Warn(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public void Warn(Func<string?> getcontent_func, string? sender = null, Action<Exception>? on_getcontent_error = null) =>
            Warn(getcontent_func, DateTime.Now, sender, on_getcontent_error);

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Warning Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>.
        /// </summary>
        /// <inheritdoc cref="PushLog(Func{string?}, LogLevel, DateTime, string?, Action{Exception}?)"/>
        public void Warn(Func<string?> getcontent_func, DateTime logTime, string? sender = null, Action<Exception>? on_getcontent_error = null) =>
            PushCallbackLogEnumUnchecked(getcontent_func, LogLevel.Warning, logTime, sender, on_getcontent_error);

        /// <inheritdoc cref="Erro(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public void Erro(Func<string?> getcontent_func, string? sender = null, Action<Exception>? on_getcontent_error = null) =>
            Erro(getcontent_func, DateTime.Now, sender, on_getcontent_error);

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Error Level to the handle queue,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>.
        /// </summary>
        /// <inheritdoc cref="PushLog(Func{string?}, LogLevel, DateTime, string?, Action{Exception}?)"/>
        public void Erro(Func<string?> getcontent_func, DateTime logTime, string? sender = null, Action<Exception>? on_getcontent_error = null) =>
            PushCallbackLogEnumUnchecked(getcontent_func, LogLevel.Error, logTime, sender, on_getcontent_error);

        /// <inheritdoc cref="PushLog(Func{string?}, LogLevel, DateTime, string?, Action{Exception}?)"/>
        public void PushLog(Func<string?> getcontent_func, LogLevel logLevel, string? sender = null, Action<Exception>? on_getcontent_error = null) =>
            PushLog(getcontent_func, logLevel, DateTime.Now, sender, on_getcontent_error);

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
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, the logger will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        /// <inheritdoc cref="PushLog(string?, LogLevel, DateTime, string?)"/>
        public void PushLog(Func<string?> getcontent_func, LogLevel logLevel, DateTime logTime, string? sender = null, Action<Exception>? on_getcontent_error = null)
        {
            if (logLevel == LogLevel.None)
            {
                throw new InvalidOperationException("A log with LogLevel.None cannot be pushed and handled.");
            }
            if (!Enum.IsDefined(logLevel))
            {
                throw new ArgumentException("A log with an invalid level cannot be pushed and handled.", nameof(logLevel));
            }
            PushCallbackLogEnumUnchecked(getcontent_func, logLevel, logTime, sender, on_getcontent_error);
        }

        private void PushCallbackLogEnumUnchecked(Func<string?> getcontent_func, LogLevel logLevel, DateTime logTime, string? sender, Action<Exception>? on_getcontent_error)
        {
            if (CustomConfig.Global_Minimum_LogLevel <= logLevel)
            {
                EnqueueLog(new LogDetail(null, logLevel, sender, logTime,
                    getcontent_func, on_getcontent_error));
            }
        }
        #endregion

        #region Background Refresh
        internal struct LogDetail
        {
            public LogLevel level;
            public string? content;
            public string? sender;
            public DateTime create_time;
            public Func<string?>? getcontent_func;
            public Action<Exception>? on_getcontent_error;

            public LogDetail(string? con, LogLevel lvl, string? snd, DateTime create_time,
                Func<string?>? getcontent_func = null, Action<Exception>? on_getcontent_error = null)
            {
                level = lvl;
                content = con;
                sender = snd;
                this.create_time = create_time;
                this.getcontent_func = getcontent_func;
                this.on_getcontent_error = on_getcontent_error ?? new(ex =>
                {
                    Log.Warn($"Delay log content (pushed on {FormatTime(create_time)}) get failed: {ex}", snd);
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

            private ColorLineResult? _pipeSeparatedWriteResult = null;
            private ColorLineResult? _standardWriteResult = null;
            public ColorLineResult GetWriteFileResult(bool is_pipeseparated_format)
            {
                if (is_pipeseparated_format)
                {
                    _pipeSeparatedWriteResult ??= ColorLineUtil.AnalyzeColorText(
                        $"{FormatTime(create_time)}{GetFileLogInfo(level, sender, true)}{content}");
                    return _pipeSeparatedWriteResult.Value;
                }

                _standardWriteResult ??= ColorLineUtil.AnalyzeColorText(
                    $"{FormatTime(create_time)}{GetFileLogInfo(level, sender, false)}{content}");
                return _standardWriteResult.Value;
            }

            public ColorLineResult GetWriteConsoleResult(LoggerConfig conf)
            {
                string nowtime = FormatTime(create_time);
                string header = GetConsoleLogInfo(level, sender);
                string res = $"{nowtime}{header}{content}";

                if (conf.Max_Output_Char_Count < 0 || (content?.Length ?? 0) < conf.Max_Output_Char_Count)
                    return ColorLineUtil.AnalyzeColorText(res);
                else
                    return ColorLineUtil.AnalyzeColorText(
                        $"{nowtime}{header}[content too long (> Maximum line length of " +
                        $"{conf.Max_Output_Char_Count}), so not output to console]");
            }
        }

        private readonly ConcurrentQueue<LogDetail> qlog = new();
        private readonly object logQueueStateLock = new();
        private readonly SemaphoreSlim loggerSignal = new(0, 1);
        private readonly Task backgroundUpdateTask;
        private bool acceptingLogs = true;
        private int _refreshLogMilliseconds = 100;
        public int RefreshLogMilliseconds
        {
            get => Volatile.Read(ref _refreshLogMilliseconds);
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value),
                        "RefreshLogMilliseconds must be greater than zero.");
                Volatile.Write(ref _refreshLogMilliseconds, value);
                SignalLoggerWorker();
            }
        }
        private readonly Stopwatch watch = new();
        // see https://learn.microsoft.com/zh-cn/dotnet/api/system.threading.tasks.task.delay?view=net-7.0#system-threading-tasks-task-delay(system-timespan)
        private readonly TimeSpan waitStandard = TimeSpan.FromMilliseconds(15);

        private async Task BackgroundUpdate()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        if (qlog.IsEmpty)
                        {
                            if (IsLogProductionCompleted())
                                return;

                            await loggerSignal.WaitAsync(
                                TimeSpan.FromMilliseconds(RefreshLogMilliseconds)).ConfigureAwait(false);
                            continue;
                        }

                        watch.Restart();
                        InnerWriteLogs();
                        watch.Stop();

                        if (IsLogProductionCompleted() && qlog.IsEmpty)
                            return;

                        TimeSpan relaxSpan = TimeSpan.FromMilliseconds(RefreshLogMilliseconds) - watch.Elapsed;
                        if (relaxSpan >= waitStandard)
                            await loggerSignal.WaitAsync(relaxSpan).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // A single callback, file, or console failure must not
                        // permanently fault the producer task and hang exit.
                        Debug.WriteLine($"BaseLogger background worker failed: {ex}");
                        if (IsLogProductionCompleted() && qlog.IsEmpty)
                            return;
                        await Task.Delay(waitStandard).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                working_loggers.TryRemove(this, out _);
                Interlocked.Increment(ref cleanup_finished_count);
                SignalConsoleWriter();
            }
        }

        private void InnerWriteLogs()
        {
            while (qlog.TryDequeue(out LogDetail log))
            {
                workerLogProcessingDepth.Value++;
                try
                {
                    if (log.getcontent_func != null)
                    {
                        try
                        {
                            log.content = log.getcontent_func();
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                log.on_getcontent_error?.Invoke(ex);
                            }
                            catch (Exception handlerEx)
                            {
                                Debug.WriteLine($"Delayed log error handler failed: {handlerEx}");
                            }
                            continue;
                        }
                    }

                    if (log.level >= CustomConfig.Console_Minimum_LogLevel)
                    {
                        try
                        {
                            var consoleOutput = log.GetWriteConsoleResult(CustomConfig);
                            if (CustomConfig.Use_Console_Wrapper)
                            {
                                ConsoleWrapper.WriteLine(consoleOutput);
                            }
                            else
                            {
                                qconsole_strings.Enqueue(consoleOutput);
                                SignalConsoleWriter();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Log console rendering failed: {ex}");
                        }
                    }

                    foreach (var push_filestream in writeTargetFiles)
                    {
                        try
                        {
                            push_filestream.WriteLine(
                                log.GetWriteFileResult(push_filestream.IsPipeSeparatedFormat), log.level);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Log file write failed: {ex}");
                        }
                    }
                }
                finally
                {
                    workerLogProcessingDepth.Value--;
                    Interlocked.Decrement(ref outstanding_log_count);
                }
            }
        }

        private void SignalLoggerWorker()
        {
            try
            {
                loggerSignal.Release();
            }
            catch (SemaphoreFullException)
            {
                // A signal is already pending.
            }
        }

        private void EnqueueLog(LogDetail detail)
        {
            lock (loggerRegistrationLock)
            {
                bool acceptedWorkerCallback = workerLogProcessingDepth.Value > 0;
                if (global_log_production_completed ||
                    (_global_loggers_ending && !acceptedWorkerCallback))
                {
                    throw new InvalidOperationException(
                        "Global logger cleanup has started and no longer accepts external logs.");
                }

                lock (logQueueStateLock)
                {
                    if (!acceptingLogs)
                        throw new InvalidOperationException(
                            "This logger no longer accepts logs because its initialization was aborted or cleanup completed.");

                    Interlocked.Increment(ref outstanding_log_count);
                    try
                    {
                        qlog.Enqueue(detail);
                    }
                    catch
                    {
                        Interlocked.Decrement(ref outstanding_log_count);
                        throw;
                    }
                }
            }
            SignalLoggerWorker();
        }

        private void CompleteLogProduction()
        {
            lock (logQueueStateLock)
            {
                acceptingLogs = false;
            }
            SignalLoggerWorker();
        }

        private bool IsLogProductionCompleted()
        {
            lock (logQueueStateLock)
            {
                return !acceptingLogs;
            }
        }

        internal void AbortInitialization()
        {
            CompleteLogProduction();
            foreach (var fileStream in ownedWriteTargetFiles)
            {
                try
                {
                    fileStream.DisposeAndUnregister();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Rolling back a log file after failed initialization failed: {ex}");
                }
            }
            foreach (var fileStream in referencedWriteTargetFiles)
            {
                try
                {
                    fileStream.DisposeAndUnregister();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Releasing a referenced log file after failed initialization failed: {ex}");
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
