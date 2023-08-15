﻿namespace YYHEggEgg.Logger
{
    /// <summary>
    /// The config of creating a log file.
    /// </summary>
    public struct LogFileConfig
    {
        public LogLevel MinimumLogLevel, MaximumLogLevel;
        public bool AutoFlushWriter;
        public string FileIdentifier;
    }
}