﻿using System.Diagnostics;
using System.Text;

namespace YYHEggEgg.Logger
{
    /// <summary>
    /// Implements a <see cref="TextWriter"/> that output text with log. It is NOT thread-safe.
    /// </summary>
    public class LogTextWriter : TextWriter
    {
        public string? LogSender;
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
        public readonly BaseLogger BasedLogger;

        public LogTextWriter(string? logSender = nameof(LogTextWriter), 
            LogLevel logLevelWrite = LogLevel.Information, BaseLogger? basedlogger = null)
        {
            LogSender = logSender;
            LogLevelWrite = logLevelWrite;
            BasedLogger = basedlogger ?? Log.GlobalBasedLogger;
        }

        public override Encoding Encoding => Encoding.UTF8;

        private StringBuilder writebuf = new();

        protected void InternalFlush_OnNewLine()
        {
            BasedLogger.PushLog(writebuf.ToString(), _logLevelWrite, LogSender);
            writebuf.Clear();
        }

        public override void Write(char value)
        {
            if (value == '\n')
            {
                InternalFlush_OnNewLine();
            }
            else writebuf.Append(value);
        }

        public override string NewLine
        {
            get => "\n";
#pragma warning disable CS8765 // 参数类型的为 Null 性与重写成员不匹配(可能是由于为 Null 性特性)。
            set => throw new NotSupportedException("CSharp-Logger currently only support '\\n' as the line terminator.");
#pragma warning restore CS8765 // 参数类型的为 Null 性与重写成员不匹配(可能是由于为 Null 性特性)。
        }

        private bool _disposed = false;

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _disposed = true;
                InternalFlush_OnNewLine();
            }
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var lines = value.Split('\n');
            if (value.StartsWith('\n')) InternalFlush_OnNewLine();
            for (int i = 0; i < lines.Length - 1; i++)
            {
                var line = lines[i];
                if (i == 0)
                {
                    writebuf.Append(line);
                    InternalFlush_OnNewLine();
                }
                else
                {
                    Debug.Assert(writebuf.Length == 0);
                    BasedLogger.PushLog(line, _logLevelWrite, LogSender);
                }
            }
            if (lines.Length > 0)
            {
                string last_line = lines[lines.Length - 1];
                writebuf.Append(last_line);
            }
            if (value.EndsWith('\n') && value.Length > 1) InternalFlush_OnNewLine();
        }

        public override void Write(StringBuilder? value)
            => Write(value?.ToString());

        public override void WriteLine()
            => InternalFlush_OnNewLine();
    }
}
