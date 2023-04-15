using YYHEggEgg.Logger;

// See https://aka.ms/new-console-template for more information

Log.Initialize(new LoggerConfig(
    max_Output_Char_Count: 500,
    use_Console_Wrapper: false,
    use_Working_Directory: true,
#if DEBUG
    is_Debug_LogLevel: true
#else
    is_Debug_LogLevel: false
#endif
    ));

// 1. Dbug test
#if DEBUG
Log.Dbug("this is run on DEBUG!");
#elif RELEASE
Log.Dbug("this should not be output in RELEASE!");
#endif

// 2. Color test
Log.Erro("<color=Blue>blue text</color>-<>>><<<color=Yellow>yelolow text</color>/<><color=FF>no color text</color>");
Console.ReadLine();