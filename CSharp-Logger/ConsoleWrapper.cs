using Internal.ReadLine;
using System.Collections.Concurrent;
using YYHEggEgg.Logger.readline.Abstractions;
using YYHEggEgg.Logger.Utils;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
namespace YYHEggEgg.Logger
{
    /// <summary>
    /// A wrapper for Console to provide a stable command line.
    /// </summary>
    public class ConsoleWrapper
    {
        private static List<string> lines; // 记录每行输入的列表
        private static ConcurrentQueue<string> readqueue = new();
        public static event EventHandler? ShutDownRequest; // 退出事件

        /// <summary>
        /// The refresh time for <see cref="ReadLine"/> waiting input.
        /// <para/>It should refer to milliseconds not ticks, but it won't change since it has been published.
        /// </summary>
        public static int RefreshTicks { get; set; }
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
            Console.CancelKeyPress += Console_CancelKeyPress;
            InputPrefix = "";
            RefreshTicks = 2;

            Console.TreatControlCAsInput = true; // 允许Ctrl+C被视为输入

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
        private static string _inputPrefix;
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
        public static async Task<string> ReadLineAsync()
        {
            AssertInitialized();

            string? result;
            while (!readqueue.TryDequeue(out result))
            {
                isReading = true;
                await Task.Delay(RefreshTicks);
            }

            isReading = false;

            return result;
        }

        public static string ReadLine()
        {
            AssertInitialized();

            string? result;
            while (!readqueue.TryDequeue(out result))
            {
                isReading = true;
                Thread.Sleep(RefreshTicks);
            }

            isReading = false;

            return result;
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

        #region Update Background
        private static void ShutdownRequest_Callback()
            => ShutDownRequest?.Invoke(null, EventArgs.Empty);

        private static DelayConsole shared_absconsole = new();
        private static ConcurrentQueue<ConsoleKeyInfo> qhandle_consolekeys = new();
        private static KeyHandler keyHandler = new(shared_absconsole, lines, null, string.Empty);
        private static async Task BackgroundReadkey()
        {
            try
            {
                while (true)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey(true);
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
                        keyHandler.ClearWrittingStatus();
                    }
                    if (cur_handle_writelines)
                    {
                        while (writelines.TryDequeue(out var line))
                            InnerWriteLine(line);
                        while (writelines_handlelist.TryDequeue(out var line))
                            InnerWriteLine(line);
                        if (isReading) shared_absconsole.Resync();
                    }
                    if (isReading)
                    {
                        string cur_prefix = isReading ? InputPrefix : string.Empty;
                        if (cur_handle_writelines || _autoCompleteHandler_updated)
                        {
                            pre_reading = isReading;
                            keyHandler = KeyHandler.RecoverWrittingStatus(
                                cur_prefix, keyHandler, _autoCompleteHandler);
                        }
                        while (qhandle_consolekeys.TryDequeue(out var keyInfo))
                        {
                            if (keyInfo.Modifiers == ConsoleModifiers.Control && keyInfo.Key == ConsoleKey.C)
                            {
                                ShutdownRequest_Callback();
                                continue;
                            }
                            else if (keyInfo.Key != ConsoleKey.Enter)
                            {
                                keyHandler.Handle(keyInfo);
                                continue;
                            }
                            Console.WriteLine();
                            readqueue.Enqueue(keyHandler.Text);
                            if (lines.Count == 0 || lines[0] != keyHandler.Text)
                                lines.Add(keyHandler.Text);
                            keyHandler = new(shared_absconsole, lines, _autoCompleteHandler, cur_prefix);
                        }
                    }
                    await Task.Delay(50);
                }
                catch { }
            }
        }
        #endregion
    }
}
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。