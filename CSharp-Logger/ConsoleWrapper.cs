﻿using Internal.ReadLine;
using Internal.ReadLine.Abstractions;
using System.Collections.Concurrent;
using System.Reflection;
using YYHEggEgg.Logger.readline.Abstractions;
using YYHEggEgg.Logger.Utils;

namespace YYHEggEgg.Logger
{
    /// <summary>
    /// Invoke when <see cref="Console.CancelKeyPress"/> is triggered or <see cref="ConsoleWrapper"/> received Ctrl+C.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The Event args. If <see cref="ConsoleWrapper"/> received the event, the program won't terminate, so it'll be null.</param>
    public delegate void ConsoleWrapperCancelKeyPressEventHandler(object? sender, ConsoleCancelEventArgs? e);

    /// <summary>
    /// A wrapper for Console to provide a stable command line.
    /// </summary>
    public class ConsoleWrapper
    {
        private static List<string> lines = null!; // 记录每行输入的列表
        private static ConcurrentQueue<string> readqueue = new();
        public static event ConsoleWrapperCancelKeyPressEventHandler? ShutDownRequest; // 退出事件

        /// <summary>
        /// The refresh time for <see cref="ReadLine"/> waiting input.
        /// <para/>It should refer to milliseconds not ticks, but it won't change since it has been published.
        /// </summary>
        public static int RefreshTicks { get; set; }
        private static bool _initialized = false;

        private static void InitAbsConsole()
        {
            shared_absconsole = new DelayConsole();
        }

        /// <summary>
        /// This method has SIDE EFFECT, so don't use <see cref="Console"/> after invoked it.
        /// <para/>
        /// If initialized before, the method will return immediately.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// <see cref="ConsoleWrapper"/> and any features
        /// related to console is not supported on current OS.
        /// </exception>
        public static void Initialize()
        {
            if (_initialized) return;
            if (!Tools.CheckIfSupportedOS())
                throw new InvalidOperationException(
                    "ConsoleWrapper and any features related " +
                    "to console is not supported on current OS.");

            _initialized = true;

            lines ??= new List<string>();
            InitAbsConsole();
            keyHandler = new(shared_absconsole, lines, null, string.Empty);
            Console.CancelKeyPress += Console_CancelKeyPress;
            InputPrefix = "";
            RefreshTicks = 2;

            Task.Run(BackgroundReadkey);
            Task.Run(BackgroundUpdate);
        }

        private static void AssertInitialized()
        {
            if (!_initialized) Initialize();
        }

        private static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            ShutDownRequest?.Invoke(sender, e);
            if (!e.Cancel) InputPrefix = string.Empty;
        }

        #region Refresh Prefix
        private static string _inputPrefix = null!;
        /// <summary>
        /// The command line input prefix. When you invoke <see cref="ConsoleWrapper.ReadLine()"/> or <see cref="ConsoleWrapper.ReadLineAsync()"/>, <see cref="ConsoleWrapper"/> will add a prefix to the user's input.
        /// <para>For example, if this is set to "&gt; ", then user will see "&gt; " at the bottom of the console.</para>
        /// </summary>
        public static string InputPrefix
        {
            get => _inputPrefix;
            set
            {
                _inputPrefix = value;
                _inputprefix_changed = true;
            }
        }
        private static object PrefixLock = "YYHEggEgg.Logger";
        #endregion

        #region Read & Write
        #region ReadLine
        public static async Task<string> ReadLineAsync(bool reserve_in_history = true, CancellationToken cancellationToken = default)
        {
            try
            {
                AssertInitialized();

                string? result;
                while (!readqueue.TryDequeue(out result))
                {
                    isReading = true;
                    await Task.Delay(RefreshTicks, cancellationToken);
                }

                AddHistoryRecord(result, reserve_in_history);

                return result;
            }
            finally
            {
                isReading = false;
            }
        }

        public static string ReadLine(bool reserve_in_history = true)
        {
            AssertInitialized();

            string? result;
            while (!readqueue.TryDequeue(out result))
            {
                isReading = true;
                Thread.Sleep(RefreshTicks);
            }

            isReading = false;
            AddHistoryRecord(result, reserve_in_history);

            return result;
        }

