# csharp-logger
A bussy but convenient C# Logger implement.    
You can download it on [nuget.org](https://www.nuget.org) by searching [EggEgg.CSharp-Logger](https://www.nuget.org/packages/EggEgg.CSharp-Logger).

[![NuGet](https://img.shields.io/nuget/v/EggEgg.CSharp-Logger.svg)](https://www.nuget.org/packages/EggEgg.CSharp-Logger)

## Update

### v3.0.0
- Fixed the unflushed logs lost when the program is exiting.
- Fixed the debug.log file unflushed bug when the program is exiting.
- Added ConsoleWrapper paste text feature.
- Fixed the issue that the console can't be scrolled when ConsoleWrapper is waiting for input.
- Fixed the bug that the console wrapper will produce `InputPrefix` repeatedly when exiting.
- Fixed the issue that the log writing thread blocks when user selecting chars in the console. 
- Other logger and ConsoleWrapper improvements.

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
