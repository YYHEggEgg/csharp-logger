using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace YYHEggEgg.Logger
{
    /// <summary>
    /// The config of creating a log file.
    /// </summary>
    public struct LogFileConfig : IEquatable<LogFileConfig>
    {
        /// <summary>
        /// The minimum <see cref="LogLevel"/> that can be written into
        /// this log file. But notice that it's inferior to
        /// <see cref="LoggerConfig.Global_Minimum_LogLevel"/> because
        /// the logs are totally ignored if logs don't reach it.
        /// </summary>
        public LogLevel? MinimumLogLevel;
        /// <summary>
        /// The maximum <see cref="LogLevel"/> that can be written into
        /// this log file. But notice that it's inferior to
        /// <see cref="LoggerConfig.Global_Minimum_LogLevel"/> because
        /// the logs are totally ignored if logs don't reach it.
        /// </summary>
        public LogLevel? MaximumLogLevel;
        /// <summary>
        /// Whether to flush the log file writer immediately after
        /// a log is handled. If you create a <see cref="BaseLogger"/>
        /// with both <see cref="LoggerConfig"/> and <see cref="LogFileConfig"/>,
        /// <see cref="LoggerConfig.Debug_LogWriter_AutoFlush"/> won't affect this.
        /// </summary>
        public bool AutoFlushWriter;
        /// <summary>
        /// The file identifier the log file is using. Generally,
        /// the created log file will be named as
        /// latest.<see cref="FileIdentifier"/>.log, as the identifier of
        /// <c>latest.debug.log</c> is <c>debug</c>.
        /// Specially, the identifier of <c>latest.log</c> is <c>global</c>.
        /// </summary>
        public string? FileIdentifier;
        /// <summary>
        /// Indicates whether this log file should be created in
        /// Pipe-separated values format (PSV). 
        /// </summary>
        public bool IsPipeSeparatedFile;
        /// <summary>
        /// If the log file with the same <see cref="FileIdentifier"/>
        /// exists, this determines whether to use the existing one
        /// (or raise an exception immediately).
        /// </summary>
        /// <remarks>
        /// However, when providing this with True, an exception will
        /// be raised if the two versions of <see cref="LogFileConfig"/>
        /// don't match.
        /// </remarks>
        public bool AllowAutoFallback;

        public LogFileConfig()
        {
            MinimumLogLevel = null;
            MaximumLogLevel = null;
            AutoFlushWriter = true;
            FileIdentifier = null;
            IsPipeSeparatedFile = false;
            AllowAutoFallback = false;
        }

        public bool Equals(LogFileConfig other)
        {
            Debug.Assert(FileIdentifier == other.FileIdentifier);
            Debug.Assert(MinimumLogLevel == other.MinimumLogLevel);
            Debug.Assert(MaximumLogLevel == other.MaximumLogLevel);
            Debug.Assert(AutoFlushWriter == other.AutoFlushWriter);
            Debug.Assert(IsPipeSeparatedFile == other.IsPipeSeparatedFile);
            return FileIdentifier == other.FileIdentifier &&
                MinimumLogLevel == other.MinimumLogLevel &&
                MaximumLogLevel == other.MaximumLogLevel &&
                AutoFlushWriter == other.AutoFlushWriter &&
                IsPipeSeparatedFile == other.IsPipeSeparatedFile;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            Debug.Assert(obj is LogFileConfig);
            Debug.Assert(obj is LogFileConfig && Equals((LogFileConfig)obj));
            return obj is LogFileConfig && Equals((LogFileConfig)obj);
        }

        public static bool operator ==(LogFileConfig left, LogFileConfig right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LogFileConfig left, LogFileConfig right)
        {
            return !(left == right);
        }

        public override int GetHashCode()
        {
            return (FileIdentifier?.GetHashCode() ?? 0) ^
                (MinimumLogLevel?.GetHashCode() ?? 0) ^
                (MaximumLogLevel?.GetHashCode() ?? 0) ^
                (AutoFlushWriter.GetHashCode()) ^
                IsPipeSeparatedFile.GetHashCode();
        }
    }
}
