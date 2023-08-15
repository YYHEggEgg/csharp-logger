# csharp-logger
一个方便的 C# 日志记录器实现。    

你可以通过在 [nuget.org](https://www.nuget.org) 上搜索 [EggEgg.CSharp-Logger](https://www.nuget.org/packages/EggEgg.CSharp-Logger) 来下载它。

[![NuGet](https://img.shields.io/nuget/v/EggEgg.CSharp-Logger.svg)](https://www.nuget.org/packages/EggEgg.CSharp-Logger)

## 更新
### v4.0.0 - Preview 1 (v3.0.50)
这个版本几乎重新实现了整个 logger，将静态类 `Log` 的主要功能提取并封装到了 `BaseLogger`。原有的功能不受影响，并修复了一些 bug，但可能会遇到一些中断性变更。

注意，尽管 `BaseLogger` 是一套完整的日志实现，想要使用它也必须调用 `Log.Initialize`。此外，尽管可以为每个 `BaseLogger` 提供不同的初始化 `LoggerConfig`，其关键的字段必须与 `Log.Initialize` 时提供的版本统一，以保持整个程序中 Logger 的各项行为一致。  
现阶段受影响的字段有：`Use_Console_Wrapper`、`Use_Working_Directory`。

主要更改：
- 修复了由于计时算法缺陷，日志处理后台任务在处理完日志后不会等待规定时间的问题。
- 修复了在大多数情况下，清理所等待的时间（1.5s）会达到最大值，且仍有可能无法完成控制台清理的问题。

### 中断性变更
- 现在如果用户在 `Log.Initialize` 时指示使用程序路径（为 `LoggerConfig.Use_Working_Directory` 提供了 `false`），并且其无法访问，相比之前的实现，其不会发出警告程序会 fallback 到工作目录。同理，在压缩过往日志文件时如果出现了错误也不会有警告提示。
- 现在调用 `ConsoleWrapper.WriteLine` 的任何重载**都会先分析其中的颜色信息**，将结果加入内部处理队列后才返回给调用方（之前会将其加入内部队列，由单独的后台任务处理），其可能引发额外的时间消耗。开发者正在寻求其他方案以及改进分析算法的性能。
- 重要：在新版本中，不仅有 `latest.log` 与 `latest.debug.log`，而是**任何在 `logs` 文件夹下符合通配符 `latest.*.log` 的**日志文件都会在调用 `Log.Initialize` 进行过往日志处理时被重命名为最后一次进行写入的时间。  
  它们也同样受日志自动压缩的影响，但这实际上与过往的行为一致。
- 现在在程序结束时，给予 Logger 的离开时间为 1000ms，而给予控制台输出（无论是普通输出还是 `ConsoleWrapper`）的离开时间为 500ms（其一定在 Logger 清理工作完毕后才进行）。在以前的版本中，这两个数字分别是 300ms 和 1200ms。
- 前修复的问题“`ConsoleWrapper.ReadLine` 系列方法在程序结束时，会异常重复输出 `InputPrefix` 内容”有恢复的可能。

### v3.0.0
- 修复了等待队列中的未处理日志会在程序关闭时丢失的问题。
- 修复了 `debug.log` 日志在 `Debug_LogWriter_AutoFlush` 设为 `false` 时，终止程序造成未写入磁盘的日志丢失的问题。
- 正常支持了 `ConsoleWrapper` 按 Ctrl+V 粘贴文本的功能。
- 修复了使用 `ConsoleWrapper.ReadLine` 系列方法时，控制台无法滚动查看上下内容的问题。
- 修复了 `ConsoleWrapper.ReadLine` 系列方法在程序结束时，会异常重复输出 `InputPrefix` 内容的问题。
- 修复了在使用原生 `Console` 输出日志（即不使用 `ConsoleWrapper` 的默认配置）时，当用户选择控制台上的内容时，日志处理线程会被阻塞的问题。
- 修复了自动压缩日志功能忽略内容为空的日志的问题。
- 修复了自动压缩日志功能在无法压缩到现有日志压缩包而创建新 zip 文件时，忽略内容为空的日志的问题。
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