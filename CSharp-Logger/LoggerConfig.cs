using System.Reflection.Metadata;
using System;
using System.Runtime.CompilerServices;

namespace YYHEggEgg.Logger
{
    public struct LoggerConfig
    {
        /// <summary>
        /// The maximum line length to output to the console. <para/>
        /// If the length of the line is longer than this value, it won't be output to console and will be replaced by a tip.<para/>
        /// The default value is 16KB. Set it to negative value or 0 to disable the feature.
        /// </summary>
        public int Max_Output_Char_Count = 16 * 1024;
        /// <summary>
        /// Whether the Logger uses <see cref="Console"/> or <see cref="ConsoleWrapper"/> implements. <para/>
        /// Generally, <see cref="Console"/> is enough. But if you wants to read the user's input while outputing logs parallel (e.g. making a command line program), you'll need <see cref="ConsoleWrapper"/>.<para/>
        /// If you decided to use <see cref="ConsoleWrapper"/>, you can set the value of <see cref="ConsoleWrapper.InputPrefix"/> as a waiting-input prefix, just like <c>mysql> </c> or <c>ubuntu ~$ </c>, and use <see cref="ConsoleWrapper.ReadLineAsync"/> to read inputs from the user.<para/>
        /// But if your program won't output anything until get input from the user, or <b>the input string is expected to be very large</b>, you should use common <see cref="Console"/> implements.<para/>
        /// No matter you decide to use either <see cref="Console"/> or <see cref="ConsoleWrapper"/>, <b>DON'T MIX THEM!</b> The side-effect has never been tested!
        /// </summary>
        public bool Use_Console_Wrapper = false;
        /// <summary>
        /// Whether the log files will be storaged at the working directory or the program path.<para/>
        /// The working directory is equal to <see cref="Environment.CurrentDirectory"/>. <para/>
        /// If you start the program in explorer, it will be set to where the program at. If you start it in bash/terminal, it will be set to the present path of the Console. It can have massive effects. <para/>
        /// We recommend to use program path, or <see cref="Environment.ProcessPath"/>. It will always be where the program at. <para/>
        /// e.g. If you invoke the program with command <c>PS C:\> dotnet ExampleProgram\ExampleProgram.dll</c>, <see cref="Environment.CurrentDirectory"/> will be set to <c>C:\</c>, and <see cref="Environment.ProcessPath"/> will be set to <c>C:\ExampleProgram</c>.<para/>
        /// But actually, working directory is changeable while running (as <see cref="Environment.CurrentDirectory"/> is writeable), but the log file path <b>won't change since <see cref="Log.Initialize(LoggerConfig)"/> is invoked,</b> for it maintains a <see cref="StreamWriter"/> instance until the process is terminated.<para/>
        /// Notice that Logger will still use the working directory when <see cref="Environment.ProcessPath"/> isn't accessible, or more clearly, when <see cref="Environment.ProcessPath"/><c> == null</c>. If it happens, you'll receive a warning. 
        /// </summary>
        public bool Use_Working_Directory = true;
        /// <summary>
        /// The minimum <see cref="LogLevel"/> that will be handled. <para/>
        /// Log messages with smaller <see cref="LogLevel"/> will be ignored at all.
        /// </summary>
        public LogLevel Global_Minimum_LogLevel = LogLevel.Debug;
        /// <summary>
        /// The minimum LogLevel that will be output to the console. <para/>
        /// Log messages with smaller <see cref="LogLevel"/> won't be output to console, but will exist in the log file. <para/>
        /// Notice that this cannot be smaller than <see cref="Global_Minimum_LogLevel"/>.
        /// </summary>
        public LogLevel Console_Minimum_LogLevel = LogLevel.Information;
        /// <summary>
        /// Whether the debug.log writer will flush automatically. It decides the value of <see cref="StreamWriter.AutoFlush"/>. <para/>
        /// Some programs has a high amount of Verbose output, so they may not have the writer flush immediately. <para/>
        /// Most programs need a immediate output and don't care about performance loss caused by this. <para/>
        /// Though it's recommended to set it to true, the default value is false because of compatiable reasons. <para/>
        /// For now, the unflushed parts of the log will be lost. More optimization will come in further versions. 
        /// </summary>
        public bool Debug_LogWriter_AutoFlush = false;
        /// <summary>
        /// Indicates whether latest.log and latest.debug.log should be created in
        /// Pipe-separated values format (PSV). Outputting logs as a table helps
        /// with analysis when dealing with large amounts of data, especially if
        /// the Log method is called by modularized code that does not change the
        /// sender parameter.<para/>
        /// However, because Logger does not guarantee that the content does not contain
        /// characters that affect the PSV file format reading (such as line breaks),
        /// this option is generally not enabled. Instead, you should create a new
        /// instance of <see cref="BaseLogger"/> and a separate log file by calling
        /// <see cref="BaseLogger(LoggerConfig, LogFileConfig)"/>, and enable
        /// <see cref="LogFileConfig.IsPipeSeparatedFile"/> specifically for storing
        /// data in PSV format.<para/>
        /// This configuration does not affect the content output to the console,
        /// and currently only accepts <c>|</c> as the separator for performance reasons.
        /// </summary>
        public bool Is_PipeSeparated_Format = false;
        /// <summary>
        /// Indicates whether to enable the time detail of the log.
        /// By default, the time recorded by Logger is only accurate to the second
        /// and does not include the date, corresponding to the formatted string
        /// <c>HH:mm:ss</c>。<para/>
        /// After enabling time details, it will display the details of the log
        /// submission time up to one-seventh of a second, and the corresponding
        /// formatted string is <c>yyyy-MM-dd HH:mm:ss fff ffff</c>, the two parts
        /// <c>fff</c> and <c>ffff</c> represent the millisecond level and the ten-thousandth
        /// of a millisecond (100 nanoseconds, 0.1 microseconds) level, such as
        /// <c>2023-08-22 15:43:36 456 4362</c>.<para/>
        /// This configuration requires global unity and is effective for both console
        /// and log file output.
        /// </summary>
        public bool Enable_Detailed_Time = false;
        /// <summary>
        /// Determines whether to enable disk operations.<para/>
        /// When set to <c>true</c>, any disk interactions will be prohibited (including
        /// previous log files compression, default log files creation).<para/>
        /// If this option is enabled under a global scope, any attempts to create
        /// another log file explicitly (using
        /// <see cref="BaseLogger(LoggerConfig, LogFileConfig)"/>) and making contradictory
        /// against the original default behaviour (providing configurations like
        /// <see cref="Customized_Global_LogFile_Config"/> with <see langword="not null"/>
        /// and <see cref="LogFileConfig.MinimumLogLevel"/> not <see cref="LogLevel.None"/>)
        /// may lead to an exception.
        /// </summary>
        public bool Enable_Disk_Operations = true;
        /// <summary>
        /// The custom configuration of <c>latest.log</c>.
        /// </summary>
        /// <remarks>
        /// Provide <see cref="LogLevel.None"/> to <see cref="LogFileConfig.MinimumLogLevel"/> can prevent this file from being created. <para/>
        /// Notice that if providing certain fields of this instance with null, they will not
        /// follow the default behaviour of <see cref="Log.GlobalConfig"/>, but the previous
        /// default behaviour of this pre-defined log file itself.
        /// </remarks>
        public LogFileConfig? Customized_Global_LogFile_Config = null;
        /// <summary>
        /// The custom configuration of <c>latest.debug.log</c>.
        /// </summary>
        /// <inheritdoc cref="Customized_Global_LogFile_Config"/>
        public LogFileConfig? Customized_Debug_LogFile_Config = null;

