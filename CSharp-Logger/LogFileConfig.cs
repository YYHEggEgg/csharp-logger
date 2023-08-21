namespace YYHEggEgg.Logger
{
    /// <summary>
    /// The config of creating a log file.
    /// </summary>
    public struct LogFileConfig
    {
        public LogLevel MinimumLogLevel, MaximumLogLevel;
        public bool AutoFlushWriter;
        public string FileIdentifier;

        public bool IsPipeSeparatedFile;

        public LogFileConfig()
        {
            MinimumLogLevel = LogLevel.Verbose;
            MaximumLogLevel = LogLevel.Error;
            AutoFlushWriter = true;
            FileIdentifier = string.Empty;
            IsPipeSeparatedFile = false;
        }
    }
}
