# csharp-logger
一个方便的 C# 日志记录器实现。    

你可以通过在 [nuget.org](https://www.nuget.org) 上搜索 [EggEgg.CSharp-Logger](https://www.nuget.org/packages/EggEgg.CSharp-Logger) 来下载它。

[![NuGet](https://img.shields.io/nuget/v/EggEgg.CSharp-Logger.svg)](https://www.nuget.org/packages/EggEgg.CSharp-Logger)

## 更新
### v2.2.2
- 修复了在特定情况下，自动压缩日志会出现错误压缩文件的问题。
- 加入了 Config 初始化参数 `debug_LogWriter_AutoFlush`。  
  尽管它最好被设置为 `true`，为了与之前的版本保持一致，其在构造函数中的默认值为 `false`。
  For now, the unflushed parts of the log will be lost. More optimization will come in further versions. 
  目前在版本中，`StreamWriter` 缓冲区中未写入磁盘的部分在关闭程序后会丢失。未来的版本中会针对其进行优化。

### v2.2.0
- 弃用了 Config 初始化参数 `is_Debug_LogLevel`。  
  使用 `Global_Minimum_LogLevel` 与 `Console_Minimum_LogLevel` 来达到同样的效果。它们可以对 Logger 的输出进行更精细的控制。
- 加入了日志级别 `Verbose` 与 `None`。现在已经支持 `Log.Verb`。
- 修复了一些问题并进行了一些优化。

## 功能
- 基础日志实现        
  使用方法：首先 `Log.Initialize(LoggerConfig)`，然后 `Log.Info(content, sender)`、`Log.Erro(...)`、`Log.Warn(...)`、`Log.Dbug(...)`。   
- **支持颜色输出**   
  只需在文本中添加xml标记，例如：`<color=Red>Output as red color</color>`。    
  颜色值应该是 [ConsoleColor](https://learn.microsoft.com/en-us/dotnet/api/system.consolecolor) 中的有效值，例如"Red"、"Green"。   
  识别到的颜色标记将在日志文件中被删除。  
- **并行输入支持**         
  如果您想在输出日志的同时读取用户的输入（例如制作支持命令交互的程序），则提供了 `ConsoleWrapper`。    
  您可以将 `ConsoleWrapper.InputPrefix` 的值设置为等待输入的前缀，就像 `mysql>` 或 `ubuntu ~$`，并使用 `ConsoleWrapper.ReadLineAsync` 读取输入。    
  _请注意，当用户的输入非常大时，它会影响性能。它默认是禁用的，您可以通过 `LoggerConfig(use_Console_Wrapper: true)`来启用它。
- 输出数量限制       
  大量信息输出会严重影响性能。您可以通过 `LoggerConfig.Max_Output_Char_Count` 设置每行的最大输出量。      
  您也可以通过将其设置为 `-1` 来禁用此功能。
- 自动压缩日志         
  如果有1天前创建的日志，则会将它们压缩成一个zip文件，例如 `logs.[Date].zip`。