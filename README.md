# csharp-logger
A bussy but convenient C# Logger implement.    
You can download it on nuget.org by searching [EggEgg.CSharp-Logger](https://www.nuget.org/packages/EggEgg.CSharp-Logger).

[![NuGet](https://img.shields.io/nuget/v/EggEgg.CSharp-Logger.svg)](https://www.nuget.org/packages/EggEgg.CSharp-Logger)

## Update
### v2.1.2
Bugfix about `ConsoleWrapper` initialize.

Also, changed `LoggerConfig`:
- Changed it to struct, for the config should be immutable since Logger is initialized.
- Changed the default value of `use_Working_Directory` to `true` for compatiable reasons.

### v2.1.1
You can now set `LoggerConfig(use_Working_Directory)` to specify whether the log files will be storaged at the working directory or the program path. For more information, see the description of this variable.

Notice that the previous versions use the working directory.

## Features
- Common logger implements        
  Usage: Firstly `Log.Initialize(LoggerConfig)`, then `Log.Info(content, sender)`, `Log.Erro(...)`, `Log.Warn(...)`, `Log.Dbug(...)`.   
- **Color output Support**   
  Just add xml tags in text, like:`<color=Red>Output as red color</color>`.    
  The Color value should be a valid value in [ConsoleColor](https://learn.microsoft.com/en-us/dotnet/api/system.consolecolor), e.g. "Red", "Green".   
  Recognized color tags will be removed in the log file.  
- **Command Line Support**         
  if you wants to read the user's input while outputing logs parallel (e.g. making a command line program), `ConsoleWrapper` is provided.    
  You can set the value of `ConsoleWrapper.InputPrefix` as a waiting-input prefix, just like `mysql> ` or `ubuntu ~$ `, and use `ConsoleWrapper.ReadLineAsync` to read inputs from the user.    
  _Notice that it will impact the performance when the user's input is very large. It's disabled as default, and you can enable it by `LoggerConfig(use_Console_Wrapper: true)`.
- Output amount limit       
  Large infomation outputing can severely impact the performance. You can set the maximum output amount per line by `LoggerConfig.Max_Output_Char_Count`.      
  You can also disable this by setting it to `-1`.
- Auto compress logs         
  If there're logs created 1 day ago, they will be compressed into a zip file like `logs.[Date].zip`.
