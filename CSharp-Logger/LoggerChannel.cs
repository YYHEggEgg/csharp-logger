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

        /// <summary>
        /// Put a log with Verbose Level to <see cref="TargetLogger"/>.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Verbose"/>.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>ded to use <see cref="nameof"/> to provide this param.</param>
        public void LogVerb(string content) => TargetLogger.Verb(content, LogSender);

        /// <summary>
        /// Put a log with Debug Level to <see cref="TargetLogger"/>.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Debug"/>.
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>ded to use <see cref="nameof"/> to provide this param.</param>
        public void LogDbug(string content) => TargetLogger.Dbug(content, LogSender);

        /// <summary>
        /// Put a log with Info Level to <see cref="TargetLogger"/>.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Information"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>ded to use <see cref="nameof"/> to provide this param.</param>
        public void LogInfo(string content) => TargetLogger.Info(content, LogSender);

        /// <summary>
        /// Put a log with Warning Level to <see cref="TargetLogger"/>.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Warning"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>ded to use <see cref="nameof"/> to provide this param.</param>
        public void LogWarn(string content) => TargetLogger.Warn(content, LogSender);

        /// <summary>
        /// Put a log with Error Level to <see cref="TargetLogger"/>.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <see cref="LogLevel.Error"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>ded to use <see cref="nameof"/> to provide this param.</param>
        public void LogErro(string content) => TargetLogger.Erro(content, LogSender);

        /// <summary>
        /// Put a log with a certain Level to <see cref="TargetLogger"/>.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="content">The log content.</param>
        /// <param name="logLevel">The <see cref="LogLevel"/> of this log message.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        public void LogPushLog(string content, LogLevel logLevel) => TargetLogger.PushLog(content, logLevel, LogSender);
        
        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Verbose Level to <see cref="TargetLogger"/>,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="getcontent_func">
        /// The func used to get the log content. It'll be invoked afterwards by
        /// <see cref="TargetLogger"/>'s background refreshing task.
        /// </param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, <see cref="TargetLogger"/> will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        public void LogVerb(Func<string> getcontent_func,
            Action<Exception>? on_getcontent_error = null)
            => TargetLogger.Verb(getcontent_func, LogSender, on_getcontent_error);

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Debug Level to <see cref="TargetLogger"/>,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="getcontent_func">
        /// The func used to get the log content. It'll be invoked afterwards by
        /// <see cref="TargetLogger"/>'s background refreshing task.
        /// </param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, <see cref="TargetLogger"/> will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        public void LogDbug(Func<string> getcontent_func,
            Action<Exception>? on_getcontent_error = null)
            => TargetLogger.Dbug(getcontent_func, LogSender, on_getcontent_error);

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Information Level to <see cref="TargetLogger"/>,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="getcontent_func">
        /// The func used to get the log content. It'll be invoked afterwards by
        /// <see cref="TargetLogger"/>'s background refreshing task.
        /// </param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, <see cref="TargetLogger"/> will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        public void LogInfo(Func<string> getcontent_func,
            Action<Exception>? on_getcontent_error = null)
            => TargetLogger.Info(getcontent_func, LogSender, on_getcontent_error);

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Warning Level to <see cref="TargetLogger"/>,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="getcontent_func">
        /// The func used to get the log content. It'll be invoked afterwards by
        /// <see cref="TargetLogger"/>'s background refreshing task.
        /// </param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, <see cref="TargetLogger"/> will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        public void LogWarn(Func<string> getcontent_func,
            Action<Exception>? on_getcontent_error = null)
            => TargetLogger.Warn(getcontent_func, LogSender, on_getcontent_error);

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with Error Level to <see cref="TargetLogger"/>,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="getcontent_func">
        /// The func used to get the log content. It'll be invoked afterwards by
        /// <see cref="TargetLogger"/>'s background refreshing task.
        /// </param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, <see cref="TargetLogger"/> will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        public void LogErro(Func<string> getcontent_func,
            Action<Exception>? on_getcontent_error = null)
            => TargetLogger.Erro(getcontent_func, LogSender, on_getcontent_error);

        /// <summary>
        /// Put a <see cref="Func{TResult}"/> with a certain Level to <see cref="TargetLogger"/>,
        /// and invoke the func afterwards to get the content.
        /// Only handled when <see cref="LoggerConfig.Global_Minimum_LogLevel"/>
        /// is no more than <paramref name="logLevel"/>. 
        /// </summary>
        /// <param name="getcontent_func">
        /// The func used to get the log content. It'll be invoked afterwards by
        /// <see cref="TargetLogger"/>'s background refreshing task.
        /// </param>
        /// <param name="logLevel">The <see cref="LogLevel"/> of this log message.</param>
        /// <param name="sender">The sender of this log. It's recommended to use <see cref="nameof"/> to provide this param.</param>
        /// <param name="on_getcontent_error">
        /// The Action used to handle the error in <paramref name="getcontent_func"/>.
        /// If not providing it, <see cref="TargetLogger"/> will report the exception to
        /// <see cref="Log.Warn(string, string?)"/> in a certain format.
        /// </param>
        public void LogPushLog(Func<string> getcontent_func, LogLevel logLevel, 
            Action<Exception>? on_getcontent_error = null)
            => TargetLogger.PushLog(getcontent_func, logLevel, LogSender, on_getcontent_error);
    }
}
