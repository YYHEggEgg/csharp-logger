namespace YYHEggEgg.Logger
{
    /// <summary>
    /// Provide a 'logger' implement for those code pushing logs always with the same sender paramter.
    /// </summary>
    public class LoggerChannel
    {
        public readonly BaseLogger TargetLogger;
        public string? LogSender = null;

        public LoggerChannel(BaseLogger targetLogger, string? globalSender)
        {
            TargetLogger = targetLogger ?? throw new ArgumentNullException(nameof(targetLogger));
            LogSender = globalSender;
        }

        /// <inheritdoc cref="BaseLogger.Verb(string?, string?)"/>
        public void LogVerb(string? content) => TargetLogger.Verb(content, LogSender);

        /// <inheritdoc cref="BaseLogger.Verb(string?, DateTime, string?)"/>
        public void LogVerb(string? content, DateTime logTime) => TargetLogger.Verb(content, logTime, LogSender);

        /// <inheritdoc cref="BaseLogger.Dbug(string?, string?)"/>
        public void LogDbug(string? content) => TargetLogger.Dbug(content, LogSender);

        /// <inheritdoc cref="BaseLogger.Dbug(string?, DateTime, string?)"/>
        public void LogDbug(string? content, DateTime logTime) => TargetLogger.Dbug(content, logTime, LogSender);

        /// <inheritdoc cref="BaseLogger.Info(string?, string?)"/>
        public void LogInfo(string? content) => TargetLogger.Info(content, LogSender);

        /// <inheritdoc cref="BaseLogger.Info(string?, DateTime, string?)"/>
        public void LogInfo(string? content, DateTime logTime) => TargetLogger.Info(content, logTime, LogSender);

        /// <inheritdoc cref="BaseLogger.Warn(string?, string?)"/>
        public void LogWarn(string? content) => TargetLogger.Warn(content, LogSender);

        /// <inheritdoc cref="BaseLogger.Warn(string?, DateTime, string?)"/>
        public void LogWarn(string? content, DateTime logTime) => TargetLogger.Warn(content, logTime, LogSender);

        /// <inheritdoc cref="BaseLogger.Erro(string?, string?)"/>
        public void LogErro(string? content) => TargetLogger.Erro(content, LogSender);

        /// <inheritdoc cref="BaseLogger.Erro(string?, DateTime, string?)"/>
        public void LogErro(string? content, DateTime logTime) => TargetLogger.Erro(content, logTime, LogSender);

        /// <inheritdoc cref="BaseLogger.PushLog(string?, LogLevel, string?)"/>
        public void LogPush(string? content, LogLevel logLevel) => TargetLogger.PushLog(content, logLevel, LogSender);

        /// <inheritdoc cref="BaseLogger.PushLog(string?, LogLevel, DateTime, string?)"/>
        public void LogPush(string? content, LogLevel logLevel, DateTime logTime) => TargetLogger.PushLog(content, logLevel, logTime, LogSender);

        /// <inheritdoc cref="BaseLogger.Verb(Func{string?}, string?, Action{Exception}?)"/>
        public void LogVerb(Func<string?> getcontent_func, Action<Exception>? on_getcontent_error = null) => TargetLogger.Verb(getcontent_func, LogSender, on_getcontent_error);

        /// <inheritdoc cref="BaseLogger.Verb(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public void LogVerb(Func<string?> getcontent_func, DateTime logTime, Action<Exception>? on_getcontent_error = null) => TargetLogger.Verb(getcontent_func, logTime, LogSender, on_getcontent_error);

        /// <inheritdoc cref="BaseLogger.Dbug(Func{string?}, string?, Action{Exception}?)"/>
        public void LogDbug(Func<string?> getcontent_func, Action<Exception>? on_getcontent_error = null) => TargetLogger.Dbug(getcontent_func, LogSender, on_getcontent_error);

        /// <inheritdoc cref="BaseLogger.Dbug(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public void LogDbug(Func<string?> getcontent_func, DateTime logTime, Action<Exception>? on_getcontent_error = null) => TargetLogger.Dbug(getcontent_func, logTime, LogSender, on_getcontent_error);

        /// <inheritdoc cref="BaseLogger.Info(Func{string?}, string?, Action{Exception}?)"/>
        public void LogInfo(Func<string?> getcontent_func, Action<Exception>? on_getcontent_error = null) => TargetLogger.Info(getcontent_func, LogSender, on_getcontent_error);

        /// <inheritdoc cref="BaseLogger.Info(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public void LogInfo(Func<string?> getcontent_func, DateTime logTime, Action<Exception>? on_getcontent_error = null) => TargetLogger.Info(getcontent_func, logTime, LogSender, on_getcontent_error);

        /// <inheritdoc cref="BaseLogger.Warn(Func{string?}, string?, Action{Exception}?)"/>
        public void LogWarn(Func<string?> getcontent_func, Action<Exception>? on_getcontent_error = null) => TargetLogger.Warn(getcontent_func, LogSender, on_getcontent_error);

        /// <inheritdoc cref="BaseLogger.Warn(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public void LogWarn(Func<string?> getcontent_func, DateTime logTime, Action<Exception>? on_getcontent_error = null) => TargetLogger.Warn(getcontent_func, logTime, LogSender, on_getcontent_error);

        /// <inheritdoc cref="BaseLogger.Erro(Func{string?}, string?, Action{Exception}?)"/>
        public void LogErro(Func<string?> getcontent_func, Action<Exception>? on_getcontent_error = null) => TargetLogger.Erro(getcontent_func, LogSender, on_getcontent_error);

        /// <inheritdoc cref="BaseLogger.Erro(Func{string?}, DateTime, string?, Action{Exception}?)"/>
        public void LogErro(Func<string?> getcontent_func, DateTime logTime, Action<Exception>? on_getcontent_error = null) => TargetLogger.Erro(getcontent_func, logTime, LogSender, on_getcontent_error);

        /// <inheritdoc cref="BaseLogger.PushLog(Func{string?}, LogLevel, string?, Action{Exception}?)"/>
        public void LogPush(Func<string?> getcontent_func, LogLevel logLevel, Action<Exception>? on_getcontent_error = null) => TargetLogger.PushLog(getcontent_func, logLevel, LogSender, on_getcontent_error);

        /// <inheritdoc cref="BaseLogger.PushLog(Func{string?}, LogLevel, DateTime, string?, Action{Exception}?)"/>
        public void LogPush(Func<string?> getcontent_func, LogLevel logLevel, DateTime logTime, Action<Exception>? on_getcontent_error = null) => TargetLogger.PushLog(getcontent_func, logLevel, logTime, LogSender, on_getcontent_error);
    }
}