        private static void AddHistoryRecord(string content, bool reserve_in_history)
        {
            if (reserve_in_history)
            {
                int _history_char_limit = HistoryMaximumChars;
                if (_history_char_limit == 0) return;

                lock (lines)
                {
                    if ((lines.Count == 0 || lines[lines.Count - 1] != content)
                        && !string.IsNullOrEmpty(content)
                        && content.Length <= _history_char_limit)
                    {
                        lines.Add(content);
                        if (keyHandler._historyIndex == lines.Count - 1)
                            keyHandler._historyIndex++;
                    }
                }
            }
        }
        #endregion

        #region WriteLine
        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        private static void InnerWriteLine(string input)
        {
            if (input == null) return;
            InnerWriteLine(ColorLineUtil.AnalyzeColorText(input));
        }

        private static void InnerWriteLine(ColorLineResult input)
            => input.WriteToConsole();

        #region Outer WriteLine
        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        public static void WriteLine(string input)
        {
            AssertInitialized();

            writelines_handlelist.Enqueue(input);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        public static void WriteLine(string input1, string input2)
        {
            AssertInitialized();

            writelines_handlelist.Enqueue(input1);
            writelines_handlelist.Enqueue(input2);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        public static void WriteLine(string input1, string input2, string input3)
        {
            AssertInitialized();

            writelines_handlelist.Enqueue(input1);
            writelines_handlelist.Enqueue(input2);
            writelines_handlelist.Enqueue(input3);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        public static void WriteLine(IEnumerable<string> inputs)
        {
            AssertInitialized();

            foreach (var input in inputs) writelines_handlelist.Enqueue(input);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        public static void WriteLine(params string[] inputs) => WriteLine(inputs);

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        internal static void WriteLine(ColorLineResult input)
        {
            AssertInitialized();

            writelines.Enqueue(input);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        internal static void WriteLine(ColorLineResult input1, ColorLineResult input2)
        {
            AssertInitialized();

            writelines.Enqueue(input1);
            writelines.Enqueue(input2);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        internal static void WriteLine(ColorLineResult input1, ColorLineResult input2, ColorLineResult input3)
        {
            AssertInitialized();

            writelines.Enqueue(input1);
            writelines.Enqueue(input2);
            writelines.Enqueue(input3);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        internal static void WriteLine(IEnumerable<ColorLineResult> inputs)
        {
            AssertInitialized();

            foreach (var input in inputs) writelines.Enqueue(input);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        internal static void WriteLine(params ColorLineResult[] inputs) => WriteLine(inputs);
        #endregion
        #endregion
        #endregion

        #region History
        /// <summary>
        /// Change the history of command line. Notice that user's history will be totally replaced after invoking this.
        /// </summary>
        public static void ChangeHistory(IEnumerable<string> initHistory)
        {
            AssertInitialized();
            lock (lines)
            {
                lines.Clear();
                lines.AddRange(initHistory.Where(x => x.Length <= HistoryMaximumChars));
                if (keyHandler != null) keyHandler._historyIndex = lines.Count;
                _inputprefix_changed = true; // TODO: Custom signal. Or not to do?
            }
        }

        private static int _custom_history_limit = int.MinValue;

        /// <summary>
        /// Get or set the maximum chars that a line in input history have.
        /// If a input line has chars that exceeded this value,
        /// it will not be recorded in input history.
        /// <para/>
        /// The default value is the characters count of the whole
        /// console, and modified value won't affect the history added
        /// before setting the property. 
        /// <para/>
        /// Set it to a positive value can apply the limit,
        /// 0 can stop the history (since then), and -1 can
        /// cancel the limit. 
        /// Otherwise, <see cref="InvalidOperationException"/> is raised.
        /// </summary>
        public static int HistoryMaximumChars
        {
            get
            {
                if (_custom_history_limit == int.MinValue)
                    return Console.WindowHeight * Console.WindowWidth;
                else if (_custom_history_limit == -1)
                    return int.MaxValue;
                else return _custom_history_limit;
            }
            set
            {
                if (value < -1)
                    throw new InvalidOperationException("Value less than -1 " +
                    "is not allowed for HistoryMaximumChars. If want to disable " +
                    "the history selective ignoring, provide -1.");
                _custom_history_limit = value;
            }
        }
        #endregion

        #region Update Background
        private static void ShutdownRequest_Callback()
        {
            ShutDownRequest?.Invoke(null, null);
        }

        private static IConsole shared_absconsole = null!;
        private static ConcurrentQueue<ConsoleKeyInfo> qhandle_consolekeys = new();
        private static KeyHandler keyHandler = null!;
        private static async Task BackgroundReadkey()
        {
            try
            {
                while (true)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                    if (keyInfo.Modifiers == ConsoleModifiers.Control && keyInfo.Key == ConsoleKey.C)
                    {
                        ShutdownRequest_Callback();
                        continue;
                    }
                    qhandle_consolekeys.Enqueue(keyInfo);
                }
            }
            finally
            {
                await Task.Run(BackgroundReadkey);
            }
        }

        private static ConcurrentQueue<ColorLineResult> writelines = new();
        private static ConcurrentQueue<string> writelines_handlelist = new();

        private static bool ending = false;
        internal static bool _clearup_completed = false;

        private static bool isReading = false;
        private static bool _inputprefix_changed = false;

        private static bool Writelines_waiting_handle
            => !writelines.IsEmpty || !writelines_handlelist.IsEmpty;

        private static IAutoCompleteHandler? _autoCompleteHandler = null;
        private static bool _autoCompleteHandler_updated = true;
        public static IAutoCompleteHandler? AutoCompleteHandler
        {
            get => _autoCompleteHandler;
            set
            {
                _autoCompleteHandler = value;
                _autoCompleteHandler_updated = true;
            }
        }

        private static async Task BackgroundUpdate()
        {
            bool pre_reading = false;
            while (!ending)
            {
                try
                {
                    bool cur_handle_writelines = Writelines_waiting_handle;
                    if (qhandle_consolekeys.IsEmpty && !_inputprefix_changed
                        && !(!pre_reading && isReading) // not starting reading nearly
                        && !cur_handle_writelines && !_autoCompleteHandler_updated)
                    {
                        if (ending)
                        {
                            _clearup_completed = true;
                            return;
                        }
                        await Task.Delay(50);
                        continue;
                    }
                    _inputprefix_changed = false;

                    if (isReading && (cur_handle_writelines || _autoCompleteHandler_updated))
                    {
                        _autoCompleteHandler_updated = false;
                        // Keep the KeyHandler status
                        try
                        {
                            keyHandler.ClearWrittingStatus();
                        }
                        // Catch console suddenly smallen
                        catch (ArgumentOutOfRangeException)
                        {
                            Console.WriteLine();
                        }
                    }
                    else if (!isReading && _autoCompleteHandler_updated)
                    {
                        _autoCompleteHandler_updated = false;
                    }

                    if (cur_handle_writelines)
                    {
                        while (writelines.TryDequeue(out var line))
                            InnerWriteLine(line);
                        while (writelines_handlelist.TryDequeue(out var line))
                            InnerWriteLine(line);
                        shared_absconsole.Resync();
                    }
                    if (isReading)
                    {
                        string cur_prefix = isReading ? InputPrefix : string.Empty;
                        if (cur_handle_writelines || _autoCompleteHandler_updated || !pre_reading)
                        {
                            while (true)
                            {
                                try
                                {
                                    keyHandler = KeyHandler.RecoverWrittingStatus(
                                        cur_prefix, keyHandler, _autoCompleteHandler);
                                    break;
                                }
                                // Catch console suddenly smallen
                                catch (ArgumentOutOfRangeException)
                                {
                                    shared_absconsole.Clear();
                                    continue;
                                }
                            }
                        }
                        while (qhandle_consolekeys.TryDequeue(out var keyInfo))
                        {
                            // Ctrl+C should be handled as soon as it's read.
                            if (keyInfo.Key != ConsoleKey.Enter &&
                                (keyInfo.Modifiers != ConsoleModifiers.Control || (keyInfo.Key != ConsoleKey.M && keyInfo.Key != ConsoleKey.J)))
                            {
                                keyHandler.Handle(keyInfo);
                                continue;
                            }

                            Console.WriteLine();
                            readqueue.Enqueue(keyHandler.Text);
                            // if ((lines.Count == 0 || lines[lines.Count - 1] != keyHandler.Text)
                            //     && !string.IsNullOrEmpty(keyHandler.Text))
                            //     lines.Add(keyHandler.Text);
                            keyHandler = new(shared_absconsole, lines, _autoCompleteHandler, cur_prefix);
                        }
                    }

                    pre_reading = isReading;
                }
                catch (Exception ex)
                {
                    LogTrace.DbugTrace(ex, nameof(ConsoleWrapper), $"Internal handler meet unexpected error. Please report to EggEgg.CSharp-Logger.");
                }
            }
        }
        #endregion
    }
}
