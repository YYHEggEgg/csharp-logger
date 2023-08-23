using Cyjb;
using Internal.ReadLine.Abstractions;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using TextCopy;

namespace Internal.ReadLine
{
    internal class KeyHandler
    {
        private int _cursorPos;
        private int _cursorLimit;
        private StringBuilder _text;
        private List<string> _history;
        internal int _historyIndex;
        private ConsoleKeyInfo _keyInfo;
        private Dictionary<string, Action> _keyActions;
        private string[]? _completions;
        private int _completionStart;
        private int _completionsIndex;
        private IConsole Console2;
        private string _prompt;
        private IAutoCompleteHandler? _autoCompleteHandler;

        /// <summary>
        /// 用户敲击 Control+C (^C) 时发生。
        /// </summary>
        public event Action? EOFSent;

        #region Writting Area length calculate
        private int _display_len = 0;
        private int _target_consolewidth;
        private void AddChar(char ch)
        {
            var actual_ch_len = ch.Width();
            if (_target_consolewidth - (_display_len % _target_consolewidth) >= 2)
                _display_len += actual_ch_len;
            else _display_len = (int)Math.Ceiling((double)_display_len / _target_consolewidth) 
                    * _target_consolewidth + actual_ch_len;
        }

        /// <summary>
        /// 模拟控制台计算输入区的字符占用长度。
        /// </summary>
        /// <param name="_text_index_limit">从 0 开始，计算截止到的字符数。左闭右开区间（for 循环的常见规则）。</param>
        /// <returns></returns>
        private int CalcWritingAreaLen(int _text_index_limit = -1)
        {
            if (_text_index_limit == -1) _text_index_limit = _text.Length;
            _display_len = 0;
            _target_consolewidth = Console2.BufferWidth;
            foreach (var ch in _prompt) AddChar(ch);
            for (int i = 0; i < _text_index_limit; i++) AddChar(_text[i]);
            return _display_len;
        }
        #endregion

        /// <summary>
        /// 是否为整个输入内容的开头。
        /// </summary>
        /// <returns></returns>
        private bool IsStartOfLine() => _cursorPos == 0;

        /// <summary>
        /// 是否为整个输入内容的结尾。
        /// </summary>
        /// <returns></returns>
        private bool IsEndOfLine() => _cursorPos == _cursorLimit;

        /// <summary>
        /// 控制台光标是否在当前行的开头。
        /// </summary>
        /// <returns></returns>
        private bool IsStartOfBuffer() => Console2.CursorLeft == 0;

        /// <summary>
        /// 控制台光标是否在当前行的结尾。
        /// </summary>
        /// <returns></returns>
        private bool IsEndOfBuffer() => Console2.CursorLeft == Console2.BufferWidth - 1;
        private bool IsInAutoCompleteMode() => _completions != null;

        /// <summary>
        /// 如果没有到达输入内容的最左端，将光标后退一格。
        /// </summary>
        private void MoveCursorLeftCore()
        {
            if (IsStartOfLine())
                return;

            if (IsStartOfBuffer())
            {
                var prelen = CalcWritingAreaLen(_cursorPos);
                if (prelen % Console2.BufferWidth == Console2.BufferWidth - 1
                    && _text[_cursorPos - 1].Width() == 2)
                    Console2.SetCursorPosition(
                        Console2.BufferWidth - 3, Console2.CursorTop - 1);
                else Console2.SetCursorPosition(
                        Console2.BufferWidth - 2, Console2.CursorTop - 1);
            }
            else
                Console2.SetCursorPosition(
                    Console2.CursorLeft - _text[_cursorPos - 1].Width(), Console2.CursorTop);

            _cursorPos--;
        }

        private void MoveCursorLeft()
        {
            int _console_cursorLeft_tmp = Console2.CursorLeft;
            int _console_cursorTop_tmp = Console2.CursorTop;
            int _cursorPos_tmp = _cursorPos;
            try
            {
                MoveCursorLeftCore();
            }
            catch (ArgumentOutOfRangeException)
            {
                Console2.SetCursorPosition(_console_cursorLeft_tmp, _console_cursorTop_tmp);
                _cursorPos = _cursorPos_tmp;
            }
        }

