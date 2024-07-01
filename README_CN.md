中文 | [EN](https://github.com/YYHEggEgg/csharp-logger/blob/main/README.md)

# csharp-logger

一个方便的 C# 日志记录器实现，同时有可靠的控制台交互封装支持可使用。

你可以通过在 [nuget.org](https://www.nuget.org) 上搜索 [EggEgg.CSharp-Logger](https://www.nuget.org/packages/EggEgg.CSharp-Logger) 来下载它。

[![NuGet](https://img.shields.io/nuget/v/EggEgg.CSharp-Logger.svg)](https://www.nuget.org/packages/EggEgg.CSharp-Logger)

## 目录

- [更新](#更新)
  - [v5.0.0](#v500)
  - [v4.0.2](#v402)
  - [v4.0.1](#v401)
  - [v4.0.0](#v400)
- [功能](#功能)
- [最佳实践](#最佳实践)

## 更新

### v5.0.0

- 修复了在使用 `ConsoleWrapper` 时按方向上键，上一次的命令输入可能不会立刻可用的问题（即先弹出上上条输入，再按一次方向下键才能找到上条输入）。
- 修复了在使用 `ConsoleWrapper` 时，对于自动填充的特定结果第一次使用不会响应，且之后出现填充错位的问题。
- 修复了在使用 `ConsoleWrapper` 时，在程序启动后至 `ConsoleWrapper.ReadLineAsync` 或其等效方法被调用之前，后台线程保持循环而不暂停，导致 CPU 始终处于满载的问题。**此问题影响所有版本号为 4.x 的版本。**
- 修复了 `ConsoleWrapper` 在遇到成 Unicode 代理项对的两个字符（例如 emoji）时，控制台操作（如光标移动、删除字符）无法将其视为单个字符。
- 为 `ConsoleWrapper.ReadLineAsync` 添加了 `CancellationToken` 参数。
- 为了提高兼容性，现在 `ConsoleWrapper` 在初始化时不会将 `Console.TreatControlCAsInput` 设为 `true`。此时 `ConsoleWrapper.ShutDownRequest` 事件会包含有意义的 `sender` 和 `args`.
- 修复了在使用 `ConsoleWrapper` 时，如果开始读取后没有输出过日志，则可能不显示读取的 `InputPrefix` 的问题。
- 修复了在 Windows 7 下的命令提示符使用 `ConsoleWrapper` 时，输入多行数据会产生异常的问题。
- 修复了使用默认构造器初始化的 `LoggerConfig` 可能出现未预期的参数的问题。
- 现在，支持使用 `ConsoleWrapper.ChangeHistory` 更改输入控制台的历史记录，将原有的历史记录清空并替换。注意：其仍受历史记录最大字符数的限制。您可以考虑更改 `ConsoleWapper.HistoryMaximumChars`.
- 现在如果您设定的 `ConsoleWrapper.AutoCompleteHandler` 在处理 `GetSuggestions` 时抛出了异常，将会显示一条警告并输出信息到 `latest.errtrace.log`.
- 现在可以设置 `LogTrace.MaximumStoredChars` 来控制其存储字符的量，用于存储异常的内容以重用 Trace ID.
- 现在您可以通过在日志记录参数中加入 `DateTime` 来改变日志写入时所用的时间。有关详细信息，请参阅对应方法重载。
- 修复了在使用 `ConsoleWrapper` 输入时，如果有日志输出，则历史记录的滚动进度被重置的问题。

#### 中断性变更

接口 `IAutoCompleteHandler` 的定义发生了变化，以给予实现者更好的控制。详见 `IAutoCompleteHandler` 与 `SuggestionResult` 的定义。

```cs
public class SuggestionResult
{
    /// <summary>
    /// The suggestion based on the context.
    /// </summary>
    public IList<string>? Suggestions { get; set; }
    /// <summary>
    /// The start index in the provided text to fill in the suggestions (replace). Only apply to the first suggestion.
    /// </summary>
    public int StartIndex { get; set; }
    /// <summary>
    /// The end index in the provided text to fill in the suggestions (replace). If want to replace all contents after user's cursor, set -1. Only apply to the first suggestion.
    /// </summary>
    public int EndIndex { get; set; } = -1;
}

public interface IAutoCompleteHandler
{
    /// <summary>
    /// Get the suggestions based on the current input.
    /// </summary>
    /// <param name="text">The full line of input.</param>
    /// <param name="index">The position where the user's cursor on.</param>
    SuggestionResult GetSuggestions(string text, int index);
}
```

#### 已知问题

- 极少部分运行环境在 `ConsoleWrapper` 的输入区域存在中文时，`Console.GetCursorPosition` 方法或 `Console.CursorLeft` 调用阻塞（除非多次按下新的字符，例如方向右键），导致出现卡顿或无法操作。尝试解决无果。
- 如果在 `ConsoleWrapper` 的输入区域出现了成 Unicode 代理项对的两个字符（例如 emoji），其在被排列到行末或特定位置会出现显示异常。实际文本与光标操作不受影响。

### v4.0.2

- 增加了对于 .NET 8.0 的支持与对于 Native AOT 的适配。
- 现在在使用 `BaseLogger` 构造器重载 `BaseLogger(LoggerConfig, LogFileConfig)` 以及类似重载时，可以指定 `LogFileConfig.AllowFallback` 来使程序在已经创建过对应日志文件时，自动使用现有的一个。  
  `BaseLogger.LogFileExists` 可以检查使用对应 `FileIdentifier` 的日志文件是否已经存在。
- 加入了 Error Trace 特性。您可以使用 `LoggerChannel.LogErroTrace` 等扩展方法来输出异常信息。它可以将异常详细信息输出到 `latest.errtrace.log`，而只在控制台显示异常的消息（`Exception.Message`）。  
  扩展方法可用于 `LoggerChannel` 和 `BaseLogger`。方法未添加到 `Log` 中，您需要使用 `LogTrace` 来调用。`latest.errtrace.log` 只会在第一次调用相关方法时创建。以下是一个效果示例：

  ```log
  03:06:51 <Erro:ErrorTraceTest> Error trace test raised an exception. System.NotImplementedException: This is a test error in Logger-test. (Trace ID: 38c3a0bf-b67a-457a-a5b7-8672d7fbc8a5)
  ```
- 现在 `LoggerConfig` 的各项值移除了只读标签，这意味着可以不必总是使用构造器初始化了。注意，这并不意味着您可以在初始化后修改 `Logger` 的配置；在调用相关属性时将自动返回一个深拷贝。

### v4.0.1

- 修复了在 `BaseLogger` 构造器中使用重载 `BaseLogger(LoggerConfig, LogFileConfig)` 后，创建的文件无法在重载 `BaseLogger(LoggerConfig, string)` 中利用的问题。
- 修复了在 `BaseLogger` 构造器中使用重载 `BaseLogger(LoggerConfig, LogFileConfig)` 后，不进行检查而允许创建多个名称相同或仅大小写不同的文件。

这些修复对于其他相似的重载也适用。

### v4.0.0

这个版本几乎重新实现了整个 logger，将静态类 `Log` 的主要功能提取并封装到了 `BaseLogger`。原有的功能不受影响，并修复了一些 bug，但可能会遇到一些中断性变更。

注意，尽管 `BaseLogger` 是一套完整的日志实现，想要使用它也必须先调用 `Log.Initialize`。此外，尽管可以为每个 `BaseLogger` 提供不同的初始化 `LoggerConfig`，其关键的字段必须与 `Log.Initialize` 时提供的版本统一，以保持整个程序中 Logger 的各项行为一致。  
现阶段受影响的字段有：`Use_Console_Wrapper`、`Use_Working_Directory`、`Max_Output_Char_Count`、`Enable_Detailed_Time`。

主要更改：

- 修复了由于计时算法缺陷，日志处理后台任务在处理完日志后不会等待规定时间的问题。
- 修复了在大多数情况下，清理所等待的时间（1.5s）会达到最大值，且仍有可能无法完成控制台清理的问题。
- 修复了 `ConsoleWrapper` 未在进行读取时，按 Ctrl+C 无法触发 `ConsoleWrapper.ShutDownRequest` 事件，直至下一次读取才可触发的问题。
- 修复了 `ConsoleWrapper` 中特定情况下可输入非法字符的问题。
- 修复了为 `Log.PushLog` 在内的一些方法与成员提供非法 `LogLevel` 时，会正常进入处理队列并可能引起内部线程崩溃的问题。
- 使用了全新的基于 KMP 的算法来分析颜色标签信息，加快了处理速度。详见 [Benchmark 数据](https://github.com/YYHEggEgg/csharp-logger/blob/main/ColorLineUtil-report-github.md)。
- 添加了 `LogTextWriter` 实现抽象类 `TextWriter`。但注意其使用一个缓冲区来维护未换行的字符；也就是说，在没有输入换行符之前的所有内容都不会显示在 `Logger`。  
  如果一些外部代码使用 `Console` 的方法且您使用 `ConsoleWrapper`（例如 `CommandLineParser`），则必须为其提供 `LogTextWriter` 的实例。
- 为了不必在每次记录日志时都提供 `sender` 参数，添加了 `LoggerChannel`。对于实现某一段逻辑的代码，其在调用 `Log` 方法时通常会传递同一个 `sender` 参数，此时便可以以它创建 `LoggerChannel` 来减少代码量并增加可维护性。
- 在 `Log` 静态类额外创建了一些指向 `BaseLogger` 对应方法的静态方法。
- 现在可以通过设置 `LoggerConfig.Enable_Detailed_Time` 来设置是否启用时间细节。默认情况下，Logger 记录的时间仅精确到秒，且不包含日期，对应格式化字符串 `HH:mm:ss`。  
  开启时间细节后，将会展现日志提交时间直至七分之一秒的细节，与之相对应的格式化字符串为 `yyyy-MM-dd HH:mm:ss fff ffff`，两部分 `fff` 和 `ffff` 分别表示毫秒级别与万分之一毫秒（100 纳秒，0.1 微秒）级别，如 `2023-08-22 15:43:36 456 4362`. 此配置要求全局统一，对控制台与日志文件的输出内容均生效。
- 可以在创建新日志文件时，使用 `LogFileConfig.IsPipeSeparatedFormat` 指示创建的日志文件是否为竖线分隔值文件（Pipe-separated values file，PSV）。
  将日志输出为表格有助于在数据量极大时进行分析，尤其是如果程序中大量模块化代码调用 Log 方法时不会改变 sender 参数的情况下。此配置不会对输出至控制台的内容生效，且出于一些性能上的考虑，目前只接受 `|` 作为分隔符。输出格式为：

  ```log
  [time]|[level]|[sender]|[content]
  23:14:17|Warn|TestSender|Push a warning log!
  ```

- 现在可以为 `LogTraceListener` 和 `LogTextWriter` 提供 `basedlogger` 参数，来改变它们输出的目标 `BaseLogger`。默认值仍为全局静态类 `Log` 共享的 `Log.GlobalBasedLogger`.
- 借鉴了一些 [tonerdo/readline](https://github.com/tonerdo/readline) 的代码来实现新版的 `ConsoleWrapper`。但请注意，本版本的 nuget 包不包含对 nuget 包 [ReadLine](https://www.nuget.org/packages/ReadLine) 的引用，也不包含类 `ReadLine`。所有操作仍封装为 `ConsoleWrapper`。
- 大幅提升了 `ConsoleWrapper` 在文本量较大时修改文本的体验。
- 现在设置 `ConsoleWrapper.AutoCompleteHandler` 可对用户的输入进行自动补全。
- 优化了 `Log`、`BaseLogger` 以及 `LoggerChannel` 类内的方法指示。
- 现在可以通过 `ConsoleWrapper.ReadLine(false)` 来指示不将当前行的内容计入命令输入历史。这在指示用户输入只与特定上下文有关的信息（例如 `Type 'y' to confirm, 'n' to refuse` 之类的确认 prompt）时很有用。

#### 中断性变更

- `Log.CustomConfig` 属性更名为 `Log.GlobalConfig`。同时，现在在未调用 `Log.Initialize` 初始化时访问 `Log.GlobalConfig` 会引发 `InvalidOperationException`.
- 现在如果用户在 `Log.Initialize` 时指示使用程序路径（为 `LoggerConfig.Use_Working_Directory` 提供了 `false`），并且其无法访问，相比之前的实现，其不会发出程序会 fallback 到工作目录的警告。同理，在压缩过往日志文件时如果出现了错误也不会有警告提示。
- **重要**：在新版本中，Logger 使用的默认的控制台颜色变更为 `ConsoleColor.Gray`，以与大部分控制台实现保持一致。你现在也可以通过 `Log.GlobalDefaultColor` 属性更改此设置。
- **重要**：在新版本中，不仅有 `latest.log` 与 `latest.debug.log`，而是**任何在 `logs` 文件夹下符合通配符 `latest.*.log` 的**日志文件都会在调用 `Log.Initialize` 进行过往日志处理时被重命名为最后一次进行写入的时间。  
  它们也同样受日志自动压缩的影响，但这实际上与过往的行为一致。
- 现在在控制台不受支持的平台上调用 `Log.Initialize` 或 `BaseLogger.Initialize` 时，如果为 `LoggerConfig.Console_Minimum_LogLevel` 指定除 `LogLevel.None` 以外的值，将会直接抛出异常。  
  你仍可以在这些平台上使用本 Logger，只是必须禁用控制台日志。目前认为控制台仅在 Windows, macOS, Linux 平台上受支持。
- 现在在程序结束时，给予 Logger 的离开时间为 1000ms，而给予控制台输出（无论是普通输出还是 `ConsoleWrapper`）的离开时间为 500ms（其一定在 Logger 清理工作完毕后才进行）。在以前的版本中，这两个数字分别是 300ms 和 1200ms。
- 颜色判断使用了全新的算法，因此其可能与以前的实现并不“bug 兼容”。
- `ConsoleWrapper` 使用了全新的算法，开发者尽量将引起的中断性变更置于下方，但是被修复的 bug 可能不会列出。
- `ConsoleWrapper` 以前支持将多行的文本保留换行符粘贴至输入中（尽管修改文本时会出现问题）；现在并不支持这一功能，并且会将粘贴的所有换行符均替换为两个空格。  
  > 识别的换行序列列表是 CR (U+000D)、LF (U+000A)，CRLF (U+000D U+000A)、NEL (U+0085)、LS (U+2028)、FF (U+000C) 和 PS (U+2029)。此列表由 Unicode Standard 第 5.8 条建议 R4 和表 5-2 提供。

  但是，由于一些终端重载了 `Ctrl`+`V` 快捷键，如果你的程序利用“将换行符转换为空格以供一次输入”的功能，你可能需要提示用户使用 `Ctrl`+`Alt`+`V` 快捷键，以保证内置粘贴机制的触发。在剪贴板文本极大时，使用内置粘贴还可以显著提高输入框性能。  
  下表表头中的快捷键到达 `ConsoleWrapper` 时均会被识别为粘贴功能，针对多种控制台列出了支持情况。表中“支持”代表换行符会被正确转换为两个空格；“不支持”代表终端重载了该快捷键，致使剪贴板中的换行符变为回车；“阻止”表示终端既不将该快捷键重载为粘贴，也不将该按键传递给程序，即按键完全被忽视（大部分时候是因为与其他快捷键冲突）。测试环境为 Windows 11.

  | 终端类型 | `Ctrl`+`V` | `Ctrl`+`Shift`+`V` | `Ctrl`+`Alt`+`V` | `Windows`+`V`（选中剪贴板记录以粘贴） |
  | :----------------- | ------ | ------ | ------ | ------ |
  | Windows cmd.exe | 支持 | 支持 | 支持 | 支持 |
  | Windows powershell.exe | 支持 | 支持 | 支持 | 支持 |
  | Windows Terminal | 不支持 | 不支持 | 支持 | 不支持 |
  | VSCode 集成终端（powershell, windows） | 支持 | 不支持 | 阻止 | 支持 |
  | VSCode 集成终端（cmd, windows） | 不支持 | 不支持 | 阻止 | 不支持 |
  | VSCode 集成终端（Git Bash MINGW64, windows） | 不支持 | 不支持 | 阻止 | 不支持 |
  | VSCode 集成终端（Javascript 调试终端, windows） | 支持 | 不支持 | 阻止 | 支持 |
  | VSCode 集成终端（Bash, Remote） | 不支持 | 不支持 | 阻止 | 不支持 |
  | Visual Studio 开发者 Powershell 集成终端 | 支持 | 支持 | 阻止 | 支持 |
  | Visual Studio 开发者命令提示 集成终端 | 支持 | 不支持 | 阻止 | 支持 |

  总结下来，如果程序确实依赖该机制，请你提示用户在 Windows Terminal 下使用 `Ctrl`+`Alt`+`V`。其他环境下使用 `Ctrl`+`V` 即可。

- `ConsoleWrapper` 现在支持与 bash 类似的历史合并，也就是说连续执行一条命令多次并不会在历史中留下多次记录。
- `ConsoleWrapper` 现在在程序未确认输入时将不会将行内容计入历史记录，此时按上/下方向键并不能包括未确认输入的内容。例如，在**没有线程等待 `ConsoleWrapper.ReadLine` 时**依次输入 `1[回车]2[回车][上方向键][回车]`，那么程序此后第三次调用 `ConsoleWrapper.ReadLine` 取回的数据将是空字符串（在以前的版本中将会取回 `2`）。
- `ConsoleWrapper` 的新读入方式类似于 [GNU Readline](https://en.wikipedia.org/wiki/GNU_Readline)，但这导致 Ctrl+C 的过往行为与其产生了冲突，因此保留了 Ctrl+C 引发 `ConsoleWrapper.ShutDownRequest` 事件的功能。除此之外，其他所有下附 GNU readline 快捷键的功能均可以视为中断性变更。
- 为了支持中文及全角字符等宽字符的输入，同时避开各种 corner case 的处理，现在 `ConsoleWrapper` 单行内可容纳的字符量减少了 1 位，最后一位作为承载宽字符的额外空间。
- 现在当一段输入的字符量超出控制台字符位数量时，它将不会被记录在输入历史中。此默认行为可以通过操作 `ConsoleWrapper.HistoryMaximumChars` 来更改。  
  将其更改为正数可更新限制，更改为 0 可以停止（往后的）命令输入历史记录，更改为 -1 可以取消此限制。
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
  它接受部分 [GNU readline](https://en.wikipedia.org/wiki/GNU_Readline) 的快捷键读取数据，但相比其他 .NET 的 GNU readline 版本，**它支持中文的输入**。下表是支持列表：

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
  | ~~`Ctrl`+`T`~~                     | 由于引入中文、全角输入支持而放弃 ~~转置（即反转）当前光标位置的前一个字符与后一个字符，并使光标随后前进一位~~ |
  | `Backspace`                    | 删除光标前的一个字符 |
  | `Ctrl`+`D` / `Delete`          | 删除光标所在位置的一个字符 |

  此外，它不需要任何配置就可以读入大于 4096 位的数据。但请注意，当用户的输入非常大且控制台频繁输出日志时，它可能会影响性能。它默认是禁用的，您可以通过 `LoggerConfig(use_Console_Wrapper: true)`来启用它。
- 输出数量限制  
  大量信息输出到控制台会严重影响性能。您可以通过 `LoggerConfig.Max_Output_Char_Count` 设置每条日志的最大输出量，如果单条日志超出了这个值，则控制台内不会显示该条日志的内容，而是以特殊的警告取代。它的内容可以在文件中找到。  
  您也可以通过将其设置为 `-1` 来禁用此功能。
- 自动压缩日志  
  如果有至少 1 天前创建的日志，则会将它们压缩成一个zip文件，例如 `logs.[Date].zip`。
- 将日志记录为表格  
  可以在创建每个新日志文件时，使用 `LogFileConfig.IsPipeSeparatedFormat` 指示创建的日志文件是否为竖线分隔值文件（Pipe-separated values file，PSV）。  
  将日志输出为表格有助于在数据量极大时进行分析，尤其是如果程序中大量模块化代码调用 Log 方法时不会改变 sender 参数的情况下。此配置不会对输出至控制台的内容生效，且出于一些性能上的考虑，目前只接受 `|` 作为分隔符。输出格式为：

  ```log
  [time]|[level]|[sender]|[content]
  23:14:17|Warn|TestSender|Push a warning log!
  ```

## 最佳实践

1. 在程序的入口点（也可是其他地方，总之越早越好）调用 `Log.Initialize(LoggerConfig)` 来进行全局初始化，然后开始使用日志相关方法。
2. 谨慎配置 `LoggerConfig` 的各项功能。如果只想用作普通的日志记录器，以下是一个推荐配置：

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

3. 如果想要使用 `ConsoleWrapper` 的功能，需要将上示 `LoggerConfig` 中的 `use_Console_Wrapper` 设为 true，然后开始使用其功能。
4. 在创建新日志文件时，可使用 `LogFileConfig.IsPipeSeparatedFormat` 指示创建的日志文件是否为竖线分隔值文件（Pipe-separated values file，PSV）。  
   将日志输出为表格有助于在数据量极大时进行筛选与分析，尤其是如果程序中大量模块化代码调用 Log 方法时不会改变 sender 参数的情况下。可使用 `BaseLogger(LoggerConfig, LogFileConfig)` 为统计类数据专门创建一个 `BaseLogger` 与其日志文件，并令 `content` 同样使用类似 PSV 的格式，以便于数据的查询。
5. 可以通过将 `LoggerConfig` 中的 `enable_Detailed_Time` 设为 true，启用日志的时间细节。默认情况下，Logger 记录的时间仅精确到秒，且不包含日期，对应格式化字符串 `HH:mm:ss`。  
  开启时间细节后，将会展现日志提交时间直至七分之一秒的细节，与之相对应的格式化字符串为 `yyyy-MM-dd HH:mm:ss fff ffff`，两部分 `fff` 和 `ffff` 分别表示毫秒级别与万分之一毫秒（100 纳秒，0.1 微秒）级别，如 `2023-08-22 15:43:36 456 4362`. 此配置要求全局统一，对控制台与日志文件的输出内容均生效。
6. 如果程序使用 [CommandLineParser](https://www.nuget.org/packages/CommandLineParser)，请重定向它的输出 `TextWriter`。使用如下代码：

   ```cs
   public readonly static Parser DefaultCommandsParser = new Parser(config =>
   {
       // 在构建时设置自定义 ConsoleWriter
       config.HelpWriter = TextWriter.Synchronized(new LogTextWriter("CommandLineParser"));
   });
   ```

7. 如果你使用 `ConsoleWrapper`，最好订阅事件 `ConsoleWrapper.ShutDownRequested` 以在用户按下 Ctrl+C 时做出操作。
