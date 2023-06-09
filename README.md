# csharp-logger
A bussy but convenient C# Logger implement.    
You can download it on [nuget.org](https://www.nuget.org) by searching [EggEgg.CSharp-Logger](https://www.nuget.org/packages/EggEgg.CSharp-Logger).

[![NuGet](https://img.shields.io/nuget/v/EggEgg.CSharp-Logger.svg)](https://www.nuget.org/packages/EggEgg.CSharp-Logger)

## Update

### v2.2.3
- Added `logLevelWrite` and `logLevelFail` paramter for the `LogTraceListener`.  
  They will be used for invoking `TraceListener.Write` and `TraceListener.Fail` as the logLevel param.
- Also, added a method `Log.PushLog(string, LogLevel, string?)` to log with any LogLevel at runtime.

### v2.2.2
- Have some bugfix about auto compress logs. 
- Added `debug_LogWriter_AutoFlush` config paramter.
  Though it's recommended to set it to true, the default value is false because of compatiable reasons.
  For now, the unflushed parts of the log will be lost. More optimization will come in further versions. 

### v2.2.0
- Obsoleted the `is_Debug_LogLevel` config paramter.  
  Use `Global_Minimum_LogLevel` and `Console_Minimum_LogLevel` instead. You can now control logging output better by them.  
- Added `Verbose` and `None` LogLevel. `Log.Verb` is avaliable now.
- Other bugfix and improvements.

## Features
- Common logger implements        
  Usage: Firstly `Log.Initialize(LoggerConfig)`, then `Log.Info(content, sender)`, `Log.Erro(...)`, `Log.Warn(...)`, `Log.Dbug(...)`.   
- **Color output Support**   
  Just add xml tags in text, like:`<color=Red>Output as red color</color>`.    
  The Color value should be a valid value in [ConsoleColor](https://learn.microsoft.com/en-us/dotnet/api/system.consolecolor), e.g. "Red", "Green".   
  Recognized color tags will be removed in the log file.  
- **Parallel input Support**         
  if you wants to read the user's input while outputing logs parallel (e.g. making a command interacting program), `ConsoleWrapper` is provided.    
  You can set the value of `ConsoleWrapper.InputPrefix` as a waiting-input prefix, just like `mysql> ` or `ubuntu ~$ `, and use `ConsoleWrapper.ReadLineAsync` to read inputs from the user.    
  _Notice that it will impact the performance when the user's input is very large. It's disabled as default, and you can enable it by `LoggerConfig(use_Console_Wrapper: true)`.
- Output amount limit       
  Large infomation outputing can severely impact the performance. You can set the maximum output amount per line by `LoggerConfig.Max_Output_Char_Count`.      
  You can also disable this by setting it to `-1`.
- Auto compress logs         
  If there're logs created 1 day ago, they will be compressed into a zip file like `logs.[Date].zip`.