        private void MoveCursorHome()
        {
            int _console_cursorLeft_tmp = Console2.CursorLeft;
            int _console_cursorTop_tmp = Console2.CursorTop;
            int _cursorPos_tmp = _cursorPos;
            try
            {
                while (!IsStartOfLine())
                    MoveCursorLeftCore();
            }
            catch (ArgumentOutOfRangeException)
            {
                Console2.SetCursorPosition(_console_cursorLeft_tmp, _console_cursorTop_tmp);
                _cursorPos = _cursorPos_tmp;
            }
        }

        /// <summary>
        /// 以 <c>{modifier:Control/Shift}{key}</c> 的格式将输入转换为 <see cref="_keyActions"/>
        /// 使用的专有格式。
        /// </summary>
        /// <returns></returns>
        private string BuildKeyInput()
        {
            return (_keyInfo.Modifiers != ConsoleModifiers.Control && _keyInfo.Modifiers != ConsoleModifiers.Shift) ?
                _keyInfo.Key.ToString() : _keyInfo.Modifiers.ToString() + _keyInfo.Key.ToString();
        }

        /// <summary>
        /// 如果没有到达输入内容的最右端，将光标前进一格。
        /// </summary>
        private void MoveCursorRight()
        {
            if (IsEndOfLine())
                return;

            if (IsEndOfBuffer())
                Console2.SetCursorPosition(0, Console2.CursorTop + 1);
            else
            {
                if (Console2.CursorLeft + _text[_cursorPos].Width() >= Console2.BufferWidth - 1)
                    Console2.SetCursorPosition(0, Console2.CursorTop + 1);
                else Console2.SetCursorPosition(
                    Console2.CursorLeft + _text[_cursorPos].Width(), Console2.CursorTop);
            }

            _cursorPos++;
        }

        private void MoveCursorEnd()
        {
            while (!IsEndOfLine())
                MoveCursorRight();
        }

        /// <summary>
        /// 同时在控制台与 <see cref="_text"/> 清除用户输入的内容。
        /// </summary>
        private void ClearLine()
        {
            MoveCursorEnd();
            while (!IsStartOfLine())
                Backspace();
        }

        /// <summary>
        /// 将目前的用户输入内容以 <paramref name="str"/> 取代。
        /// </summary>
        /// <param name="str"></param>
        private void WriteNewString(string str)
        {
            ClearLine();
            foreach (char character in str)
                WriteChar(character);
        }

        /// <summary>
        /// 在用户输入后追加 <paramref name="str"/> 的内容。
        /// </summary>
        /// <param name="str"></param>
        private void WriteString(string str)
        {
            foreach (char character in str)
                WriteChar(character);
        }

        /// <summary>
        /// 将该实例创建时提供的 <see cref="ConsoleKeyInfo.KeyChar"/> 写入控制台。
        /// </summary>
        private void WriteChar() => WriteChar(_keyInfo.KeyChar);

        /// <summary>
        /// 利用特殊的规则（强制控制台每行最后留出一格供以全角字符操作），将字符写入控制台。
        /// </summary>
        private void ConsoleWriteChar(char ch)
        {
            Console2.Write(ch);
            if (Console2.CursorLeft == Console2.BufferWidth - 1)
            {
                Console2.Write("  ");
                Console2.SetCursorPosition(0, Console2.CursorTop);
            }
        }

        /// <summary>
        /// 利用特殊的规则（强制控制台每行最后留出一格供以全角字符操作），将字符写入控制台。
        /// </summary>
        /// <param name="startindex">从 0 开始，计算截止到的字符数。左闭右开区间（for 循环的常见规则）。</param>
        private void ConsoleWriteString(string str, int startindex = 0)
        {
            int _tmpcursorleft = Console2.CursorLeft;
            int _cutsb_start = startindex;
            for (int i = startindex; i < str.Length; i++)
            {
                _tmpcursorleft += str[i].Width();
                if (_tmpcursorleft >= Console2.BufferWidth - 1)
                {
                    Console2.Write($"{str.Substring(_cutsb_start, i - _cutsb_start + 1)}  ");
                    Console2.SetCursorPosition(0, Console2.CursorTop);
                    _tmpcursorleft = Console2.CursorLeft;
                    _cutsb_start = i + 1;
                }
            }
            Console2.Write(str.Substring(_cutsb_start, str.Length - _cutsb_start));
        }

