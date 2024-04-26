namespace YYHEggEgg.Logger;

public static class LoggerChannelTraceExtensions
{
    private static object _exception_lck = "<3 from miHomo Technology";
    private static Dictionary<string, Guid> _trace_ids = new();
    private static int _total_characters = 0;
    /// <summary>
    /// Set the maximum chars count of exception content stored in the memory. LogTrace use it to reuse trace-ids for exceptions with the same content.
    /// </summary>
    public static int MaximumStoredChars { get; set; } = 4 * 1024 * 1024;
    private static bool _initialized = false;
    private static BaseLogger? _tracelog;

    public const string ErrTraceLogFileIdentifier = "errtrace";

    private static void Initialize()
    {
        lock (_exception_lck)
        {
            if (_initialized) return;

            var inheritingLoggerConfig = Log.GlobalConfig;
            inheritingLoggerConfig.Global_Minimum_LogLevel = LogLevel.Information;
            inheritingLoggerConfig.Console_Minimum_LogLevel = LogLevel.None;
            inheritingLoggerConfig.Debug_LogWriter_AutoFlush = true;
            inheritingLoggerConfig.Is_PipeSeparated_Format = false;

            _tracelog = new BaseLogger(inheritingLoggerConfig, new LogFileConfig
            {
                MinimumLogLevel = LogLevel.Information,
                MaximumLogLevel = LogLevel.Information,
                AutoFlushWriter = true,
                FileIdentifier = ErrTraceLogFileIdentifier,
                IsPipeSeparatedFile = false,
                AllowAutoFallback = true,
            });
            _initialized = true;
        }
    }

    private static (Guid traceid, bool isnew) AssignTraceId(string ex_full_content)
    {
        (Guid traceid, bool isnew) rtn;
        lock (_exception_lck)
        {
            Guid? cleared = null;
            _total_characters += ex_full_content.Length;
            if (_total_characters > MaximumStoredChars)
            {
                cleared = _trace_ids[ex_full_content];
                _trace_ids.Clear();
            }

            if (!_trace_ids.TryGetValue(ex_full_content, out Guid id))
            {
                rtn = (Guid.NewGuid(), true);
                _trace_ids.Add(ex_full_content, rtn.traceid);
                return rtn;
            }

            if (cleared != null)
                _trace_ids.Add(ex_full_content, cleared.Value);
            rtn = (id, false);
            return rtn;
        }
    }

    /// <summary>
    /// Output <see cref="Exception.Message"/> and <paramref name="prompt"/>
    /// with <paramref name="logLevel"/> to <paramref name="logger"/>，
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid LogExceptionTrace(this LoggerChannel logger,
        Exception? ex, LogLevel logLevel, string? prompt = null)
    {
        Initialize();
        #region Fuck
        // nari don't like TargetInvocationException
        if (ex is System.Reflection.TargetInvocationException tiex)
        {
            ex = tiex.InnerException;
        }
        #endregion

        var traceinfo = ex?.ToString() ?? string.Empty;
        var extype = ex?.GetType().ToString() ?? "<Unknown exception>";
        var exmsg = ex?.Message ?? "<Exception message missing>";
        (Guid traceid, bool isnew) = AssignTraceId(traceinfo);

        logger.LogPush($"{prompt} {extype}: {exmsg} (Trace ID: {traceid})",
            logLevel);
        if (isnew) _tracelog?.Info($"Trace ID [{traceid}]: \n{traceinfo}", logger.LogSender);
        return traceid;
    }

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Error"/> to <paramref name="logger"/>,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid LogErroTrace(this LoggerChannel logger,
        Exception? ex, string? prompt = null)
        => LogExceptionTrace(logger, ex, LogLevel.Error, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Warning"/> to <paramref name="logger"/>,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid LogWarnTrace(this LoggerChannel logger,
        Exception? ex, string? prompt = null)
        => LogExceptionTrace(logger, ex, LogLevel.Warning, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Information"/> to <paramref name="logger"/>,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid LogInfoTrace(this LoggerChannel logger,
        Exception? ex, string? prompt = null)
        => LogExceptionTrace(logger, ex, LogLevel.Information, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Debug"/> to <paramref name="logger"/>,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid LogDbugTrace(this LoggerChannel logger,
        Exception? ex, string? prompt = null)
        => LogExceptionTrace(logger, ex, LogLevel.Debug, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Verbose"/> to <paramref name="logger"/>,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid LogVerbTrace(this LoggerChannel logger,
        Exception? ex, string? prompt = null)
        => LogExceptionTrace(logger, ex, LogLevel.Verbose, prompt);
}

