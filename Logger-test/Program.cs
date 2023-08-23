using System.Text;
using YYHEggEgg.Logger;

internal class Program
{
    private static async Task Main(string[] args)
    {
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
            enable_Detailed_Time: false
            ));

        BaseLogger separate_logger = new BaseLogger(new LoggerConfig(
            max_Output_Char_Count: 1024,
            use_Console_Wrapper: true,
            use_Working_Directory: true,
            global_Minimum_LogLevel: LogLevel.Verbose,
            console_Minimum_LogLevel: LogLevel.Information,
            debug_LogWriter_AutoFlush: true,
            enable_Detailed_Time: false
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
        Log.Erro("<color=</color>" +
            "<color=Yellow>yelolow text</color>" +
            "<color=Yellow></color><-nothing text|" +
            "<color=Yellow><color=White><color=Blue><>></color></color></color>");
        Log.Info("<color=Blue>blue text</color>-<>>><<<color=Yellow>yelolow text</color>/<><color=FF>no color text</color>", "Should not output if Release");
        Log.Info("start 1st reading attempt in 1s...");
        await Task.Delay(1000);
        ConsoleWrapper.InputPrefix = "WrapperCLI> ";
        string res = ConsoleWrapper.ReadLine();
        Log.Warn(res, "ReadLine");
        Log.Info("Now auto complete enabled... (Press Tab/Shift+Tab)");
        bool waiting = true;
        _ = Task.Run(async () =>
        {
            while (waiting)
            {
                Log.Info("A message is passing by once per sec.", "Background_TestAutoComplete");
                await Task.Delay(1000);
            }
        });
        ConsoleWrapper.AutoCompleteHandler = new SimpleAutoCmpl();
        res = ConsoleWrapper.ReadLine();
        waiting = false;
        Log.Warn(res, "ReadLine_WithAutoCOmplete");
        //ConsoleWrapper.ReadLine();

        // 3. High output amout test
        separate_logger.Warn(() =>
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 1000; i++) sb.AppendLine($"Batching message part {i}");
            return sb.ToString();
        });

        // ConsoleWrapper.InputPrefix = "Present for test prefix > ";
        // _ = ConsoleWrapper.ReadLineAsync();

        // Parallel.For(0, 1000, i =>
        // {
        //     Log.Warn(BatchingMessage);
        // });

        // ConsoleWrapper.ReadLine();

        // 4. Sudden terminate test
        Log.Warn($"The clearup succeed!");
        Environment.Exit(0);
    }

    class SimpleAutoCmpl : IAutoCompleteHandler
    {
        public char[] Separators { get; set; } = new char[] { ' ' };

        public string[] GetSuggestions(string text, int index)
        {
            return new string[]
                { "autocmp1_01", "autocmp1_02", "autocmpl_03", "autocmpl_final" };
        }
    }
}