        /// <summary>
        /// 利用特殊的规则（强制控制台每行最后留出一格供以全角字符操作），将字符写入控制台。
        /// </summary>
        /// <param name="startindex">从 0 开始，计算截止到的字符数。左闭右开区间（for 循环的常见规则）。</param>
        private void ConsoleWriteStringBuilder(StringBuilder sb, int startindex = 0)
        {
            int _tmpcursorleft = Console2.CursorLeft;
            int _cutsb_start = startindex;
            for (int i = startindex; i < sb.Length; i++)
            {
                _tmpcursorleft += sb[i].Width();
                if (_tmpcursorleft >= Console2.BufferWidth - 1)
                {
                    Console2.Write($"{sb.ToString(_cutsb_start, i - _cutsb_start + 1)}  ");
                    Console2.SetCursorPosition(0, Console2.CursorTop);
                    _tmpcursorleft = Console2.CursorLeft;
                    _cutsb_start = i + 1;
                }
            }
            Console2.Write(sb.ToString(_cutsb_start, sb.Length - _cutsb_start));
        }

        private void WriteChar(char c)
        {
            if (IsEndOfLine())
            {
                _text.Append(c);
                Console2.Write(c);
                _cursorPos++;
                if (Console2.CursorLeft == Console2.BufferWidth - 1)
                {
                    Console2.Write("   ");
                    Console2.SetCursorPosition(0, Console2.CursorTop);
                }
            }
            else
            {
                int left = Console2.CursorLeft;
                int top = Console2.CursorTop;
                string str = _text.ToString().Substring(_cursorPos);
                _text.Insert(_cursorPos, c);
                ConsoleWriteChar(c);
                ConsoleWriteString(str);
                Console2.SetCursorPosition(left, top);
                MoveCursorRight();
            }

            _cursorLimit++;
        }

        /// <summary>
        /// 同时在控制台与 <see cref="_text"/> 删去用户输入在光标位置前的一个字符。
        /// </summary>
        private void Backspace()
        {
            if (IsStartOfLine())
                return;

            MoveCursorLeft();
            int index = _cursorPos;
            _text.Remove(index, 1);
            int left = Console2.CursorLeft;
            int top = Console2.CursorTop;
            ConsoleWriteStringBuilder(_text, index);
            ConsoleWriteString("  ");
            Console2.SetCursorPosition(left, top);
            _cursorLimit--;
        }

        /// <summary>
        /// 同时在控制台与 <see cref="_text"/> 从用户输入在光标位置删去一个字符。
        /// </summary>
        private void Delete()
        {
            if (IsEndOfLine())
                return;

            int index = _cursorPos;
            _text.Remove(index, 1);
            int left = Console2.CursorLeft;
            int top = Console2.CursorTop;
            ConsoleWriteStringBuilder(_text, index);
            ConsoleWriteString("  ");
            Console2.SetCursorPosition(left, top);
            _cursorLimit--;
        }

