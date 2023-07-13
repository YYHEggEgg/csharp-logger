# csharp-logger
一个方便的 C# 日志记录器实现。    

你可以通过在 [nuget.org](https://www.nuget.org) 上搜索 [EggEgg.CSharp-Logger](https://www.nuget.org/packages/EggEgg.CSharp-Logger) 来下载它。

[![NuGet](https://img.shields.io/nuget/v/EggEgg.CSharp-Logger.svg)](https://www.nuget.org/packages/EggEgg.CSharp-Logger)

## 更新
### v3.0.0
- 修复了等待队列中的未处理日志会在程序关闭时丢失的问题。
- 修复了 `debug.log` 日志在 `Debug_LogWriter_AutoFlush` 设为 `false` 时，终止程序造成未写入磁盘的日志丢失的问题。
- 正常支持了 `ConsoleWrapper` 按 Ctrl+V 粘贴文本的功能。
- 修复了使用 `ConsoleWrapper.ReadLine` 系列方法时，控制台无法滚动查看上下内容的问题。
- 修复了 `ConsoleWrapper.ReadLine` 系列方法在程序结束时，会异常重复输出 `InputPrefix` 内容的问题。
- 修复了在使用原生 `Console` 输出日志（即不使用 `ConsoleWrapper` 的默认配置）时，当用户选择控制台上的内容时，日志处理线程会被阻塞的问题。
- 修复了自动压缩日志功能忽略内容为空的日志的问题。
- 其他有关 logger 与 `ConsoleWrapper` 的功能修复与改进。
- 已知 `ConsoleWrapper` 使用 Home/End 键的功能在输入有多行的情况下存在显著问题。由于涉及底层代码，将会在充分测试后在下一个主要版本修复。

## 功能
- 基础日志实现        
  使用方法：首先 `Log.Initialize(LoggerConfig)`，然后 `Log.Info(content, sender)`、`Log.Erro(...)`、`Log.Warn(...)`、`Log.Dbug(...)`。   
- **支持颜色输出**   
  只需在文本中添加xml标记，例如：`<color=Red>Output as red color</color>`。    
  颜色值应该是 [ConsoleColor](https://learn.microsoft.com/en-us/dotnet/api/system.consolecolor) 中的有效值，例如"Red"、"Green"。   
  识别到的颜色标记将在日志文件中被删除。  
- **并行输入支持**         
  如果您想在输出日志的同时读取用户的输入（例如制作支持 CLI 交互的程序），则提供了 `ConsoleWrapper`。    
  您可以将 `ConsoleWrapper.InputPrefix` 的值设置为等待输入的前缀，就像 `mysql>` 或 `ubuntu ~$`，并使用 `ConsoleWrapper.ReadLineAsync` 读取输入。    
  _请注意，当用户的输入非常大时，它会影响性能。它默认是禁用的，您可以通过 `LoggerConfig(use_Console_Wrapper: true)`来启用它。
- 输出数量限制       
  大量信息输出会严重影响性能。您可以通过 `LoggerConfig.Max_Output_Char_Count` 设置每行的最大输出量。      
  您也可以通过将其设置为 `-1` 来禁用此功能。
- 自动压缩日志         
  如果有1天前创建的日志，则会将它们压缩成一个zip文件，例如 `logs.[Date].zip`。