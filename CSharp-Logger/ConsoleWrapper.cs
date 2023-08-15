using System.Collections.Concurrent;
using System.Text;
using TextCopy;
using YYHEggEgg.Logger.Utils;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
namespace YYHEggEgg.Logger
{
    /// <summary>
    /// A wrapper for Console to provide a stable command line.f
    /// </summary>
    public class ConsoleWrapper
    {
        private static List<string> lines; // 记录每行输入的列表
        private static int currentLine; // 当前所在行的标记
        private static ConcurrentQueue<string> readqueue = new();
        public static event EventHandler? ShutDownRequest; // 退出事件

        private static bool isReading;
        /// <summary>
        /// It should refer to milliseconds not ticks, but it won't change since it has been published.
        /// </summary>
        public static int RefreshTicks { get; set; }
        private static StringBuilder input;
        private static int cursor;
        private static bool _initialized = false;

        /// <summary>
        /// This method has SIDE EFFECT, so don't use <see cref="Console"/> after invoked it.
        /// <para/> If initialized before, the method will return immediately.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            lines = new List<string>();
            currentLine = 0;
            Console.CancelKeyPress += Console_CancelKeyPress;
            InputPrefix = "";
            RefreshTicks = 2;

            Console.TreatControlCAsInput = true; // 允许Ctrl+C被视为输入
            input = new();
            cursor = 0;

            Task.Run(BackgroundReadkey);
            Task.Run(BackgroundUpdate);
        }

        private static void AssertInitialized()
        {
            if (!_initialized) Initialize();
        }

