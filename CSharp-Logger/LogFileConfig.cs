namespace YYHEggEgg.Logger
{
    /// <summary>
    /// The config of creating a log file.
    /// </summary>
    public struct LogFileConfig
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

        public bool IsPipeSeparatedFile;

        public LogFileConfig()
        {
            MinimumLogLevel = null;
            MaximumLogLevel = null;
            AutoFlushWriter = true;
            FileIdentifier = null;
            IsPipeSeparatedFile = false;
        }
    }
}
