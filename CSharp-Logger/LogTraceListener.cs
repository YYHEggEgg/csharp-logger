using System.Diagnostics;

namespace YYHEggEgg.Logger
{
    public class LogTraceListener : TraceListener
    {
        public override bool IsThreadSafe => true;
        public string LogSender { get; set; }
        private LogLevel _logLevelWrite;
        /// <summary>
        /// The LogLevel used for invoking <see cref="TraceListener.Write(string?)"/>.
        /// </summary>
        public LogLevel LogLevelWrite 
        {
            get => _logLevelWrite;
            set
            {
                if (!Enum.IsDefined(value))
                {
                    throw new InvalidOperationException("Logs with an invalid level cannot be pushed and handled.");
                }
                _logLevelWrite = value;
            }
        }
        private LogLevel _logLevelFail;
        /// <summary>
        /// The LogLevel used for invoking <see cref="TraceListener.Fail(string?)"/>.
        /// </summary>
        public LogLevel LogLevelFail
        {
            get => _logLevelFail;
            set
            {
                if (!Enum.IsDefined(value))
                {
                    throw new InvalidOperationException("Logs with an invalid level cannot be pushed and handled.");
                }
                _logLevelFail = value;
            }
        }
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
            if (message != null) BasedLogger.PushLog(message, _logLevelFail, LogSender);
        }

        public override void Fail(string? message, string? detailMessage)
        {
            if (message != null) BasedLogger.PushLog(message, _logLevelFail, LogSender);
            if (detailMessage != null) BasedLogger.PushLog(detailMessage, _logLevelFail, LogSender);
        }

        public override void Write(object? o)
        {
            if (o != null) BasedLogger.PushLog(o.ToString() ?? "", _logLevelWrite, LogSender);
        }

        public override void Write(string? message, string? category)
        {
            if (message != null) BasedLogger.PushLog(message, _logLevelWrite, $"{LogSender}:{category}");
        }

        public override void Write(string? message)
        {
            if (message != null) BasedLogger.PushLog(message, _logLevelWrite, LogSender);
        }

        public override void Write(object? o, string? category)
        {
            if (o != null) BasedLogger.PushLog(o.ToString() ?? "", _logLevelWrite, $"{LogSender}:{category}");
        }

        public override void WriteLine(string? message)
        {
            if (message != null) BasedLogger.PushLog(message, _logLevelWrite, LogSender);
        }

        public override void WriteLine(string? message, string? category)
        {
            if (message != null) BasedLogger.PushLog(message, _logLevelWrite, $"{LogSender}:{category}");
        }

        public override void WriteLine(object? o, string? category)
        {
            if (o != null) BasedLogger.PushLog(o.ToString() ?? "", _logLevelWrite, $"{LogSender}:{category}");
        }
    }
}
