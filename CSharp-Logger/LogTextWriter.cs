using System.Diagnostics;
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

        private readonly StringBuilder writebuf = new();

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LogTextWriter));
        }

        protected void InternalFlush_OnNewLine()
        {
            BasedLogger.PushLog(writebuf.ToString(), _logLevelWrite, LogSender);
            writebuf.Clear();
        }

        public override void Write(char value)
        {
            ThrowIfDisposed();
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
            try
            {
                if (disposing && writebuf.Length > 0)
                {
                    InternalFlush_OnNewLine();
                }
            }
            finally
            {
                _disposed = true;
                base.Dispose(disposing);
            }
        }

        public override void Flush()
        {
            ThrowIfDisposed();
            if (writebuf.Length > 0)
            {
                InternalFlush_OnNewLine();
            }
        }

        public override Task FlushAsync()
        {
            Flush();
            return Task.CompletedTask;
        }

#if NET8_0_OR_GREATER
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);
            return FlushAsync();
        }
#endif

        public override void Write(string? value)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(value)) return;

            int unflushedStart = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '\n') continue;

                if (i > unflushedStart)
                {
                    writebuf.Append(value, unflushedStart, i - unflushedStart);
                }
                InternalFlush_OnNewLine();
                unflushedStart = i + 1;
            }

            if (unflushedStart < value.Length)
            {
                writebuf.Append(value, unflushedStart, value.Length - unflushedStart);
            }
        }

        public override void Write(StringBuilder? value)
            => Write(value?.ToString());

        public override void WriteLine()
        {
            ThrowIfDisposed();
            InternalFlush_OnNewLine();
        }
    }
}
