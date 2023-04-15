# csharp-logger
一个方便的C#日志记录器实现。    

你可以通过在nuget.org上搜索 [EggEgg.CSharp-Logger](https://www.nuget.org/packages/EggEgg.CSharp-Logger) 来下载它。

[![NuGet](https://img.shields.io/nuget/v/EggEgg.CSharp-Logger.svg)](https://www.nuget.org/packages/EggEgg.CSharp-Logger)

## 更新
### v2.1.3
修复了 Debug 日志记录的问题。

在之前的版本中 `Log.Dbug` 实际上不可用。

### v2.1.2
修复了 `ConsoleWrapper` 初始化的问题。

同时，更改了 `LoggerConfig`：
- 将其更改为结构体，因为配置应该在 Logger 初始化后是不可变的。
- 将 `use_Working_Directory` 的默认值更改为 `true`，以保持兼容性。

### v2.1.1
现在，您可以设置 `LoggerConfig(use_Working_Directory)` 来指定日志文件是存储在工作目录还是程序路径中。有关此变量的说明，请参见其描述。

请注意，之前的版本使用的是工作目录。

## 功能
- 基础日志实现        
  使用方法：首先 `Log.Initialize(LoggerConfig)`，然后 `Log.Info(content, sender)`、`Log.Erro(...)`、`Log.Warn(...)`、`Log.Dbug(...)`。   
- **支持颜色输出**   
  只需在文本中添加xml标记，例如：`<color=Red>Output as red color</color>`。    
  颜色值应该是 [ConsoleColor](https://learn.microsoft.com/en-us/dotnet/api/system.consolecolor) 中的有效值，例如"Red"、"Green"。   
  识别到的颜色标记将在日志文件中被删除。  
- **命令输入支持**         
  如果您想在输出日志的同时读取用户的输入（例如制作命令行程序），则提供了 `ConsoleWrapper`。    
  您可以将 `ConsoleWrapper.InputPrefix` 的值设置为等待输入的前缀，就像 `mysql>` 或 `ubuntu ~$`，并使用 `ConsoleWrapper.ReadLineAsync` 读取输入。    
  _请注意，当用户的输入非常大时，它会影响性能。它默认是禁用的，您可以通过`LoggerConfig(use_Console_Wrapper: true)`来启用它。
- 输出数量限制       
  大量信息输出会严重影响性能。您可以通过 `LoggerConfig.Max_Output_Char_Count` 设置每行的最大输出量。      
  您也可以通过将其设置为 `-1` 来禁用此功能。
- 自动压缩日志         
  如果有1天前创建的日志，则会将它们压缩成一个zip文件，例如 `logs.[Date].zip`。