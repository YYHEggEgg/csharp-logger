using System.Diagnostics;

namespace YYHEggEgg.Logger
{
    public class LogTraceListener : TraceListener
    {
        public override bool IsThreadSafe => true;
        public string LogSender { get; set; }
        /// <summary>
        /// The LogLevel used for invoking <see cref="TraceListener.Write(string?)"/>.
        /// </summary>
        public LogLevel LogLevelWrite;
        /// <summary>
        /// The LogLevel used for invoking <see cref="TraceListener.Fail(string?)"/>.
        /// </summary>
        public readonly LogLevel LogLevelFail;
        public readonly BaseLogger BasedLogger;

        /// <summary>
        /// The .ctor of <see cref="LogTraceListener"/> instance.
        /// </summary>
        /// <param name="logSender">The log sender when <see cref="TraceListener"/> is invoked.</param>
        /// <param name="logLevelWrite">The LogLevel used for invoking <see cref="TraceListener.Write(string?)"/>.</param>
        /// <param name="logLevelFail">The LogLevel used for invoking <see cref="TraceListener.Fail(string?)"/>.</param>
        public LogTraceListener(string logSender = "TraceListener",
            LogLevel logLevelWrite = LogLevel.Information, LogLevel logLevelFail = LogLevel.Error,
            BaseLogger? basedlogger = null)
        {
            NeedIndent = false;
            LogSender = logSender;
            LogLevelWrite = logLevelWrite;
            LogLevelFail = logLevelFail;
            BasedLogger = basedlogger ?? Log.GlobalBasedLogger;
        }

        public override void Fail(string? message)
        {
            if (message != null) BasedLogger.PushLog(message, LogLevelFail, LogSender);
        }

        public override void Fail(string? message, string? detailMessage)
        {
            if (message != null) BasedLogger.PushLog(message, LogLevelFail, LogSender);
            if (detailMessage != null) BasedLogger.PushLog(detailMessage, LogLevelFail, LogSender);
        }

        public override void Write(object? o)
        {
            if (o != null) BasedLogger.PushLog(o.ToString() ?? "", LogLevelWrite, LogSender);
        }

        public override void Write(string? message, string? category)
        {
            if (message != null) BasedLogger.PushLog(message, LogLevelWrite, $"{LogSender}:{category}");
        }

        public override void Write(string? message)
        {
            if (message != null) BasedLogger.PushLog(message, LogLevelWrite, LogSender);
        }

        public override void Write(object? o, string? category)
        {
            if (o != null) BasedLogger.PushLog(o.ToString() ?? "", LogLevelWrite, $"{LogSender}:{category}");
        }

        public override void WriteLine(string? message)
        {
            if (message != null) BasedLogger.PushLog(message, LogLevelWrite, LogSender);
        }

        public override void WriteLine(string? message, string? category)
        {
            if (message != null) BasedLogger.PushLog(message, LogLevelWrite, $"{LogSender}:{category}");
        }

        public override void WriteLine(object? o, string? category)
        {
            if (o != null) BasedLogger.PushLog(o.ToString() ?? "", LogLevelWrite, $"{LogSender}:{category}");
        }
    }
}