public static class BaseLoggerTraceExtensions
{
    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// a given <see cref="LogLevel"/> to the global main logger,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <param name="sender">The sender param used by logging.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid ExceptionTrace(this BaseLogger logger,
        Exception? ex, LogLevel logLevel, string? sender = null, string? prompt = null)
        => logger.GetChannel(sender).LogExceptionTrace(ex, logLevel, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Error"/> to the global main logger,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <param name="sender">The sender param used by logging.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid ErroTrace(this BaseLogger logger,
        Exception? ex, string? sender = null, string? prompt = null)
        => logger.GetChannel(sender).LogErroTrace(ex, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Warning"/> to the global main logger,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <param name="sender">The sender param used by logging.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid WarnTrace(this BaseLogger logger,
        Exception? ex, string? sender = null, string? prompt = null)
        => logger.GetChannel(sender).LogWarnTrace(ex, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Information"/> to the global main logger,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <param name="sender">The sender param used by logging.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid InfoTrace(this BaseLogger logger,
        Exception? ex, string? sender = null, string? prompt = null)
        => logger.GetChannel(sender).LogInfoTrace(ex, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Debug"/> to the global main logger,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <param name="sender">The sender param used by logging.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid DbugTrace(this BaseLogger logger,
        Exception? ex, string? sender = null, string? prompt = null)
        => logger.GetChannel(sender).LogDbugTrace(ex, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Verbose"/> to the global main logger,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <param name="sender">The sender param used by logging.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid VerbTrace(this BaseLogger logger,
        Exception? ex, string? sender = null, string? prompt = null)
        => logger.GetChannel(sender).LogVerbTrace(ex, prompt);
}

public static class LogTrace
{
    /// <inheritdoc cref="LoggerChannelTraceExtensions.MaximumStoredChars"/>
    public static int MaximumStoredChars
    {
         get => LoggerChannelTraceExtensions.MaximumStoredChars;
         set => LoggerChannelTraceExtensions.MaximumStoredChars = value;
    }

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// a given <see cref="LogLevel"/> to the global main logger,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <param name="sender">The sender param used by logging.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid ExceptionTrace(
        Exception? ex, LogLevel logLevel, string? sender = null, string? prompt = null)
        => Log.GlobalBasedLogger.ExceptionTrace(ex, logLevel, sender, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Error"/> to the global main logger,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <param name="sender">The sender param used by logging.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid ErroTrace(
        Exception? ex, string? sender = null, string? prompt = null)
        => Log.GlobalBasedLogger.ErroTrace(ex, sender, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Warning"/> to the global main logger,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <param name="sender">The sender param used by logging.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid WarnTrace(
        Exception? ex, string? sender = null, string? prompt = null)
        => Log.GlobalBasedLogger.WarnTrace(ex, sender, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Information"/> to the global main logger,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <param name="sender">The sender param used by logging.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid InfoTrace(
        Exception? ex, string? sender = null, string? prompt = null)
        => Log.GlobalBasedLogger.InfoTrace(ex, sender, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Debug"/> to the global main logger,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <param name="sender">The sender param used by logging.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid DbugTrace(
        Exception? ex, string? sender = null, string? prompt = null)
        => Log.GlobalBasedLogger.DbugTrace(ex, sender, prompt);

    /// <summary>
    /// Output <see cref="Exception.Message"/> with
    /// <see cref="LogLevel.Verbose"/> to the global main logger,
    /// and output the extra detailed exception trace to
    /// <c>latest.errtrace.log</c>.
    /// </summary>
    /// <param name="ex">The exception caught in the program.</param>
    /// <param name="prompt">The summary of where the exception is caught.</param>
    /// <param name="sender">The sender param used by logging.</param>
    /// <returns>The found or assigned Trace ID.</returns>
    public static Guid VerbTrace(
        Exception? ex, string? sender = null, string? prompt = null)
        => Log.GlobalBasedLogger.VerbTrace(ex, sender, prompt);
}
