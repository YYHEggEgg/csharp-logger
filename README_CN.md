# csharp-logger

一个方便的 C# 日志记录器实现，同时有可靠的控制台交互封装支持可使用。

你可以通过在 [nuget.org](https://www.nuget.org) 上搜索 [EggEgg.CSharp-Logger](https://www.nuget.org/packages/EggEgg.CSharp-Logger) 来下载它。

[![NuGet](https://img.shields.io/nuget/v/EggEgg.CSharp-Logger.svg)](https://www.nuget.org/packages/EggEgg.CSharp-Logger)

## 更新

### v4.0.0 - Preview 9 Patch 2 (v3.8.52-beta)

（注：这是 v4.0.0 正式版本前的最后一个次要预览版本。它的最新 Patch 将会与正式版本 v4.0.0 完全一致。）

这个版本几乎重新实现了整个 logger，将静态类 `Log` 的主要功能提取并封装到了 `BaseLogger`。原有的功能不受影响，并修复了一些 bug，但可能会遇到一些中断性变更。

注意，尽管 `BaseLogger` 是一套完整的日志实现，想要使用它也必须先调用 `Log.Initialize`。此外，尽管可以为每个 `BaseLogger` 提供不同的初始化 `LoggerConfig`，其关键的字段必须与 `Log.Initialize` 时提供的版本统一，以保持整个程序中 Logger 的各项行为一致。  
现阶段受影响的字段有：`Use_Console_Wrapper`、`Use_Working_Directory`、`Max_Output_Char_Count`、`Enable_Detailed_Time`。

主要更改：

- 修复了由于计时算法缺陷，日志处理后台任务在处理完日志后不会等待规定时间的问题。
- 修复了在大多数情况下，清理所等待的时间（1.5s）会达到最大值，且仍有可能无法完成控制台清理的问题。
- 使用了全新的基于 KMP 的算法来分析颜色标签信息，加快了处理速度。详见 [Benchmark 数据](https://github.com/YYHEggEgg/csharp-logger/blob/main/ColorLineUtil-report-github.md)。
- 添加了 `LogTextWriter`，但注意其使用一个缓冲区来维护未换行的字符；也就是说，在没有输入换行符之前的所有内容都不会显示在 `Logger`。  
  如果一些外部代码使用 `Console` 的方法且您使用 `ConsoleWrapper`（例如 `CommandLineParser`），则必须为其提供 `LogTextWriter` 的实例。
- 添加了 `LoggerChannel`。对于实现某一段逻辑的代码，其在调用 `Log` 方法时通常会传递同一个 `sender` 参数，可以创建 `LoggerChannel` 来减少代码量并增加可维护性。
- 在 `Log` 静态类额外创建了一些指向 `BaseLogger` 对应方法的静态方法。
- 现在可以通过设置 `LoggerConfig.Enable_Detailed_Time` 来设置是否启用时间细节。默认情况下，Logger 记录的时间仅精确到秒，且不包含日期，对应格式化字符串 `HH:mm:ss`。  
  开启时间细节后，将会展现日志提交时间直至七分之一秒的细节，与之相对应的格式化字符串为 `yyyy-MM-dd HH:mm:ss fff ffff`，两部分 `fff` 和 `ffff` 分别表示毫秒级别与万分之一毫秒（100 纳秒，0.1 微秒）级别，如 `2023-08-22 15:43:36 456 4362`. 此配置要求全局统一，对控制台与日志文件的输出内容均生效。
- 可以在创建新日志文件时，使用 `LogFileConfig.IsPipeSeparatedFormat` 指示创建的日志文件是否为竖线分隔值文件（Pipe-separated values file，PSV）。
  将日志输出为表格有助于在数据量极大时进行分析，尤其是如果程序中大量模块化代码调用 Log 方法时不会改变 sender 参数的情况下。此配置不会对输出至控制台的内容生效，且出于一些性能上的考虑，目前只接受 `|` 作为分隔符。输出格式为：

  ```log
  [time]|[level]|[sender]|[content]
  23:14:17|Warn|TestSender|Push a warning log!
  ```

- 现在可以为 `LogTraceListener` 和 `LogTextWriter` 提供 `basedlogger` 参数，来改变它们输出的目标 `BaseLogger`。默认值仍为全局静态类共享的 `Log.GlobalBasedLogger`.
- 借鉴了一些 [tonerdo/readline](https://github.com/tonerdo/readline) 的代码来实现新版的 `ConsoleWrapper`。但请注意，本版本的 nuget 包不包含对 nuget 包 [ReadLine](https://www.nuget.org/packages/ReadLine) 的引用，也不包含类 `ReadLine`。所有操作仍封装为 `ConsoleWrapper`。
- 大幅提升了 `ConsoleWrapper` 在文本量较大时修改文本的体验。
- 现在设置 `ConsoleWrapper.AutoCompleteHandler` 可对用户的输入进行自动补全。
- 优化了 `Log`、`BaseLogger` 以及 `LoggerChannel` 类内的方法指示。

### 中断性变更

- 现在如果用户在 `Log.Initialize` 时指示使用程序路径（为 `LoggerConfig.Use_Working_Directory` 提供了 `false`），并且其无法访问，相比之前的实现，其不会发出程序会 fallback 到工作目录的警告。同理，在压缩过往日志文件时如果出现了错误也不会有警告提示。
- **重要**：在新版本中，不仅有 `latest.log` 与 `latest.debug.log`，而是**任何在 `logs` 文件夹下符合通配符 `latest.*.log` 的**日志文件都会在调用 `Log.Initialize` 进行过往日志处理时被重命名为最后一次进行写入的时间。  
  它们也同样受日志自动压缩的影响，但这实际上与过往的行为一致。
- 现在在程序结束时，给予 Logger 的离开时间为 1000ms，而给予控制台输出（无论是普通输出还是 `ConsoleWrapper`）的离开时间为 500ms（其一定在 Logger 清理工作完毕后才进行）。在以前的版本中，这两个数字分别是 300ms 和 1200ms。
- 颜色判断使用了全新的算法，因此其可能与以前的实现并不“bug 兼容”。
- `ConsoleWrapper` 使用了全新的算法，开发者尽量将引起的中断性变更置于下方，但是被修复的 bug 可能不会列出。
- `ConsoleWrapper` 以前支持将多行的文本粘贴至输入中（尽管修改文本时会出现问题）；现在并不支持这一功能，并且会将输入的所有换行符均替换为两个空格。  
  > 识别的换行序列列表是 CR (U+000D)、LF (U+000A)，CRLF (U+000D U+000A)、NEL (U+0085)、LS (U+2028)、FF (U+000C) 和 PS (U+2029)。此列表由 Unicode Standard 第 5.8 条建议 R4 和表 5-2 提供。
- `ConsoleWrapper` 现在支持与 bash 类似的历史合并，也就是说连续执行一条命令多次并不会在历史中留下多次记录。
- `ConsoleWrapper` 的新读入方式类似于 [GNU Readline](https://en.wikipedia.org/wiki/GNU_Readline)，但这导致 Ctrl+C 的过往行为与其产生了冲突，因此保留了 Ctrl+C 引发 `ConsoleWrapper.ShutDownRequest` 事件的功能。除此之外，其他所有下附 GNU readline 快捷键的功能均可以视为中断性变更。
- 该项目未来不会向下兼容到 `.NET 6.0` 以下的任何版本，包括 `.NET Standard 2.1`。

## 功能

- 基础日志实现  
  使用方法：首先 `Log.Initialize(LoggerConfig)`，然后 `Log.Info(content, sender)`、`Log.Erro(...)`、`Log.Warn(...)`、`Log.Dbug(...)`。也可以先调用 `Log.GetChannel(sender)` 确定一个 `sender` 参数，然后再调用 `logchan.LogInfo(content)`。
- **支持颜色输出**  
  只需在文本中添加xml标记，例如：`<color=Red>Output as red color</color>`。  
  颜色值应该是 [ConsoleColor](https://learn.microsoft.com/zh-cn/dotnet/api/system.consolecolor) 中的有效值，例如"Red"、"Green"。识别到的颜色标记将在日志文件中被删除。
- **并行用户输入/日志输出支持**  
  如果您想在输出日志的同时读取用户的输入（例如制作支持 CLI 交互的程序），则提供了 `ConsoleWrapper`。  
  您可以将 `ConsoleWrapper.InputPrefix` 的值设置为等待输入的前缀，就像 `mysql>` 或 `ubuntu ~$`，并使用 `ConsoleWrapper.ReadLineAsync` 读取输入。**通过设置 `ConsoleWrapper.AutoCompleteHandler` 还可对用户的输入进行自动补全**。  
  它接受部分 [GNU readline](https://en.wikipedia.org/wiki/GNU_Readline) 的快捷键读取数据。下表是支持列表：

  | 快捷键 | 功能 |
  | ------------------------------ | --------------------------------- |
  | `Ctrl`+`A` / `HOME`            | 回到输入起点 |
  | `Ctrl`+`B` / `←`               | 将光标后退一个字符（一般是左移） |
  | `Ctrl`+`C`                     | **与 GNU readline 不同，表示终止程序** |
  | `Ctrl`+`E` / `END`             | 回到输入末尾 |
  | `Ctrl`+`F` / `→`               | 将光标前进一个字符（一般是右移） |
  | `Ctrl`+`H` / `Backspace`       | 删除光标前的一个字符 |
  | `Tab`                          | 自动补全 |
  | `Shift`+`Tab`                  | 上一个自动补全 |
  | `Ctrl`+`J` / `Enter`           | 确认输入 |
  | `Ctrl`+`K`                     | 切除自光标所在位置开始（包括）至输入末尾的字符 |
  | `Ctrl`+`L`                     | 清除输入区域 |
  | `Ctrl`+`M`                     | 作用等价于回车 |
  | `Ctrl`+`N` / `↓`               | 下一条命令历史 |
  | `Ctrl`+`P` / `↑`               | 上一条命令历史 |
  | `Ctrl`+`U`                     | 切除自光标所在位置开始（不包括）至输入起始点的字符 |
  | `Ctrl`+`W`                     | 切除光标前的一个单词，即切除自光标所在位置开始（不包括）至后方（一般为左方）第一个空格（不包括）的字符 |
  | `Ctrl`+`T`                     | 转置（即反转）当前光标位置的前一个字符与后一个字符，并使光标随后前进一位（这里的前进应指向右） |
  | `Backspace`                    | 删除光标前的一个字符 |
  | `Ctrl`+`D` / `Delete`          | 删除光标所在位置的一个字符 |

  此外，它不需要任何配置就可以读入大于 4096 位的数据。但请注意，当用户的输入非常大且控制台频繁输出日志时，它可能会影响性能。它默认是禁用的，您可以通过 `LoggerConfig(use_Console_Wrapper: true)`来启用它。
- 输出数量限制  
  大量信息输出到控制台会严重影响性能。您可以通过 `LoggerConfig.Max_Output_Char_Count` 设置每条日志的最大输出量，如果单条日志超出了这个值，则控制台内不会显示该条日志的内容，而是以特殊的警告取代。它的内容可以在文件中找到。  
  您也可以通过将其设置为 `-1` 来禁用此功能。
- 自动压缩日志  
  如果有至少 1 天前创建的日志，则会将它们压缩成一个zip文件，例如 `logs.[Date].zip`。
- 分隔符表格记录  
  可以在创建每个新日志文件时，使用 `LogFileConfig.IsPipeSeparatedFormat` 指示创建的日志文件是否为竖线分隔值文件（Pipe-separated values file，PSV）。  
  将日志输出为表格有助于在数据量极大时进行分析，尤其是如果程序中大量模块化代码调用 Log 方法时不会改变 sender 参数的情况下。此配置不会对输出至控制台的内容生效，且出于一些性能上的考虑，目前只接受 `|` 作为分隔符。输出格式为：

  ```log
  [time]|[level]|[sender]|[content]
  23:14:17|Warn|TestSender|Push a warning log!
  ```

## 最佳实践

1. 在程序的入口点（也可是其他地方，总之越早越好）调用 `Log.Initialize(LoggerConfig)` 来进行全局初始化，然后开始使用日志相关方法。
2. 如果程序使用 [CommandLineParser](https://www.nuget.org/packages/CommandLineParser)，请重定向它的输出 `TextWriter`。代码如下：

   ```cs
   public readonly static Parser DefaultCommandsParser = new Parser(config =>
   {
       // 在构建时设置自定义 ConsoleWriter
       config.HelpWriter = TextWriter.Synchronized(new LogTextWriter("CommandLineParser"));
   });
   ```

3. 谨慎配置 `LoggerConfig` 的各项功能。如果只想用作普通的日志记录器，以下是一个推荐配置：

   ```cs
   Log.Initialize(new LoggerConfig(
       max_Output_Char_Count: -1,
       use_Console_Wrapper: false,
       use_Working_Directory: true,
   #if DEBUG
       global_Minimum_LogLevel: LogLevel.Verbose,
       console_Minimum_LogLevel: LogLevel.Information,
   #else
       global_Minimum_LogLevel: LogLevel.Information,
       console_Minimum_LogLevel: LogLevel.Information,
   #endif
       debug_LogWriter_AutoFlush: true,
       is_PipeSeparated_Format: false,
       enable_Detailed_Time: false
       ));
   ```

4. 如果想要使用 `ConsoleWrapper` 的功能，需要将上示 `LoggerConfig` 中的 `use_Console_Wrapper` 设为 true，然后开始使用其功能。
5. 在创建新日志文件时，可使用 `LogFileConfig.IsPipeSeparatedFormat` 指示创建的日志文件是否为竖线分隔值文件（Pipe-separated values file，PSV）。  
   将日志输出为表格有助于在数据量极大时进行筛选与分析，尤其是如果程序中大量模块化代码调用 Log 方法时不会改变 sender 参数的情况下。可使用 `BaseLogger(LoggerConfig, LogFileConfig)` 为统计类数据专门创建一个 `BaseLogger` 与其日志文件，并令 `content` 同样使用类似 PSV 的格式，以便于数据的查询。
6. 可以通过将 `LoggerConfig` 中的 `enable_Detailed_Time` 设为 true，启用日志的时间细节。默认情况下，Logger 记录的时间仅精确到秒，且不包含日期，对应格式化字符串 `HH:mm:ss`。  
  开启时间细节后，将会展现日志提交时间直至七分之一秒的细节，与之相对应的格式化字符串为 `yyyy-MM-dd HH:mm:ss fff ffff`，两部分 `fff` 和 `ffff` 分别表示毫秒级别与万分之一毫秒（100 纳秒，0.1 微秒）级别，如 `2023-08-22 15:43:36 456 4362`. 此配置要求全局统一，对控制台与日志文件的输出内容均生效。
7. 如果你使用 `ConsoleWrapper`，最好订阅事件 `ConsoleWrapper.ShutDownRequested` 以在用户按下 Ctrl+C 时做出操作。注意引发该事件的 `EventHandler` 提供的 `sender` 与 `args` 均无实际意义。
