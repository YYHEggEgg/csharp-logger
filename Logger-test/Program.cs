using System.Text;
using YYHEggEgg.Logger;

// See https://aka.ms/new-console-template for more information

Log.Initialize(new LoggerConfig(
    max_Output_Char_Count: 1024,
    use_Console_Wrapper: true,
    use_Working_Directory: true,
#if DEBUG
    global_Minimum_LogLevel: LogLevel.Verbose,
    console_Minimum_LogLevel: LogLevel.Debug,
#else
    global_Minimum_LogLevel: LogLevel.Information,
    console_Minimum_LogLevel: LogLevel.Warning,
#endif
    debug_LogWriter_AutoFlush: true,
    enable_Detailed_Time: true
    ));
BaseLogger separate_logger = new(new LoggerConfig(
    max_Output_Char_Count: 1024,
    use_Console_Wrapper: true,
    use_Working_Directory: true,
    global_Minimum_LogLevel: LogLevel.Verbose,
    console_Minimum_LogLevel: LogLevel.Information,
    debug_LogWriter_AutoFlush: true,
    enable_Detailed_Time: true
    ), new LogFileConfig
    {
        AutoFlushWriter = true,
        FileIdentifier = "warning",
        MinimumLogLevel = LogLevel.Warning,
        MaximumLogLevel = LogLevel.Error,
        IsPipeSeparatedFile = true,
    }
);

// 1. Dbug test
#if DEBUG
Log.Dbug("this is run on DEBUG!");
Log.Verb("Verbose is output to the log file, not console!");
#elif RELEASE
Log.Dbug("this should not be output in RELEASE!");
Log.Verb("Verbose should not appear at all!", "TESTSender");
#endif

Log.PushLog("Push a warning log!", LogLevel.Warning, "TSETSender");
Log.PushLog("Push a verbose log!", LogLevel.Verbose, "TSESTender");

// 2. Color test
ConsoleWrapper.InputPrefix = "WrapperCLI> ";
Log.Erro("<color=</color>" +
    "<color=Yellow>yelolow text</color>" +
    "<color=Yellow></color><-nothing text|" +
    "<color=Yellow><color=White><color=Blue><>></color></color></color>");
Log.Info("<color=Blue>blue text</color>-<>>><<<color=Yellow>yelolow text</color>/<><color=FF>no color text</color>", "Should not output if Release");
string res = ConsoleWrapper.ReadLine();
Log.Warn(res, "ReadLine");
//ConsoleWrapper.ReadLine();

// 3. High output amout test
StringBuilder sb = new();
for (int i = 0; i < 1000; i++) sb.AppendLine($"Batching message part {i}");
string BatchingMessage = sb.ToString();

// ConsoleWrapper.InputPrefix = "Present for test prefix > ";
// _ = ConsoleWrapper.ReadLineAsync();

separate_logger.Warn(BatchingMessage);
// Parallel.For(0, 1000, i =>
// {
//     Log.Warn(BatchingMessage);
// });

// ConsoleWrapper.ReadLine();

// 4. Sudden terminate test
Log.Warn($"The clearup succeed!");
Environment.Exit(0);