        private static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            ShutDownRequest?.Invoke(null, EventArgs.Empty);
        }

        #region Refresh Prefix
        // Reference:
        // [ Can Console.Clear be used to only clear a line instead of whole console? ]
        // https://stackoverflow.com/questions/8946808/can-console-clear-be-used-to-only-clear-a-line-instead-of-whole-console
        // Applied some modifications to support only clear current line.
        private static void ClearThisLine()
        {
            // if (Console.CursorTop > 0) Console.SetCursorPosition(0, Console.CursorTop - 1);
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        /// <summary>
        /// The command line input prefix. When you invoke <see cref="ConsoleWrapper.ReadLine()"/> or <see cref="ConsoleWrapper.ReadLineAsync()"/>, <see cref="ConsoleWrapper"/> will add a prefix to the user's input.
        /// <para>For example, if this is set to "&gt; ", then user will see "&gt; " at the bottom of the console.</para>
        /// </summary>
        public static string InputPrefix { get; set; }
        private static object PrefixLock = "YYHEggEgg.Logger";
        #endregion

        #region Read & Write
        #region ReadLine
        public static async Task<string> ReadLineAsync()
        {
            AssertInitialized();

            lock (PrefixLock)
            {
                isReading = true;
                ClearWrittingArea();
                Console.Write(InputPrefix);
            }

            string? result;
            while (!readqueue.TryDequeue(out result))
            {
                await Task.Delay(RefreshTicks);
            }

            lock (PrefixLock)
            {
                isReading = false;
            }

            return result;
        }

        public static string ReadLine()
        {
            AssertInitialized();

            lock (PrefixLock)
            {
                isReading = true;
                ClearWrittingArea();
                Console.Write(InputPrefix);
            }

            string? result;
            while (!readqueue.TryDequeue(out result))
            {
                Thread.Sleep(RefreshTicks);
            }

            lock (PrefixLock)
            {
                isReading = false;
            }

            return result;
        }
        #endregion

        #region WriteLine
        private static void BeginWrite()
        {
            lock (PrefixLock)
            {
                if (isReading)
                {
                    ClearWrittingArea();
                }
            }
        }

        private static void EndWrite()
        {
            lock (PrefixLock)
            {
                Console.Write(InputPrefix);
                lock (PrefixLock)
                {
                    if (isReading)
                    {
                        Console.Write(input);
                        #region Corner case: new line handle
                        // Researches show that the cursor don't switch to the
                        // next line when it reaches the end of a line.
                        if (((InputPrefix.Length + cursor) % Console.WindowWidth == 0) 
                            && (Console.CursorLeft == Console.WindowWidth - 1))
                        {
                            Console.SetCursorPosition(0, Console.CursorTop + 1);
                        }
                        #endregion
                        RecoverCursor();
                    }
                }
            }
        }

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

            writelines.Enqueue(ColorLineUtil.AnalyzeColorText(input));
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        public static void WriteLine(string input1, string input2)
        {
            AssertInitialized();

            writelines.Enqueue(ColorLineUtil.AnalyzeColorText(input1));
            writelines.Enqueue(ColorLineUtil.AnalyzeColorText(input2));
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        public static void WriteLine(string input1, string input2, string input3)
        {
            AssertInitialized();

            writelines.Enqueue(ColorLineUtil.AnalyzeColorText(input1));
            writelines.Enqueue(ColorLineUtil.AnalyzeColorText(input2));
            writelines.Enqueue(ColorLineUtil.AnalyzeColorText(input3));
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        public static void WriteLine(IEnumerable<string> inputs)
        {
            AssertInitialized();

            foreach (var input in inputs) writelines.Enqueue(ColorLineUtil.AnalyzeColorText(input));
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

        #region Cursor Calculate
        private static void SetCursorHome()
        {
            int calcCursor = InputPrefix.Length + cursor;

            int inputlines = calcCursor / Console.WindowWidth;
            Console.SetCursorPosition(InputPrefix.Length, Console.CursorTop - inputlines);
        }

        private static void SetCursorEnd()
        {
            int calcCursor = InputPrefix.Length + input.Length;
            int forwardChars = Console.CursorLeft + input.Length - cursor + 1;

            int inputlines = calcCursor / Console.WindowWidth + 1;
            Console.SetCursorPosition(calcCursor % Console.WindowWidth,
                Console.CursorTop + forwardChars / Console.WindowWidth);
        }

        private static void RecoverCursor()
        {
            SetCursorHome();
            int calcCursor = InputPrefix.Length + cursor;
            Console.SetCursorPosition(calcCursor % Console.WindowWidth,
                Console.CursorTop + calcCursor / Console.WindowWidth);
        }

        private static int last_input_len = 0;
        private static void ClearWrittingArea()
        {
            int occupiedLength = InputPrefix.Length + last_input_len;
            while (occupiedLength >= 0)
            {
                ClearThisLine();
                occupiedLength -= Console.WindowWidth;
                if (occupiedLength >= 0)
                {
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                }
            }
            last_input_len = input.Length;
        }
        #endregion

        #region Update Background
        private static async Task BackgroundReadkey()
        {
            try
            {
                while (true)
                {
                    ConsoleKeyInfo keyInfo;
                    keyInfo = Console.ReadKey(true);
                    #region 清除整行的操作
                    if (keyInfo.Key == ConsoleKey.Enter) // 整合输入的所有字符
                    {
                        lines.Add(input.ToString());
                        readqueue.Enqueue(input.ToString());
                        currentLine = lines.Count;
                        input = new();
                        cursor = 0;
                    }
                    else if (keyInfo.Key == ConsoleKey.UpArrow) // 切换cursor至上一行
                    {
                        if (currentLine > 0)
                        {
                            currentLine--;
                            input = new(lines[currentLine]);
                            cursor = input.Length;
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.DownArrow) // 切换cursor至下一行
                    {
                        if (currentLine < lines.Count - 1)
                        {
                            currentLine++;
                            input = new(lines[currentLine]);
                            cursor = input.Length;
                        }
                    }
                    #endregion
                    else if (
                        keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers == ConsoleModifiers.Control
                    ) // 触发ShutDown事件
                    {
                        ShutDownRequest?.Invoke(null, EventArgs.Empty);
                        // return;
                    }
                    #region 主要处理光标的操作
                    else if (
                        keyInfo.Key == ConsoleKey.V && keyInfo.Modifiers == ConsoleModifiers.Control
                    ) // 粘贴剪贴板内容
                    {
                        string? res = await ClipboardService.GetTextAsync();
                        if (string.IsNullOrEmpty(res)) continue;
                        input.Insert(cursor, res);
                        cursor += res.Length;
                    }
                    else if (keyInfo.Key == ConsoleKey.Backspace) // 处理退格
                    {
                        if (input.Length > 0)
                        {
                            input.Remove(cursor - 1, 1);
                            cursor--;
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Delete)
                    {
                        if (input.Length > 0)
                        {
                            input.Remove(cursor, 1);
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Home)
                    {
                        cursor = 0;
                        cursor_moved_refresh = true;
                    }
                    else if (keyInfo.Key == ConsoleKey.End)
                    {
                        cursor = input.Length;
                        cursor_moved_refresh = true;
                    }
                    else if (keyInfo.Key == ConsoleKey.LeftArrow)
                    {
                        if (cursor > 0)
                        {
                            cursor--;
                            cursor_moved_refresh = true;
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.RightArrow)
                    {
                        if (cursor < input.Length)
                        {
                            cursor++;
                            cursor_moved_refresh = true;
                        }
                    }
                    #endregion
                    else if (keyInfo.Key != ConsoleKey.LeftWindows
                        && keyInfo.Key != ConsoleKey.RightWindows)
                    {
                        input.Insert(cursor, keyInfo.KeyChar);
                        cursor++;
                    }
                }
            }
            finally
            {
                await Task.Run(BackgroundReadkey);
            }
        }

        private static ConcurrentQueue<ColorLineResult> writelines = new();

        private static bool ending = false;
        internal static bool _clearup_completed = false;
        private static bool cursor_moved_refresh = false;
        private static async Task BackgroundUpdate()
        {
            string? prev_input = null;
            while (!ending)
            {
                try
                {
                    var inputnow = input.ToString();
                    if (inputnow == prev_input && writelines.Count == 0 && !cursor_moved_refresh)
                    {
                        if (ending)
                        {
                            _clearup_completed = true;
                            return;
                        }
                        await Task.Delay(50);
                        continue;
                    }
                    else
                    {
                        BeginWrite();
                        var arr = writelines.ToArray();
                        writelines.Clear();
                        foreach (var line in arr)
                            InnerWriteLine(line);
                        EndWrite();
                        prev_input = inputnow;
                        cursor_moved_refresh = false;
                        await Task.Delay(10);
                    }
                }
                catch { }
            }
        }
        #endregion
    }
}
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。