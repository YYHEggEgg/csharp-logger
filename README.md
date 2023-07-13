# csharp-logger
A bussy but convenient C# Logger implement.    
You can download it on [nuget.org](https://www.nuget.org) by searching [EggEgg.CSharp-Logger](https://www.nuget.org/packages/EggEgg.CSharp-Logger).

[![NuGet](https://img.shields.io/nuget/v/EggEgg.CSharp-Logger.svg)](https://www.nuget.org/packages/EggEgg.CSharp-Logger)

## Update

### v3.0.0
- Fixed the issue where unprocessed logs in the waiting queue would be lost when the program is closed.
- Fixed the issue where `debug.log` were not written to disk when `Debug_LogWriter_AutoFlush` is set to `false` and the program is terminated.
- Properly supported the functionality of pasting text using Ctrl+V in `ConsoleWrapper`.
- Fixed the issue where the console could not scroll to view previous input/output when using the `ConsoleWrapper.ReadLine` series of methods.
- Fixed the issue where the `ConsoleWrapper.ReadLine` series of methods would repeatedly output the `InputPrefix` content when the program ends.
- Fixed the issue where the log processing thread would be blocked when the user selects content on the console while using the default configuration of logging with the native `Console`.
- Fixed the issue where the automatic log compression feature ignored logs with empty content.
- Fixed the issue where logs with empty content were ignored when the automatic log compression feature was unable to compress into an existing log compression package and created a new zip file.
- Other fixes and improvements related to the logger and `ConsoleWrapper`.
- Known issue: Significant problems exist with the functionality of using the Home/End keys in `ConsoleWrapper` when there are multiple lines of input.    
  This will be fixed in the next major version after thorough testing.

## Features
- Common logger implements        
  Usage: Firstly `Log.Initialize(LoggerConfig)`, then `Log.Info(content, sender)`, `Log.Erro(...)`, `Log.Warn(...)`, `Log.Dbug(...)`.   
- **Color output Support**   
  Just add xml tags in text, like:`<color=Red>Output as red color</color>`.    
  The Color value should be a valid value in [ConsoleColor](https://learn.microsoft.com/en-us/dotnet/api/system.consolecolor), e.g. "Red", "Green".   
  Recognized color tags will be removed in the log file.  
- **Parallel input Support**         
  if you wants to read the user's input while outputing logs parallel (e.g. making a CLI program), `ConsoleWrapper` is provided.    
  You can set the value of `ConsoleWrapper.InputPrefix` as a waiting-input prefix, just like `mysql> ` or `ubuntu ~$ `, and use `ConsoleWrapper.ReadLineAsync` to read inputs from the user.    
  _Notice that it will impact the performance when the user's input is very large. It's disabled as default, and you can enable it by `LoggerConfig(use_Console_Wrapper: true)`.
- Output amount limit       
  Large infomation outputing can severely impact the performance. You can set the maximum output amount per line by `LoggerConfig.Max_Output_Char_Count`.      
  You can also disable this by setting it to `-1`.
- Auto compress logs         
  If there're logs created 1 day ago, they will be compressed into a zip file like `logs.[Date].zip`.