        /// <summary>
        /// 转置（即反转）当前光标位置的前一个字符与后一个字符，并使光标随后前进一位（这里的前进应指向右）。
        /// </summary>
        private void TransposeChars()
        {
            // local helper functions
            bool almostEndOfLine() => (_cursorLimit - _cursorPos) == 1;
            int incrementIf(Func<bool> expression, int index) => expression() ? index + 1 : index;
            int decrementIf(Func<bool> expression, int index) => expression() ? index - 1 : index;

            if (IsStartOfLine()) { return; }

            var firstIdx = decrementIf(IsEndOfLine, _cursorPos - 1);
            var secondIdx = decrementIf(IsEndOfLine, _cursorPos);

            var secondChar = _text[secondIdx];
            _text[secondIdx] = _text[firstIdx];
            _text[firstIdx] = secondChar;

            var left = incrementIf(almostEndOfLine, Console2.CursorLeft);
            var cursorPosition = incrementIf(almostEndOfLine, _cursorPos);

            WriteNewString(_text.ToString());

            Console2.SetCursorPosition(left, Console2.CursorTop);
            _cursorPos = cursorPosition;

            MoveCursorRight();
        }

#pragma warning disable CS8602 // 解引用可能出现空引用。
        private void StartAutoComplete()
        {
            if (!IsInAutoCompleteMode()) return;
            while (_cursorPos > _completionStart)
                Backspace();

            _completionsIndex = 0;

            WriteString(_completions[_completionsIndex]);
        }

        private void NextAutoComplete()
        {
            if (!IsInAutoCompleteMode()) return;
            while (_cursorPos > _completionStart)
                Backspace();

            _completionsIndex++;

            if (_completionsIndex == _completions.Length)
                _completionsIndex = 0;

            WriteString(_completions[_completionsIndex]);
        }

        private void PreviousAutoComplete()
        {
            if (!IsInAutoCompleteMode()) return;
            while (_cursorPos > _completionStart)
                Backspace();

            _completionsIndex--;

            if (_completionsIndex == -1)
                _completionsIndex = _completions.Length - 1;

            WriteString(_completions[_completionsIndex]);
        }
#pragma warning restore CS8602 // 解引用可能出现空引用。