        public LoggerConfig(int max_Output_Char_Count = 16 * 1024,
                            bool use_Console_Wrapper = false,
                            bool use_Working_Directory = true,
                            LogLevel global_Minimum_LogLevel = LogLevel.Debug,
                            LogLevel console_Minimum_LogLevel = LogLevel.Information,
                            bool debug_LogWriter_AutoFlush = false,
                            bool is_PipeSeparated_Format = false,
                            bool enable_Detailed_Time = false,
                            bool enable_Disk_Operations = true,
                            LogFileConfig? customized_Global_LogFile_Config = null,
                            LogFileConfig? customized_Debug_LogFile_Config = null)
        {
            Max_Output_Char_Count = max_Output_Char_Count;
            Use_Console_Wrapper = use_Console_Wrapper;
            Use_Working_Directory = use_Working_Directory;
            if (console_Minimum_LogLevel < global_Minimum_LogLevel)
            {
                throw new ArgumentException("The LogLevel output to console cannot be smaller than the global minimum LogLevel.", nameof(console_Minimum_LogLevel));
            }
            Global_Minimum_LogLevel = global_Minimum_LogLevel;
            Console_Minimum_LogLevel = console_Minimum_LogLevel;
            Debug_LogWriter_AutoFlush = debug_LogWriter_AutoFlush;
            Is_PipeSeparated_Format = is_PipeSeparated_Format;
            Enable_Detailed_Time = enable_Detailed_Time;
            Enable_Disk_Operations = enable_Disk_Operations;
            Customized_Global_LogFile_Config = customized_Global_LogFile_Config;
            Customized_Debug_LogFile_Config = customized_Debug_LogFile_Config;
        }

        public LoggerConfig()
        {
            Max_Output_Char_Count = 16 * 1024;
            Use_Console_Wrapper = false;
            Use_Working_Directory = true;
            Global_Minimum_LogLevel = LogLevel.Debug;
            Console_Minimum_LogLevel = LogLevel.Information;
            Debug_LogWriter_AutoFlush = true;
            Is_PipeSeparated_Format = false;
            Enable_Detailed_Time = false;
            Enable_Disk_Operations = true;
        }
    }
}
