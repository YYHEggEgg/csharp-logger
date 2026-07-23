using Internal.ReadLine.Abstractions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace YYHEggEgg.Logger.Utils
{
    internal struct ColorLineResult
    {
        private List<(string text, ConsoleColor color)> _color_parts;
        public List<(string text, ConsoleColor color)> ColorParts
        {
            get => _color_parts;
            set
            {
                _color_parts = value;
                _text_nocolor = null;
            }
        }
        private string? _text_nocolor;
        public string TextWithoutColor
        {
            get
            {
                if (_text_nocolor != null) return _text_nocolor;
                StringBuilder sb = new();
                foreach (var strpart in _color_parts)
                {
                    sb.Append(strpart.text);
                }
                _text_nocolor = sb.ToString();
                return _text_nocolor;
            }
        }

        public ColorLineResult()
        {
            _color_parts = new();
            _text_nocolor = null;
        }

        /// <summary>
        /// 输出文字至控制台。支持使用颜色，在文本中加入xml标签即可：
        /// &lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为
        /// <see cref="ConsoleColor"/> 中的有效值，如"Red"、"Green"等。
        /// </summary>
        /// <remarks>此方法会解除 <paramref name="console2"/> 的同步状态。</remarks>
        public void WriteToConsole(IConsole console2)
        {
            foreach (var strpart in _color_parts)
            {
                Console.ForegroundColor = strpart.color;
                console2.WriteNonSync(strpart.text);
            }
            Console.ForegroundColor = ColorLineUtil.DefaultColor;
            console2.WriteLineNonSync(string.Empty);
        }

        /// <summary>
        /// 输出文字到控制台的同时，估计其文本占用了多少行。此“占用”指整个区域的大小，包括最后的换行符也会被计入一行。
        /// </summary>
        /// <returns></returns>
        public int WriteAndCountLines(IConsole console2)
        {
            int result = 1;
            // CursorTop 不是可靠的值，原因在于它只取决于用户看到的控制台
            // 窗口大小（而与旁边的滚动条无关，那是终端自己推断的扩展）；
            // 例如一直在控制台最下方写入导致窗口保持滚动，但 CursorTop
            // 不会变。因此，我们一个一个字符进行写入，如果 CursorLeft
            // 发生了非正向变化，则我们认为控制台触发了一次换行。此种方法
            // 并不保证准确性，目前仅在进度条渲染时使用。
            int preCursorLeft = console2.CursorLeft;
            int preCursorTop = console2.CursorTop;
            foreach (var strpart in _color_parts)
            {
                Console.ForegroundColor = strpart.color;
                var textElements = StringInfo.GetTextElementEnumerator(strpart.text);
                while (textElements.MoveNext())
                {
                    var textElement = textElements.GetTextElement();
                    console2.Write(textElement);
                    var currentCursorLeft = console2.CursorLeft;
                    var currentCursorTop = console2.CursorTop;

                    // CursorTop may stay unchanged while the terminal scrolls at
                    // the bottom of its buffer, so CursorLeft is still needed to
                    // detect natural wrapping. Equality is deliberately excluded:
                    // combining marks, variation selectors and other zero-width
                    // text elements commonly leave CursorLeft unchanged.
                    if (textElement.IndexOf('\n') >= 0)
                    {
                        result++;
                    }
                    else if (currentCursorTop > preCursorTop)
                    {
                        result += currentCursorTop - preCursorTop;
                    }
                    else if (textElement != "\r" && currentCursorLeft < preCursorLeft)
                    {
                        result++;
                    }

                    preCursorLeft = currentCursorLeft;
                    preCursorTop = currentCursorTop;
                }
            }
            Console.ForegroundColor = ColorLineUtil.DefaultColor;
            console2.WriteLine(string.Empty);
            return result + 1;
        }
    }

    internal static class ColorLineUtil
    {
        public static ConsoleColor DefaultColor = ConsoleColor.Gray;

        /// <summary>
        /// 在 <paramref name="obj"/> 中寻找字符串 <paramref name="content"/>，
        /// 并返回由索引组成的结果队列。
        /// </summary>
        private static Queue<int> KMPFind(string obj, string content)
        {
            int content_len = content.Length;
            int obj_len = obj.Length;

            #region nxt array init
            int[] nxt = new int[content.Length + 1];
            int i = 0, j = -1;
            nxt[0] = -1;

            while (i < content_len)
            {
                if (j == -1 || content[i] == content[j]) nxt[++i] = ++j;
                else j = nxt[j];
            }
            #endregion

            Queue<int> indexes = new();
            #region KMP
            int k = 0, m = 0;
            while (k < obj_len)
            {
                if (m == -1 || obj[k] == content[m])
                {
                    k++; m++;
                }
                else m = nxt[m];

                if (m == content_len)
                {
                    indexes.Enqueue(k - content_len);
                    m = nxt[m];
                }
            }
            #endregion

            return indexes;
        }

        private const string color_prefix = "<color=";
        private const string color_suffix = "</color>";
        public static ColorLineResult AnalyzeColorText(string input)
        {
            List<(string text, ConsoleColor color)> res = new();
            try
            {
                var color_starts = KMPFind(input, color_prefix);
                var color_ends = KMPFind(input, color_suffix);
                int last_output_end = 0;

                while (color_starts.Count > 0)
                {
                    var start = color_starts.Dequeue();
                    if (start < last_output_end) continue;
                    // Verify color is valid
                    if (!TryFindColor(input, start, out ConsoleColor? color))
                    {
                        continue;
                    }
#pragma warning disable CS8602 // 解引用可能出现空引用。
                    var actual_content_start = start + color_prefix.Length
                        + color.ToString().Length + 1;
#pragma warning restore CS8602 // 解引用可能出现空引用。

                    int end = -1;
                    while (color_ends.TryDequeue(out end))
                    {
                        if (end >= actual_content_start) break;
                        else end = -1;
                    }
                    if (end == -1) break;

                    PushResultToRes(last_output_end, start, DefaultColor);
                    PushResultToRes(actual_content_start, end, (ConsoleColor)color);
                    last_output_end = end + color_suffix.Length;
                }

                PushResultToRes(last_output_end, input.Length, DefaultColor);
            }
            catch (Exception ex)
            {
                // Color parsing is also used by ConsoleWrapper when the logger is
                // configured without disk operations (or before Log.Initialize).
                // Diagnostic logging must not turn a recoverable parse failure into
                // a failure of the sole console worker.
                try
                {
                    Log.Erro($"Content has caused exception when resolving color: ex={ex}; content (base64)=" +
                        Convert.ToBase64String(Encoding.UTF8.GetBytes(input)), nameof(ColorLineUtil));
                }
                catch
                {
                    // Keep ConsoleWrapper operational even when logging diagnostics
                    // is unavailable.
                }
                return new()
                {
                    ColorParts = new() { (input, DefaultColor) }
                };
            }
            return new()
            {
                ColorParts = res
            };

            void PushResultToRes(int start, int end, ConsoleColor color)
            {
                var length = end - start;
                Debug.Assert(length >= 0);
                if (length <= 0) return;
                res.Add((input.Substring(start, length), color));
            }
        }

        private static readonly int shortest_console_color;
        private static readonly int longest_console_color;
        static ColorLineUtil()
        {
            shortest_console_color = int.MaxValue;
            longest_console_color = int.MinValue;
            foreach (var colorname in Enum.GetNames(typeof(ConsoleColor)))
            {
                shortest_console_color = Math.Min(colorname.Length, shortest_console_color);
                longest_console_color = Math.Max(colorname.Length, longest_console_color);
            }
        }
        /// <summary>
        /// 查找 <paramref name="input"/> 中的下一个颜色信息，其中
        /// <paramref name="input"/> 规定为字符串查找 <see cref="color_prefix"/> 的位置。
        /// </summary>
        /// <param name="input"></param>
        /// <param name="offset"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private static bool TryFindColor(string input, int offset, [NotNullWhen(true)] out ConsoleColor? result)
        {
            var start_index = offset + color_prefix.Length;
            for (int i = start_index + shortest_console_color; i <= start_index + longest_console_color; i++)
            {
                if (i >= input.Length)
                {
                    result = null;
                    return false;
                }
                if (input[i] == '>')
                {
                    int length = i - start_index;
                    string res = input.Substring(start_index, length);
                    if (Enum.TryParse<ConsoleColor>(res, out var parsed) &&
                        Enum.IsDefined(typeof(ConsoleColor), parsed))
                    {
                        result = parsed;
                        return true;
                    }
                    else
                    {
                        result = null;
                        return false;
                    }
                }
            }
            result = null;
            return false;
        }
    }
}
