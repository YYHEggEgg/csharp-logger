[中文](https://github.com/YYHEggEgg/csharp-logger/blob/main/README_CN.md) | EN

# csharp-logger

A convenient C# Logger implementation, with reliable console interaction encapsulation support available.

You can download it on [nuget.org](https://www.nuget.org) by searching [EggEgg.CSharp-Logger](https://www.nuget.org/packages/EggEgg.CSharp-Logger).

[![NuGet](https://img.shields.io/nuget/v/EggEgg.CSharp-Logger.svg)](https://www.nuget.org/packages/EggEgg.CSharp-Logger)

## Contents

- [Update](#update)
  - [v5.0.1](#v501)
  - [v5.0.0](#v500)
  - [v4.0.2](#v402)
  - [v4.0.1](#v401)
  - [v4.0.0](#v400)
- [Features](#features)
- [Best Practices](#best-practices)

## Update

### v5.0.1

- Fixed an issue on Linux where, when `ConsoleWrapper.ReadLine(Async)` is awaiting input, log messages would only appear in the console after pressing any key.
- Fixed a bug on Linux where, entering Chinese characters while using `ConsoleWrapper` caused the console to freeze abnormally, requiring multiple key presses to slowly resume execution.
- Fixed a problem whereby if the input area of `ConsoleWrapper` contained emojis, using combinations of Home, End, or Left/Right arrow keys could frequently trigger a Debug.Assert failure and terminate the program.

#### Known Issues

- When running the program in Windows Terminal, emojis are not displayed correctly (shown as `??`), both in the input area and the log area. No other anomalies have been observed; cursor movement behaves correctly and file logging works as expected.  
  *Similar behavior occurs in Windows Command Prompt (cmd), and emojis cannot be entered when using the integrated terminal in VSCode. These are not considered program bugs and are not planned for resolution.*

### v5.0.0

- Fixed the issue where pressing the up arrow key while using `ConsoleWrapper` might not immediately bring up the previous command input (i.e., the second previous input appears first, and the previous input can be found by pressing the down arrow key once).
- Fixed the issue whereby the first use of certain auto-complete results with `ConsoleWrapper` would not respond, and subsequent uses would result in misaligned auto-completion.
- Fixed the issue when using `ConsoleWrapper`, after the program started and before `ConsoleWrapper.ReadLineAsync` or its equivalent method was called, the background thread kept looping without waiting, causing the CPU to remain fully loaded. **This issue affects all versions with version number 4.x.**
- Fixed the issue when using `ConsoleWrapper`, if the console window size changes (becomes smaller) suddenly, input won't function properly and exceptions are thrown too frequently.
- Fixed the issue whereby the `ConsoleWrapper` was unable to treat a pair of Unicode surrogate characters (e.g. emojis) as a single character when performing console operations (such as cursor movement, character deletion).
- Added `CancellationToken` parameter for `ConsoleWrapper.ReadLineAsync`.
- To improve compatibility, now `ConsoleWrapper` won't set `Console.TreatControlCAsInput` as `true` while initializing. `ConsoleWrapper.ShutDownRequest` will contain meaningful `sender` and `args` also.
- Fixed the issue whereby when using `ConsoleWrapper`, if no logs were output after reading started, the `InputPrefix` being read may not be displayed.
- Fixed the issue whereby using `ConsoleWrapper` with the command prompt on Windows 7 would cause an issue when inputting multiple lines of data.
- Fixed the issue whereby using the default constructor to initialize `LoggerConfig` might result in unexpected parameters.
- Now supports adding history at its startup using `Log.Initialize` or `ConsoleWrapper.Initialize`. Notice that it's still limited by the history maximum string length policy. You can set `ConsoleWrapper.HistoryMaximumChars` before `Initialize`.
- If the `ConsoleWrapper.AutoCompleteHandler` you have set throws an exception while processing `GetSuggestions`, a warning will be displayed, and the information will be output to the `latest.errtrace.log`.
- You can now set `LogTrace.MaximumStoredChars` to control the amount of characters stored, which is used to store the content of exceptions in order to reuse the Trace ID. The default value is 4M `char`.
- Now you can change the time used for logging by including `DateTime` in the logging parameters. For more details, please refer to the corresponding method overload.
- Fixed the issue whereby the scroll progress of the history was reset when there was log output with `ConsoleWrapper` accessing input.

#### Breaking Changes

The interface API of `IAutoCompleteHandler` has been altered to provide implementers with better control. For details, refer to the definitions of `IAutoCompleteHandler` and `SuggestionResult`.

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

#### Known Issues

- In a very small number of running environments, when there is Chinese text in the input area of `ConsoleWrapper`, the `Console.GetCursorPosition` method or the `Console.CursorLeft` call becomes blocked (unless new characters are pressed multiple times, such as the right arrow key), resulting in lag or unresponsiveness. Attempts to resolve this issue have been unsuccessful.
- If a pair of Unicode surrogate characters (e.g. emojis) appeared in the input area of the ConsoleWrapper, they would display incorrectly when wrapped to the end of a line or a specific position. The actual text and cursor operations were not affected.
- `ConsoleWrapper` in all versions of this logger can't cope with `dotnet watch run` well unless you specify `--non-interactive` option for `dotnet watch`, because they both use `Console.ReadKey`.

### v4.0.2

- Added support for .NET 8.0 and compatibility with Native AOT.
- Now, when using the `BaseLogger` constructor overload `BaseLogger(LoggerConfig, LogFileConfig)` and similar overloads, you can specify `LogFileConfig.AllowFallback` to automatically use an existing log file if one has already been created.  
  `BaseLogger.LogFileExists` can be used to check if a log file with the corresponding `FileIdentifier` already exists.
- Added the Error Trace feature. You can use extension methods such as `LoggerChannel.LogErrorTrace` to output exception information. It will output the detailed exception information to `latest.errtrace.log`, while only displaying the exception message (`Exception.Message`) on the console.  
  The extension methods are available for both `LoggerChannel` and `BaseLogger`. The methods are not added to `Log`, so you need to use `LogTrace` to invoke them. The `latest.errtrace.log` file will only be created on the first call to the relevant methods. Here is an example of the effect:

  ```log
  03:06:51 <Erro:ErrorTraceTest> Error trace test raised an exception. System.NotImplementedException: This is a test error in Logger-test. (Trace ID: 38c3a0bf-b67a-457a-a5b7-8672d7fbc8a5)
  ```
- The values of `LoggerConfig` now have read-only tags removed, which means you don't always have to initialize with the constructor. Note that this does not mean that you can modify the configuration of the `Logger` after initialization; a deep copy will be automatically returned when the relevant property is called.

### v4.0.1

- Fixed the issue whereby the file created in the constructor of `BaseLogger` using the overload `BaseLogger(LoggerConfig, LogFileConfig)` could not be utilized in the overload `BaseLogger(LoggerConfig, string)`.
- Fixed the issue whereby after using the overload `BaseLogger(LoggerConfig, LogFileConfig)` in the constructor of `BaseLogger`, multiple files with the same name or only case-insensitive differences were allowed to be created without checks.

These fixes also apply to other similar overloads.

### v4.0.0

This version almost re-implements the entire logger, extracting the main functions of the static class `Log` and encapsulating them into `BaseLogger`. The original functions are not affected and some bugs have been fixed, but there may be some breaking changes.

Please note that although `BaseLogger` is a complete log implementation, you must first call `Log.Initialize` to use it. In addition, although different initialization `LoggerConfig` can be provided for each `BaseLogger`, its key fields must be consistent with the version provided when `Log.Initialize`, to keep the behavior of Logger consistent throughout the program.  
The affected fields at this stage are: `Use_Console_Wrapper`, `Use_Working_Directory`, `Max_Output_Char_Count`, `Enable_Detailed_Time`.

Main changes:

- Fixed the problem that due to the defect of the timing algorithm, the log processing background task will not wait for the specified time after processing the log.
- Fixed the problem that in most cases, the waiting time for cleaning (1.5s) will reach the maximum value, and it is still possible that the console cleaning cannot be completed.
- Fixed the problem that when not reading input by `ConsoleWrapper`, event `ConsoleWrapper.ShutDownRequest` cannot be triggered by pressing Ctrl+C, until someone starting reading input again.
- Fixed the problem that in certain conditions, `ConsoleWrapper` accepts invalid characters.
- Fixed the problem that when providing some log methods and members including `Log.PushLog` with invalid `LogLevel`, the logs will enter the handle queue and possibly make the handle thread crash.
- A new KMP-based algorithm is used to analyze color tag information, which speeds up processing. See [Benchmark data](https://github.com/YYHEggEgg/csharp-logger/blob/main/ColorLineUtil-report-github.md) for details.
- Added `LogTextWriter` that implements the abstract class `TextWriter`. But note that it uses a buffer to maintain characters that have not been wrapped; that is, all content before the newline character is not displayed in `Logger`.  
  If some external code uses `Console`'s methods and you use `ConsoleWrapper` (such as `CommandLineParser`), you must provide an instance of `LogTextWriter` for it.
- To make `sender` param not required for each time of logging, `LoggerChannel` is added. For code that implements a certain logic, it usually passes the same `sender` parameter when calling the `Log` method, in which condition you can create `LoggerChannel` with this `sender` to reduce the amount of code and increase maintainability.
- Some static methods pointing to the corresponding methods of `BaseLogger` were created in the `Log` static class.
- Now you can set whether to enable time details by setting `LoggerConfig.Enable_Detailed_Time`. By default, the time recorded by Logger is only accurate to the second and does not include the date, corresponding to the formatted string `HH:mm:ss`.  
  After enabling time details, the details of the log submission time up to one-seventh of a second will be displayed. The corresponding formatted string is `yyyy-MM-dd HH:mm:ss fff ffff`. The two parts `fff` and `ffff` represent the millisecond level and the ten-thousandth of a millisecond (100 nanoseconds, 0.1 microseconds) level, such as `2023-08-22 15:43:36 456 4362`. This configuration requires global unity and is effective for both console and log file output.
- When creating a new log file, you can use `LogFileConfig.IsPipeSeparatedFormat` to indicate whether the created log file is a pipe-separated value file (PSV).  
  Outputting the log as a table is helpful for analysis when the data volume is extremely large, especially if a large amount of modular code in the program does not change the sender parameter when calling the Log method. This configuration does not affect the content output to the console, and for some performance considerations, currently only `|` is accepted as the separator. The output format is:

  ```log
  [time]|[level]|[sender]|[content]
  23:14:17|Warn|TestSender|Push a warning log!
  ```

- Now you can provide a `basedlogger` parameter for `LogTraceListener` and `LogTextWriter` to change their output target `BaseLogger`. The default value is still `Log.GlobalBasedLogger` shared by the global static class `Log`.
- Some codes of [tonerdo/readline](https://github.com/tonerdo/readline) are used to implement the new version of `ConsoleWrapper`. But please note that this version of the nuget package does not include references to the nuget package [ReadLine](https://www.nuget.org/packages/ReadLine), nor does it include the `ReadLine` class. All operations are still encapsulated as `ConsoleWrapper`.
- The experience of modifying text when the text volume is large in `ConsoleWrapper` has been greatly improved.
- Now setting `ConsoleWrapper.AutoCompleteHandler` can auto-complete user input.
- Optimized the method indication in the `Log`, `BaseLogger` and `LoggerChannel` classes.
- Now you can indicate that the content of the current line should not be included in the command input history by using `ConsoleWrapper.ReadLine(false)`. This is useful when prompting the user to input information that is specific to a certain context (e.g., a confirmation prompt like `Type 'y' to confirm, 'n' to refuse`).

#### Breaking changes

- The property `Log.CustomConfig` is renamed to `Log.GlobalConfig`, and an access attempt to `Log.GlobalConfig` before invoking `Log.Initialize` will raise `InvalidOperationException`.
- Now if the user indicates to use the program path (provided `false` for `LoggerConfig.Use_Working_Directory`) during `Log.Initialize` and it cannot be accessed, compared with the previous implementation, it will not issue a warning that the program will fallback to the working directory. Similarly, there will be no warning prompts if an error occurs when compressing past log files.
- **IMPORTANT**: In the new version, the default color Logger using in Console is changed to `ConsoleColor.Gray` to be consistent to most Console implements. You can also change this setting by `Log.GlobalDefaultColor` property now.
- **IMPORTANT**: In the new version, when `Log.Initialize` processing past logs, not only `latest.log` and `latest.debug.log`, but **any log files under the `logs` folder that match the pattern `latest.*.log`** will be renamed to the last time they were written.  
  They are also affected by automatic log compression, but this is actually consistent with past behavior.
- Now if invoking `Log.Initialize` or `BaseLogger.Initialize` on platforms not supporting the Console with `LoggerConfig.Console_Minimum_LogLevel` not set to `LogLevel.None`, an exception will be immerdiately thrown.  
  You can also use this logger on these platforms, but outputing logs to the Console should be disabled. Windows, macOS, Linux are treated as supported platforms currently.
- Now at the end of the program, the leave time given to Logger is 1000ms, and the leave time given to console output (whether it is normal output or `ConsoleWrapper`) is 500ms (it will only be performed after Logger's cleaning work is completed). In previous versions, these two numbers were 300ms and 1200ms respectively.
- The color judgment uses a new algorithm, so it may not be "bug compatible" with the previous implementation.
- `ConsoleWrapper` uses a new algorithm, developers try to put the breaking changes caused by it below, but the fixed bugs may not be listed.
- `ConsoleWrapper` used to support pasting multi-line text into the input with newline characters reserved (although there will be problems when modifying the text); now it does not support this function, and all newline characters pasted will be replaced with two spaces.
  > The list of recognized newline sequences is CR (U+000D), LF (U+000A), CRLF (U+000D U+000A), NEL (U+0085), LS (U+2028), FF (U+000C), and PS (U+2029). This list is given by the Unicode Standard, Sec. 5.8, Recommendation R4 and Table 5-2.

  However, due to some terminals overriding the `Ctrl`+`V` shortcut, if your program utilizes the "converting newline characters to spaces for a single input" feature, you may need to prompt the user to use the `Ctrl`+`Alt`+`V` shortcut to trigger the built-in paste mechanism. When the clipboard text is large, using the built-in paste can significantly improve the performance of the input box.  
  The following table lists the support for various consoles when the shortcut keys in the table header reach `ConsoleWrapper`. "Supported" means that newline characters will be correctly converted to two spaces; "Not supported" means that the terminal overrides the shortcut key, causing newline characters in the clipboard to become carriage returns; "Blocked" means that the terminal neither overrides the shortcut key as paste nor passes the key to the program, i.e., the key is completely ignored (mostly due to conflicts with other shortcut keys). The test environment is Windows 11.

  | Terminal Type | `Ctrl`+`V` | `Ctrl`+`Shift`+`V` | `Ctrl`+`Alt`+`V` | `Windows`+`V` (Paste selected clipboard record) |
  | :----------------- | ------ | ------ | ------ | ------ |
  | Windows cmd.exe | Supported | Supported | Supported | Supported |
  | Windows powershell.exe | Supported | Supported | Supported | Supported |
  | Windows Terminal | Not supported | Not supported | Supported | Not supported |
  | VSCode Integrated Terminal (powershell, windows) | Supported | Not supported | Blocked | Supported |
  | VSCode Integrated Terminal (cmd, windows) | Not supported | Not supported | Blocked | Not supported |
  | VSCode Integrated Terminal (Git Bash MINGW64, windows) | Not supported | Not supported | Blocked | Not supported |
  | VSCode Integrated Terminal (Javascript Debug Terminal, windows) | Supported | Not supported | Blocked | Supported |
  | VSCode Integrated Terminal (Bash, Remote) | Not supported | Not supported | Blocked | Not supported |
  | Visual Studio Developer Powershell Integrated Terminal | Supported | Supported | Blocked | Supported |
  | Visual Studio Developer Command Prompt Integrated Terminal | Supported | Not supported | Blocked | Supported |

  In summary, if your program does rely on this mechanism, please prompt the user to use `Ctrl`+`Alt`+`V` in Windows Terminal. In other environments, `Ctrl`+`V` can be used.

- `ConsoleWrapper` now supports history merging similar to bash, that is, executing a command multiple times in a row will not leave multiple records in history.
- `ConsoleWrapper` will no longer include unconfirmed input in the command history. Therefore, pressing the up/down arrow keys will not include unconfirmed input. For example, if you enter `1[Enter]2[Enter][Up Arrow][Enter]` **without any thread waiting for `ConsoleWrapper.ReadLine`**, the third call to `ConsoleWrapper.ReadLine` will return an empty string (in previous versions, it would return `2`).
- The new reading method of `ConsoleWrapper` is similar to [GNU Readline](https://en.wikipedia.org/wiki/GNU_Readline), but this causes a conflict with the past behavior of Ctrl+C, so the function of Ctrl+C triggering the `ConsoleWrapper.ShutDownRequest` event is retained. In addition to this, all other functions of the attached GNU readline shortcut keys can be regarded as breaking changes.
- In order to support the input of Chinese and full-width characters and other wide characters, while avoiding the processing of various corner cases, now `ConsoleWrapper` reduces the number of characters that can fit in a single line by 1 bit, which is reserved as the extra space for full-width characters.
- Now, if a command input line have characters exceeding the console window, it won't be recorded in the input history. You can set `ConsoleWrapper.HistoryMaximumChars` to change the default behaviour.  
  Set it to a positive value can apply the limit, 0 can stop the history (since then), and -1 can cancel the limit. 
- This project will not be backward compatible with any version below `.NET 6.0` in the future, including `.NET Standard 2.1`.

## Features

- Basic log implementation  
  Usage: First `Log.Initialize(LoggerConfig)`, then `Log.Info(content, sender)`, `Log.Erro(...)`、`Log.Warn(...)`、`Log.Dbug(...)`. You can also first call `Log.GetChannel(sender)` to determine a `sender` parameter, and then call `logchan.LogInfo(content)`.
- **Color output Support**  
  Just add xml tags in text, like:`<color=Red>Output as red color</color>`.  
  The Color value should be a valid value in [ConsoleColor](https://learn.microsoft.com/en-us/dotnet/api/system.consolecolor), e.g. "Red", "Green". Recognized color tags will be removed in the log file.
- **Parallel user input/log output support**  
  If you want to read the user's input while outputting logs in parallel (e.g. making a CLI program), `ConsoleWrapper` is provided.  
  You can set the value of `ConsoleWrapper.InputPrefix` as a waiting-input prefix, just like `mysql>` or `ubuntu ~$`, and use `ConsoleWrapper.ReadLineAsync` to read inputs from the user. **You can also set `ConsoleWrapper.AutoCompleteHandler` and enable auto-completion of user's input**.  
  It accepts some [GNU readline](https://en.wikipedia.org/wiki/GNU_Readline) shortcut keys to read data, but comparing with other .NET GNU readline implementions, **it fully supports Chinese and full-width characters input**. The following table is the support list:

  | Shortcut key | Function |
  | ------------------------------ | --------------------------------- |
  | `Ctrl`+`A` / `HOME`            | Go to the beginning of the input |
  | `Ctrl`+`B` / `←`               | Move the cursor back one character (usually move left) |
  | `Ctrl`+`C`                     | **Different from GNU readline, it means to terminate the program** |
  | `Ctrl`+`E` / `END`             | Go to the end of the input |
  | `Ctrl`+`F` / `→`               | Move the cursor forward one character (usually move right) |
  | `Ctrl`+`H` / `Backspace`       | Delete one character before the cursor |
  | `Tab`                          | Auto complete |
  | `Shift`+`Tab`                  | Previous auto complete |
  | `Ctrl`+`J` / `Enter`           | Confirm input |
  | `Ctrl`+`K`                     | Cut off the characters from the cursor position (including) to the end of the input |
  | `Ctrl`+`L`                     | Clear the input area |
  | `Ctrl`+`M`                     | The effect is equivalent to enter |
  | `Ctrl`+`N` / `↓`               | Next command history |
  | `Ctrl`+`P` / `↑`               | Previous command history |
  | `Ctrl`+`U`                     | Cut off the characters from the cursor position (not including) to the beginning of the input |
  | `Ctrl`+`W`                     | Cut off a word before the cursor, that is, cut off the characters from the cursor position (not including) to the first space behind (usually left) (not including) |
  | ~~`Ctrl`+`T`~~                 | Abandoned due to the introduction of Chinese, full-width input support ~~Transpose (reverse) the character before the current cursor position with the character after it, and move the cursor forward one position~~ |
  | `Backspace`                    | Delete one character before the cursor |
  | `Ctrl`+`D` / `Delete`          | Delete one character at the cursor position |

  In addition, it can read data larger than 4096 bits without any configuration. But please note that when the user's input is very large and the console frequently outputs logs, it may affect performance. It is disabled by default, and you can enable it through `LoggerConfig(use_Console_Wrapper: true)`.
- Output Quantity Limit  
  Outputting a large amount of information to the console can severely impact performance. You can set the maximum output limit for each log message using `LoggerConfig.Max_Output_Char_Count`. If a log message exceeds this value, its content will not be displayed in the console, and a special warning will be shown instead. You can find the content in the log file.  
  You can also disable this feature by setting it to `-1`.
- Auto Compress Logs  
  If there are logs created 1 day ago, they will be compressed into a zip file like `logs.[Date].zip`.
- Record logs as a table  
  When creating each new log file, you can use `LogFileConfig.IsPipeSeparatedFormat` to indicate whether the log file should be in Pipe-separated values format (PSV).  
  Outputting logs as a table helps with analysis when dealing with large amounts of data, especially if the Log method is called by modularized code that does not change the sender parameter. This configuration does not affect the content output to the console, and currently only accepts `|` as the separator for performance reasons. The output format is:

  ```log
  [time]|[level]|[sender]|[content]
  23:14:17|Warn|TestSender|Push a warning log!
  ```

## Best Practices

1. Call `Log.Initialize(LoggerConfig)` at the entry point of the program (or anywhere else, the earlier the better) to initialize globally, then start using the log related methods.
2. Be careful to configure the various functions of `LoggerConfig`. If you just want to use it as a regular logger, here is a recommended configuration:

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

3. If you want to use the `ConsoleWrapper` function, you need to set the `use_Console_Wrapper` in the above `LoggerConfig` to true, and then start using its function.
4. When creating a new log file, you can use `LogFileConfig.IsPipeSeparatedFormat` to indicate whether the created log file is a pipe-separated value file (PSV).  
   Outputting the log as a table is helpful for filtering and analysis when the data volume is extremely large, especially if a large amount of modular code in the program does not change the sender parameter when calling the Log method. You can use `BaseLogger(LoggerConfig, LogFileConfig)` to create a `BaseLogger` and its log file specifically for statistical data, and make `content` also use a similar PSV format for data query.
5. You can enable the time detail of the log by setting `enable_Detailed_Time` in `LoggerConfig` to true. By default, the time recorded by Logger is only accurate to the second and does not include the date, corresponding to the formatted string `HH:mm:ss`.  
  After enabling time details, it will display the details of the log submission time up to one-seventh of a second, and the corresponding formatted string is `yyyy-MM-dd HH:mm:ss fff ffff`, the two parts `fff` and `ffff` represent the millisecond level and the ten-thousandth of a millisecond (100 nanoseconds, 0.1 microseconds) level, such as `2023-08-22 15:43:36 456 4362`. This configuration requires global unity and is effective for both console and log file output.
6. If the program uses [CommandLineParser](https://www.nuget.org/packages/CommandLineParser), please redirect its output `TextWriter`. Use code as follows:

   ```cs
   public readonly static Parser DefaultCommandsParser = new Parser(config =>
   {
       // Set custom ConsoleWriter during construction
       config.HelpWriter = TextWriter.Synchronized(new LogTextWriter("CommandLineParser"));
   });
   ```

7. If you're using `ConsoleWrapper`, you'd better subscribe event `ConsoleWrapper.ShutDownRequested` to do certain responses when the user pressing Ctrl+C. Notice that `sender` and `args` provided by the `EventHandler` make no sense.