        private void PrevHistory()
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                WriteNewString(_history[_historyIndex]);
            }
        }

        private void NextHistory()
        {
            if (_historyIndex < _history.Count)
            {
                _historyIndex++;
                if (_historyIndex == _history.Count)
                    ClearLine();
                else
                    WriteNewString(_history[_historyIndex]);
            }
        }

        private void ResetAutoComplete()
        {
            _completions = null;
            _completionsIndex = 0;
        }

        public string Text
        {
            get
            {
                return _text.ToString();
            }
        }

        public KeyHandler(IConsole console, List<string>? history, IAutoCompleteHandler? autoCompleteHandler,
            string prompt)
        {
            Console2 = console;
            _prompt = prompt;
            Console2.Write(prompt);

            _history = history ?? new List<string>();
            _historyIndex = _history.Count;

            _text = new StringBuilder();
            _keyActions = new Dictionary<string, Action>();

            _keyActions["LeftArrow"] = MoveCursorLeft;
            _keyActions["Home"] = MoveCursorHome;
            _keyActions["End"] = MoveCursorEnd;
            _keyActions["ControlA"] = MoveCursorHome;
            _keyActions["ControlB"] = MoveCursorLeft;
            _keyActions["RightArrow"] = MoveCursorRight;
            _keyActions["ControlF"] = MoveCursorRight;
            _keyActions["ControlE"] = MoveCursorEnd;
            _keyActions["Backspace"] = Backspace;
            _keyActions["Delete"] = Delete;
            _keyActions["ControlD"] = Delete;
            _keyActions["ControlH"] = Backspace;
            _keyActions["ControlL"] = ClearLine;
            _keyActions["Escape"] = ClearLine;
            _keyActions["UpArrow"] = PrevHistory;
            _keyActions["ControlP"] = PrevHistory;
            _keyActions["DownArrow"] = NextHistory;
            _keyActions["ControlN"] = NextHistory;
            _keyActions["ControlU"] = () =>
            {
                while (!IsStartOfLine())
                    Backspace();
            };
            _keyActions["ControlK"] = () =>
            {
                int pos = _cursorPos;
                MoveCursorEnd();
                while (_cursorPos > pos)
                    Backspace();
            };
            _keyActions["ControlW"] = () =>
            {
                while (!IsStartOfLine() && _text[_cursorPos - 1] != ' ')
                    Backspace();
            };
            _keyActions["ControlT"] = TransposeChars;
            _autoCompleteHandler = autoCompleteHandler;
            _keyActions["Tab"] = () =>
            {
                if (IsInAutoCompleteMode())
                {
                    NextAutoComplete();
                }
                else
                {
                    if (autoCompleteHandler == null || !IsEndOfLine())
                        return;

                    string text = _text.ToString();

                    _completionStart = text.LastIndexOfAny(autoCompleteHandler.Separators);
                    _completionStart = _completionStart == -1 ? 0 : _completionStart + 1;

                    _completions = autoCompleteHandler.GetSuggestions(text, _completionStart);
                    _completions = _completions?.Length == 0 ? null : _completions;

                    if (_completions == null)
                        return;

                    StartAutoComplete();
                }
            };

            _keyActions["ShiftTab"] = () =>
            {
                if (IsInAutoCompleteMode())
                {
                    PreviousAutoComplete();
                }
            };

            _keyActions["ShiftBackspace"] = Backspace;

            _keyActions["ControlV"] = () =>
            {
                string? res = ClipboardService.GetText();
                if (string.IsNullOrEmpty(res)) return;
                res.ReplaceLineEndings("  ");
                WriteString(res);
            };
            _keyActions["ControlC"] = () => EOFSent?.Invoke();
        }

        /// <summary>
        /// 当输入这些 <see cref="ConsoleKey"/> 时，如果 <see cref="_keyActions"/>
        /// 中没有预先定义它们的处理程序，则对这个键作忽略处理。
        /// </summary>
        public static readonly ConsoleKey[] IgnoreUndefinedKeys = new ConsoleKey[]
        {
            // 以下按键在未定义处理程序时的输入无意义。
            ConsoleKey.LeftArrow,
            ConsoleKey.RightArrow,
            ConsoleKey.UpArrow,
            ConsoleKey.DownArrow,
            ConsoleKey.Delete,
            ConsoleKey.Home,
            ConsoleKey.End,
            // 回车键应由外部逻辑处理。
            ConsoleKey.Enter,
            // 以下按键应永远忽略。
            ConsoleKey.Clear,
            ConsoleKey.Pause,
            ConsoleKey.PageUp,
            ConsoleKey.PageDown,
            ConsoleKey.Select,
            ConsoleKey.Print,
            ConsoleKey.Execute,
            ConsoleKey.PrintScreen,
            ConsoleKey.Insert,
            ConsoleKey.Delete,
            ConsoleKey.Help,
            ConsoleKey.LeftWindows,
            ConsoleKey.RightWindows,
            ConsoleKey.Applications,
            ConsoleKey.Sleep,
            ConsoleKey.F1,
            ConsoleKey.F2,
            ConsoleKey.F3,
            ConsoleKey.F4,
            ConsoleKey.F5,
            ConsoleKey.F6,
            ConsoleKey.F7,
            ConsoleKey.F8,
            ConsoleKey.F9,
            ConsoleKey.F10,
            ConsoleKey.F11,
            ConsoleKey.F12,
            ConsoleKey.F13,
            ConsoleKey.F14,
            ConsoleKey.F15,
            ConsoleKey.F16,
            ConsoleKey.F17,
            ConsoleKey.F18,
            ConsoleKey.F19,
            ConsoleKey.F20,
            ConsoleKey.F21,
            ConsoleKey.F22,
            ConsoleKey.F23,
            ConsoleKey.F24,
            ConsoleKey.BrowserBack,
            ConsoleKey.BrowserForward,
            ConsoleKey.BrowserRefresh,
            ConsoleKey.BrowserStop,
            ConsoleKey.BrowserSearch,
            ConsoleKey.BrowserFavorites,
            ConsoleKey.BrowserHome,
            ConsoleKey.VolumeMute,
            ConsoleKey.VolumeDown,
            ConsoleKey.VolumeUp,
            ConsoleKey.MediaNext,
            ConsoleKey.MediaPrevious,
            ConsoleKey.MediaStop,
            ConsoleKey.MediaPlay,
            ConsoleKey.LaunchMail,
            ConsoleKey.LaunchMediaSelect,
            ConsoleKey.LaunchApp1,
            ConsoleKey.LaunchApp2,
            ConsoleKey.Oem102,
            ConsoleKey.Process,
            ConsoleKey.Packet,
            ConsoleKey.Attention,
            ConsoleKey.CrSel,
            ConsoleKey.ExSel,
            ConsoleKey.EraseEndOfFile,
            ConsoleKey.Play,
            ConsoleKey.Zoom,
            ConsoleKey.NoName,
            ConsoleKey.Pa1,
            ConsoleKey.OemClear,
        };
        private static readonly ReadOnlyCollection<string> _ignore_undefined_querykeys;
        static KeyHandler()
        {
            List<string> ignore_querykey_list = new();
            foreach (var consoleKey in IgnoreUndefinedKeys)
            {
                ignore_querykey_list.Add(consoleKey.ToString());
                ignore_querykey_list.Add($"Control{consoleKey}");
                ignore_querykey_list.Add($"Shift{consoleKey}");
            }
            _ignore_undefined_querykeys = new(ignore_querykey_list);
        }

        public void Handle(ConsoleKeyInfo keyInfo)
        {
            _keyInfo = keyInfo;

            // If in auto complete mode and Tab wasn't pressed
            if (IsInAutoCompleteMode() && _keyInfo.Key != ConsoleKey.Tab)
                ResetAutoComplete();

            _keyActions.TryGetValue(BuildKeyInput(), out Action? action);
            if (action != null || !_ignore_undefined_querykeys.Contains(BuildKeyInput()))
            {
                action ??= WriteChar;
                action.Invoke();
                try
                {
                    Console2.Flush();
                }
                catch (ArgumentOutOfRangeException)
                {
                    Console2.Clear();
                    var str = Text;
                    _text = new();
                    _cursorPos = 0;
                    _cursorLimit = 0;
                    Console2.Write(_prompt);
                    foreach (var ch in str)
                        WriteChar(ch);
                }
            }
        }

        /// <summary>
        /// 清理当前书写的区域，但保留实例数据。在必要的操作完成后，需调用
        /// <see cref="RecoverWrittingStatus(KeyHandler)"/> 来恢复状态。
        /// </summary>
        internal void ClearWrittingStatus()
        {
            var tmp_cursor = _cursorPos;
            MoveCursorEnd();
            int calcCursor = CalcWritingAreaLen();
            _cursorPos = tmp_cursor;

            int operate_line_count = calcCursor / Console2.BufferWidth + 1;
            int current_line = Console2.CursorTop;
            for (int i = 0; i < operate_line_count; i++)
            {
                Console2.SetCursorPosition(0, current_line);
                Console2.Write(new string(' ', Console2.BufferWidth));
                if (i == operate_line_count - 1)
                    Console2.SetCursorPosition(0, current_line);
                current_line--;
                if (current_line < 0)
                    break;
            }
            Console2.Flush();
            if (!IsStartOfBuffer()) Console.WriteLine();
        }

        internal static KeyHandler RecoverWrittingStatus(string prompt, KeyHandler previous_stat,
            IAutoCompleteHandler? autoCompleteHandler)
        {
            KeyHandler keyHandler = new(previous_stat.Console2, previous_stat._history, autoCompleteHandler, prompt);

            #region Auto Completion
            if (autoCompleteHandler != null &&
                ReferenceEquals(previous_stat._autoCompleteHandler, autoCompleteHandler))
            {
                keyHandler._completions = previous_stat._completions;
                keyHandler._completionsIndex = previous_stat._completionsIndex;
                keyHandler._completionStart = previous_stat._completionStart;
            }
            #endregion

            IConsole abstract_console = previous_stat.Console2;
            keyHandler.WriteNewString(previous_stat.Text);
            for (int i = previous_stat._cursorLimit; i > previous_stat._cursorPos; i--)
                keyHandler.MoveCursorLeft();
            abstract_console.Flush();
            Debug.Assert(keyHandler._cursorPos == previous_stat._cursorPos);
            Debug.Assert(keyHandler._cursorLimit == previous_stat._cursorLimit);
            return keyHandler;
        }
    }
}
