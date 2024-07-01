using Microsoft.Extensions.Hosting;
using YYHEggEgg.Logger;

Log.Initialize(new()
{
    Use_Console_Wrapper = true,
    Console_Minimum_LogLevel = YYHEggEgg.Logger.LogLevel.Debug,
    Global_Minimum_LogLevel = YYHEggEgg.Logger.LogLevel.Verbose,
    Debug_LogWriter_AutoFlush = true,
    Max_Output_Char_Count = -1,
    Enable_Detailed_Time = true,
});

var builder = Host.CreateApplicationBuilder(args);

var app = builder.Build();

await app.RunAsync();
