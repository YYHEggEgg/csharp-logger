using Cyjb;
using Internal.ReadLine.Abstractions;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TextCopy;
using YYHEggEgg.Logger;

namespace Internal.ReadLine
{
    internal partial class KeyHandler
    {
        private int _cursorPos;
        private int _cursorLimit;
        private StringBuilder _text;
        private readonly List<string> _history;
        internal int _historyIndex;
        private string? _historyDraft;
        private int _historyDraftCursor;
        private ConsoleKeyInfo _keyInfo;
        private readonly Dictionary<string, Action> _keyActions;
        private SuggestionResult? _completions;
        private int _completionStart;
        private int _completionLength;
        private int _completionsIndex;
        private readonly IConsole Console2;
        private readonly string _prompt;
        private readonly IAutoCompleteHandler? _autoCompleteHandler;
        private char? _pendingHighSurrogate;
        private int _renderOriginLeft;
        private int _renderOriginTop;
        private int _renderedUsableWidth;

        /// <summary>
        /// Raised when the user presses Control+C (^C).
        /// </summary>
        public event Action? EOFSent;

        private readonly struct DisplayPosition
        {
            public DisplayPosition(int line, int column)
            {
                Line = line;
                Column = column;
            }

            public int Line { get; }
            public int Column { get; }
        }

        private int UsableWidth => Math.Max(1, Console2.BufferWidth - 1);

        private bool IsStartOfLine() => _cursorPos == 0;

        private bool IsEndOfLine() => _cursorPos == _cursorLimit;

        private bool IsInAutoCompleteMode()
        {
            return _completions?.Suggestions is { Count: > 0 } &&
                _completionStart >= 0 &&
                _completionLength >= 0 &&
                _completionStart <= _text.Length - _completionLength;
        }

        private static void AdvanceDisplayPosition(ref int line, ref int column,
            int elementWidth, int usableWidth)
        {
            if (elementWidth <= 0)
            {
                return;
            }

            elementWidth = Math.Min(elementWidth, usableWidth);
            if (column + elementWidth > usableWidth)
            {
                line++;
                column = 0;
            }

            column += elementWidth;
            if (column >= usableWidth)
            {
                line++;
                column = 0;
            }
        }

        private static void AdvanceText(ref int line, ref int column, string value,
            int length, int usableWidth)
        {
            if (length == 0)
            {
                return;
            }
            if (length < 0 || length > value.Length ||
                !CharUtil.IsTextElementBoundary(value, length))
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            int[] starts = StringInfo.ParseCombiningCharacters(value);
            for (int i = 0; i < starts.Length && starts[i] < length; i++)
            {
                int end = i + 1 < starts.Length ? starts[i + 1] : value.Length;
                int width = CharUtil.TextElementWidth(value, starts[i], end - starts[i]);
                AdvanceDisplayPosition(ref line, ref column, width, usableWidth);
            }
        }

        private DisplayPosition CalculateDisplayPosition(string text, int textIndex,
            int usableWidth)
        {
            if (textIndex < 0 || textIndex > text.Length ||
                !CharUtil.IsTextElementBoundary(text, textIndex))
            {
                throw new ArgumentOutOfRangeException(nameof(textIndex));
            }

            int line = 0;
            // The origin may be exactly at the reserved rightmost column. In
            // that case the first visible text element deliberately starts on
            // the next row instead of pretending the cursor was one cell left.
            int column = Math.Clamp(_renderOriginLeft, 0, usableWidth);
            AdvanceText(ref line, ref column, _prompt, _prompt.Length, usableWidth);
            AdvanceText(ref line, ref column, text, textIndex, usableWidth);
            return new DisplayPosition(line, column);
        }

        /// <summary>
        /// Writes complete text elements and batches all elements that fit on
        /// the same row into one console write.  This avoids both splitting a
        /// grapheme and the old per-character O(n²) paste path.
        /// </summary>
        private void WriteRenderedText(string value, int usableWidth)
        {
            if (value.Length == 0)
            {
                return;
            }

            int column = Math.Clamp(Console2.CursorLeft, 0, usableWidth);
            int[] starts = StringInfo.ParseCombiningCharacters(value);
            int chunkStart = 0;

            for (int i = 0; i < starts.Length; i++)
            {
                int start = starts[i];
                int end = i + 1 < starts.Length ? starts[i + 1] : value.Length;
                int width = Math.Min(
                    CharUtil.TextElementWidth(value, start, end - start), usableWidth);

                if (width > 0 && column + width > usableWidth)
                {
                    if (start > chunkStart)
                    {
                        Console2.Write(value.Substring(chunkStart, start - chunkStart));
                    }
                    Console2.WriteLine(string.Empty);
                    column = 0;
                    chunkStart = start;
                }

                column += width;
                if (width > 0 && column >= usableWidth)
                {
                    Console2.Write(value.Substring(chunkStart, end - chunkStart));
                    Console2.WriteLine(string.Empty);
                    column = 0;
                    chunkStart = end;
                }
            }

            if (chunkStart < value.Length)
            {
                Console2.Write(value.Substring(chunkStart));
            }
        }

        private int GetOriginTop(string oldText, int oldCursor, int currentCursorTop,
            int usableWidth)
        {
            DisplayPosition cursor = CalculateDisplayPosition(oldText, oldCursor, usableWidth);
            int originTop = currentCursorTop - cursor.Line;
            if (originTop < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentCursorTop));
            }
            return originTop;
        }

        private void ClearRenderedRows(string oldText, int originTop, int renderedUsableWidth)
        {
            DisplayPosition renderedEnd = CalculateDisplayPosition(
                oldText, oldText.Length, renderedUsableWidth);
            int usableWidth = UsableWidth;
            DisplayPosition currentEnd = CalculateDisplayPosition(
                oldText, oldText.Length, usableWidth);
            int lastLine = Math.Max(renderedEnd.Line, currentEnd.Line);
            lastLine = Math.Min(lastLine,
                Math.Max(0, Console2.BufferHeight - 1 - originTop));

            // Include the cursor row.  In particular, an input whose display
            // width is exactly BufferWidth-1 has advanced to the next row.
            // When the buffer width changed, clear both the old layout and the
            // potentially reflowed new layout so neither widening nor shrinking
            // can leave ghost rows behind.
            for (int line = 0; line <= lastLine; line++)
            {
                int startColumn = line == 0 ? _renderOriginLeft : 0;
                startColumn = Math.Clamp(startColumn, 0, usableWidth);
                Console2.SetCursorPosition(startColumn, originTop + line);
                int clearLength = usableWidth - startColumn;
                if (clearLength > 0)
                {
                    Console2.Write(new string(' ', clearLength));
                }
            }
        }

        private void SetCursorForText(string text, int cursor, int originTop,
            int usableWidth)
        {
            DisplayPosition target = CalculateDisplayPosition(text, cursor, usableWidth);
            Console2.SetCursorPosition(target.Column, originTop + target.Line);
            Console2.Flush();
            _renderOriginTop = originTop;
        }

        private void RedrawInput(string oldText, int oldCursor, int oldConsoleTop)
        {
            bool widthChanged = UsableWidth != _renderedUsableWidth;
            int originTop = widthChanged
                ? _renderOriginTop
                : GetOriginTop(oldText, oldCursor, oldConsoleTop, _renderedUsableWidth);
            ClearRenderedRows(oldText, originTop, _renderedUsableWidth);
            int usableWidth = UsableWidth;
            Console2.SetCursorPosition(_renderOriginLeft, originTop);
            Console2.Flush();

            WriteRenderedText(_prompt, usableWidth);
            WriteRenderedText(_text.ToString(), usableWidth);

            DisplayPosition newEnd = CalculateDisplayPosition(
                _text.ToString(), _text.Length, usableWidth);
            int observedEndTop = Console2.CursorTop;
            int expectedEndTop = originTop + newEnd.Line;
            if (observedEndTop != expectedEndTop)
            {
                // Account for a terminal scroll while rendering at the bottom.
                originTop += observedEndTop - expectedEndTop;
            }
            if (originTop < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(originTop));
            }

            _renderedUsableWidth = usableWidth;
            SetCursorForText(_text.ToString(), _cursorPos, originTop, usableWidth);
        }

        private void AppendRenderedInput(string oldText, int oldCursor,
            string currentText, int oldConsoleTop)
        {
            int usableWidth = UsableWidth;
            if (usableWidth != _renderedUsableWidth)
            {
                RedrawInput(oldText, oldCursor, oldConsoleTop);
                return;
            }

            int originTop = GetOriginTop(
                oldText, oldCursor, oldConsoleTop, _renderedUsableWidth);
            WriteRenderedText(currentText.Substring(oldText.Length), usableWidth);

            DisplayPosition end = CalculateDisplayPosition(
                currentText, currentText.Length, usableWidth);
            int expectedEndTop = originTop + end.Line;
            if (Console2.CursorTop != expectedEndTop)
            {
                originTop += Console2.CursorTop - expectedEndTop;
            }
            if (originTop < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(originTop));
            }
            _renderedUsableWidth = usableWidth;
            SetCursorForText(currentText, _cursorPos, originTop, usableWidth);
        }

        private void EmergencyRedraw()
        {
            try
            {
                Console2.TryClear();
                _renderOriginLeft = Math.Clamp(Console2.CursorLeft, 0, UsableWidth);
                _renderOriginTop = Console2.CursorTop;
                int usableWidth = UsableWidth;
                WriteRenderedText(_prompt, usableWidth);
                WriteRenderedText(_text.ToString(), usableWidth);

                DisplayPosition end = CalculateDisplayPosition(
                    _text.ToString(), _text.Length, usableWidth);
                int originTop = Console2.CursorTop - end.Line;
                if (originTop < 0)
                {
                    originTop = 0;
                }
                _renderedUsableWidth = usableWidth;
                SetCursorForText(_text.ToString(), _cursorPos, originTop, usableWidth);
            }
            catch
            {
                // There is no further safe cursor operation when even the
                // emergency rebuild fails (for example after terminal close).
            }
        }

        private static void TryLogWarning(Exception ex, string prompt)
        {
            try
            {
                LogTrace.WarnTrace(ex, nameof(KeyHandler), prompt);
            }
            catch
            {
                // Error tracing is optional here.  Input recovery must not
                // fault merely because disk logging is disabled or shutting down.
            }
        }

        private static int SkipCsi(string value, int index)
        {
            while (index < value.Length)
            {
                char ch = value[index++];
                if (ch >= '\x40' && ch <= '\x7E')
                {
                    break;
                }
            }
            return index;
        }

        private static int SkipControlString(string value, int index)
        {
            while (index < value.Length)
            {
                if (value[index] == '\a')
                {
                    return index + 1;
                }
                if (value[index] == '\x1B' && index + 1 < value.Length &&
                    value[index + 1] == '\\')
                {
                    return index + 2;
                }
                if (value[index] == '\x9C')
                {
                    return index + 1;
                }
                index++;
            }
            return index;
        }

        private static int SkipEscapeSequence(string value, int index)
        {
            if (index >= value.Length)
            {
                return index;
            }

            char introducer = value[index++];
            if (introducer == '[')
            {
                return SkipCsi(value, index);
            }
            if (introducer is ']' or 'P' or '^' or '_')
            {
                return SkipControlString(value, index);
            }

            // A two-character escape sequence, optionally with intermediate
            // bytes.  Consume through its final byte.
            while (index < value.Length && value[index] >= '\x20' && value[index] <= '\x2F')
            {
                index++;
            }
            return index < value.Length ? index + 1 : index;
        }

        private static string SanitizeInput(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder result = new(value.Length);
            for (int index = 0; index < value.Length;)
            {
                char ch = value[index++];
                if (ch == '\x1B')
                {
                    index = SkipEscapeSequence(value, index);
                    continue;
                }
                if (ch == '\x9B')
                {
                    index = SkipCsi(value, index);
                    continue;
                }
                if (ch is '\x90' or '\x9D' or '\x9E' or '\x9F')
                {
                    index = SkipControlString(value, index);
                    continue;
                }

                if (ch == '\r')
                {
                    if (index < value.Length && value[index] == '\n')
                    {
                        index++;
                    }
                    result.Append("  ");
                    continue;
                }
                if (ch is '\n' or '\f' or '\u0085' or '\u2028' or '\u2029')
                {
                    result.Append("  ");
                    continue;
                }
                if (char.IsControl(ch))
                {
                    continue;
                }

                if (char.IsHighSurrogate(ch))
                {
                    if (index < value.Length && char.IsLowSurrogate(value[index]))
                    {
                        result.Append(ch);
                        result.Append(value[index++]);
                    }
                    continue;
                }
                if (char.IsLowSurrogate(ch))
                {
                    continue;
                }

                result.Append(ch);
            }
            return result.ToString();
        }

        private static bool IsPotentialTextUnit(char value)
        {
            return value != '\0' && !char.IsControl(value) &&
                value is not '\u007F' and not '\u0085' and not '\u2028' and not '\u2029';
        }

        private static bool IsAltGrText(ConsoleKeyInfo keyInfo)
        {
            const ConsoleModifiers altGr = ConsoleModifiers.Alt | ConsoleModifiers.Control;
            return (keyInfo.Modifiers & altGr) == altGr &&
                IsPotentialTextUnit(keyInfo.KeyChar);
        }

        private void MoveCursorLeft()
        {
            if (!IsStartOfLine())
            {
                _cursorPos = CharUtil.PreviousTextElementIndex(_text.ToString(), _cursorPos);
            }
        }

        private void MoveCursorHome() => _cursorPos = 0;

        private string BuildKeyInput()
        {
            return _keyInfo.Modifiers == 0
                ? _keyInfo.Key.ToString()
                : _keyInfo.Modifiers + _keyInfo.Key.ToString();
        }

        private void MoveCursorRight()
        {
            if (!IsEndOfLine())
            {
                _cursorPos = CharUtil.NextTextElementIndex(_text.ToString(), _cursorPos);
            }
        }

        private void MoveCursorEnd() => _cursorPos = _cursorLimit;

        private void ClearLine()
        {
            _text.Clear();
            _cursorPos = 0;
            _cursorLimit = 0;
            _pendingHighSurrogate = null;
        }

        private void WriteNewString(string str, int cursor = -1)
        {
            string sanitized = SanitizeInput(str);
            _text = new StringBuilder(sanitized);
            _cursorLimit = sanitized.Length;
            int requestedCursor = cursor < 0 ? _cursorLimit : Math.Clamp(cursor, 0, _cursorLimit);
            _cursorPos = CharUtil.IsTextElementBoundary(sanitized, requestedCursor)
                ? requestedCursor
                : CharUtil.PreviousTextElementIndex(sanitized, requestedCursor);
            _pendingHighSurrogate = null;
        }

        private void WriteString(string str)
        {
            string sanitized = SanitizeInput(str);
            if (sanitized.Length == 0)
            {
                return;
            }

            _text.Insert(_cursorPos, sanitized);
            _cursorPos += sanitized.Length;
            _cursorLimit = _text.Length;
            string currentText = _text.ToString();
            if (!CharUtil.IsTextElementBoundary(currentText, _cursorPos))
            {
                // Inserting a joiner/modifier between existing elements can
                // merge them.  Never leave the cursor inside the merged cluster.
                _cursorPos = CharUtil.NextTextElementIndex(currentText, _cursorPos);
            }
        }

        private void WriteChar() => WriteChar(_keyInfo.KeyChar);

        private void WriteChar(char value)
        {
            if (char.IsHighSurrogate(value))
            {
                _pendingHighSurrogate = value;
                return;
            }

            if (char.IsLowSurrogate(value))
            {
                if (_pendingHighSurrogate is char high)
                {
                    _pendingHighSurrogate = null;
                    WriteString(new string(new[] { high, value }));
                }
                return;
            }

            _pendingHighSurrogate = null;
            if (IsPotentialTextUnit(value))
            {
                WriteString(value.ToString());
            }
        }

        private void Backspace()
        {
            if (IsStartOfLine())
            {
                return;
            }

            string text = _text.ToString();
            int start = CharUtil.PreviousTextElementIndex(text, _cursorPos);
            _text.Remove(start, _cursorPos - start);
            _cursorPos = start;
            _cursorLimit = _text.Length;
            string currentText = _text.ToString();
            if (!CharUtil.IsTextElementBoundary(currentText, _cursorPos))
            {
                _cursorPos = CharUtil.PreviousTextElementIndex(currentText, _cursorPos);
            }
        }

        private void Delete()
        {
            if (IsEndOfLine())
            {
                return;
            }

            string text = _text.ToString();
            int end = CharUtil.NextTextElementIndex(text, _cursorPos);
            _text.Remove(_cursorPos, end - _cursorPos);
            _cursorLimit = _text.Length;
            string currentText = _text.ToString();
            if (!CharUtil.IsTextElementBoundary(currentText, _cursorPos))
            {
                _cursorPos = CharUtil.PreviousTextElementIndex(currentText, _cursorPos);
            }
        }

        private void TransposeChars()
        {
            string text = _text.ToString();
            if (text.Length == 0 || _cursorPos == 0)
            {
                return;
            }

            int rightEnd = _cursorPos == text.Length
                ? text.Length
                : CharUtil.NextTextElementIndex(text, _cursorPos);
            int rightStart = _cursorPos == text.Length
                ? CharUtil.PreviousTextElementIndex(text, text.Length)
                : _cursorPos;
            int leftStart = CharUtil.PreviousTextElementIndex(text, rightStart);
            if (leftStart == rightStart)
            {
                return;
            }

            string left = text.Substring(leftStart, rightStart - leftStart);
            string right = text.Substring(rightStart, rightEnd - rightStart);
            _text.Remove(leftStart, rightEnd - leftStart);
            _text.Insert(leftStart, right + left);
            _cursorPos = rightEnd;
            _cursorLimit = _text.Length;
            string currentText = _text.ToString();
            if (!CharUtil.IsTextElementBoundary(currentText, _cursorPos))
            {
                _cursorPos = CharUtil.NextTextElementIndex(currentText, _cursorPos);
            }
        }

        private static SuggestionResult ValidateAndSanitizeCompletions(
            SuggestionResult result, string text)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (result.StartIndex < 0 || result.StartIndex > text.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(result.StartIndex));
            }

            int endIndex;
            if (result.EndIndex == -1)
            {
                endIndex = text.Length;
            }
            else
            {
                if (result.EndIndex < 0 || result.EndIndex > text.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(result.EndIndex));
                }
                endIndex = result.EndIndex;
            }

            if (endIndex < result.StartIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(result.EndIndex));
            }
            if (!CharUtil.IsTextElementBoundary(text, result.StartIndex) ||
                !CharUtil.IsTextElementBoundary(text, endIndex))
            {
                throw new ArgumentException(
                    "Completion indices must be Unicode text-element boundaries.");
            }

            List<string> suggestions = new();
            if (result.Suggestions != null)
            {
                foreach (string? suggestion in result.Suggestions)
                {
                    if (suggestion != null)
                    {
                        suggestions.Add(SanitizeInput(suggestion));
                    }
                }
            }

            return new SuggestionResult
            {
                StartIndex = result.StartIndex,
                EndIndex = endIndex,
                Suggestions = suggestions,
            };
        }

        private void StartAutoComplete()
        {
            if (!IsInAutoCompleteMode())
            {
                return;
            }

            IList<string> suggestions = _completions!.Suggestions!;
            string suggestion = suggestions[0];
            int replaceLength = _completions.EndIndex - _completions.StartIndex;
            _text.Remove(_completions.StartIndex, replaceLength);
            _text.Insert(_completions.StartIndex, suggestion);
            _completionStart = _completions.StartIndex;
            _completionLength = suggestion.Length;
            _completionsIndex = 0;
            _cursorPos = _completionStart + _completionLength;
            _cursorLimit = _text.Length;
            string currentText = _text.ToString();
            if (!CharUtil.IsTextElementBoundary(currentText, _cursorPos))
            {
                _cursorPos = CharUtil.NextTextElementIndex(currentText, _cursorPos);
            }
        }

        private void ApplyAutoComplete(int index)
        {
            if (!IsInAutoCompleteMode())
            {
                return;
            }

            IList<string> suggestions = _completions!.Suggestions!;
            _text.Remove(_completionStart, _completionLength);
            string suggestion = suggestions[index];
            _text.Insert(_completionStart, suggestion);
            _completionLength = suggestion.Length;
            _completionsIndex = index;
            _cursorPos = _completionStart + _completionLength;
            _cursorLimit = _text.Length;
            string currentText = _text.ToString();
            if (!CharUtil.IsTextElementBoundary(currentText, _cursorPos))
            {
                _cursorPos = CharUtil.NextTextElementIndex(currentText, _cursorPos);
            }
        }

        private void NextAutoComplete()
        {
            if (!IsInAutoCompleteMode())
            {
                return;
            }

            int count = _completions!.Suggestions!.Count;
            ApplyAutoComplete((_completionsIndex + 1) % count);
        }

        private void PreviousAutoComplete()
        {
            if (!IsInAutoCompleteMode())
            {
                return;
            }

            int count = _completions!.Suggestions!.Count;
            ApplyAutoComplete((_completionsIndex + count - 1) % count);
        }

        private void PrevHistory()
        {
            lock (_history)
            {
                _historyIndex = Math.Clamp(_historyIndex, 0, _history.Count);
                if (_historyIndex == _history.Count)
                {
                    _historyDraft = _text.ToString();
                    _historyDraftCursor = _cursorPos;
                }

                if (_historyIndex > 0)
                {
                    _historyIndex--;
                    WriteNewString(_history[_historyIndex]);
                }
            }
        }

        private void NextHistory()
        {
            lock (_history)
            {
                _historyIndex = Math.Clamp(_historyIndex, 0, _history.Count);
                if (_historyIndex >= _history.Count)
                {
                    return;
                }

                _historyIndex++;
                if (_historyIndex == _history.Count)
                {
                    WriteNewString(_historyDraft ?? string.Empty, _historyDraftCursor);
                }
                else
                {
                    WriteNewString(_history[_historyIndex]);
                }
            }
        }

        private void ResetAutoComplete()
        {
            _completions = null;
            _completionStart = 0;
            _completionLength = 0;
            _completionsIndex = 0;
        }

        public string Text => _text.ToString();

        public KeyHandler(IConsole console, List<string>? history,
            IAutoCompleteHandler? autoCompleteHandler, string prompt)
        {
            Console2 = console;
            _prompt = SanitizeInput(prompt);
            _history = history ?? new List<string>();
            lock (_history)
            {
                _historyIndex = _history.Count;
            }
            _historyDraftCursor = 0;
            _text = new StringBuilder();
            _keyActions = new Dictionary<string, Action>();
            _autoCompleteHandler = autoCompleteHandler;

            _renderOriginLeft = Math.Clamp(Console2.CursorLeft, 0, UsableWidth);
            _renderOriginTop = Console2.CursorTop;
            _renderedUsableWidth = UsableWidth;
            WriteRenderedText(_prompt, _renderedUsableWidth);
            Console2.Flush();

            DisplayPosition promptEnd = CalculateDisplayPosition(
                string.Empty, 0, _renderedUsableWidth);
            _renderOriginTop = Math.Max(0, Console2.CursorTop - promptEnd.Line);

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
                if (_cursorPos > 0)
                {
                    _text.Remove(0, _cursorPos);
                    _cursorPos = 0;
                    _cursorLimit = _text.Length;
                }
            };
            _keyActions["ControlK"] = () =>
            {
                if (_cursorPos < _text.Length)
                {
                    _text.Remove(_cursorPos, _text.Length - _cursorPos);
                    _cursorLimit = _text.Length;
                }
            };
            _keyActions["ControlW"] = () =>
            {
                while (!IsStartOfLine())
                {
                    string text = _text.ToString();
                    int previous = CharUtil.PreviousTextElementIndex(text, _cursorPos);
                    string element = text.Substring(previous, _cursorPos - previous);
                    if (string.IsNullOrWhiteSpace(element))
                    {
                        break;
                    }
                    Backspace();
                }
            };
            // ControlT/TransposeChars remains intentionally unbound.
            _keyActions["Tab"] = HandleAutoComplete;
            _keyActions["ShiftTab"] = PreviousAutoComplete;
            _keyActions["ShiftBackspace"] = Backspace;
            _keyActions["ControlV"] = PasteClipboard;
            _keyActions["Shift, ControlV"] = PasteClipboard;
            _keyActions["Alt, ControlV"] = PasteClipboard;
            _keyActions["ControlC"] = () => EOFSent?.Invoke();
        }

        private void HandleAutoComplete()
        {
            if (IsInAutoCompleteMode())
            {
                NextAutoComplete();
                return;
            }
            if (_autoCompleteHandler == null)
            {
                return;
            }

            string oldText = _text.ToString();
            int oldCursor = _cursorPos;
            try
            {
                string text = oldText;
                SuggestionResult result = _autoCompleteHandler.GetSuggestions(text, _cursorPos);
                _completions = ValidateAndSanitizeCompletions(result, text);
                if (_completions.Suggestions is not { Count: > 0 })
                {
                    ResetAutoComplete();
                    return;
                }
                StartAutoComplete();
            }
            catch (Exception ex)
            {
                _text = new StringBuilder(oldText);
                _cursorPos = oldCursor;
                _cursorLimit = oldText.Length;
                ResetAutoComplete();
                TryLogWarning(ex,
                    $"Auto Complete Handler ({_autoCompleteHandler.GetType().FullName}) returned an invalid result or threw an exception.");
            }
        }

        private void PasteClipboard()
        {
            string? clipboardText;
            try
            {
                clipboardText = ClipboardService.GetText();
            }
            catch (Exception ex)
            {
                TryLogWarning(ex, "Reading text from the clipboard failed.");
                return;
            }

            if (!string.IsNullOrEmpty(clipboardText))
            {
                // One sanitized insertion and one redraw, independent of the
                // clipboard length.  Mutation errors are handled transactionally
                // by Handle rather than being mistaken for clipboard failures.
                WriteString(clipboardText);
            }
        }

        private void RestoreState(string text, int cursor, int historyIndex,
            string? historyDraft, int historyDraftCursor, char? pendingHighSurrogate)
        {
            _text = new StringBuilder(text);
            _cursorLimit = text.Length;
            _cursorPos = cursor;
            _historyIndex = historyIndex;
            _historyDraft = historyDraft;
            _historyDraftCursor = historyDraftCursor;
            _pendingHighSurrogate = pendingHighSurrogate;
            ResetAutoComplete();
        }

        public void Handle(ConsoleKeyInfo keyInfo)
        {
            _keyInfo = keyInfo;
            string keyInput = BuildKeyInput();

            Action? action = null;
            // Printable Ctrl+Alt input is AltGr text, not a shortcut.  A real
            // Ctrl+Alt+V still reaches the paste action because its KeyChar is
            // a control/NUL value on supported consoles.
            if (!IsAltGrText(keyInfo))
            {
                _keyActions.TryGetValue(keyInput, out action);
            }

            bool writeCharacter = action == null && !BlockKey(keyInfo);
            if (action == null && !writeCharacter)
            {
                _pendingHighSurrogate = null;
                return;
            }

            bool completionKey = keyInput is "Tab" or "ShiftTab";
            if (IsInAutoCompleteMode() && !completionKey)
            {
                ResetAutoComplete();
            }

            string oldText = _text.ToString();
            int oldCursor = _cursorPos;
            int oldHistoryIndex = _historyIndex;
            string? oldHistoryDraft = _historyDraft;
            int oldHistoryDraftCursor = _historyDraftCursor;
            char? oldPendingHighSurrogate = _pendingHighSurrogate;
            int oldConsoleLeft = Console2.CursorLeft;
            int oldConsoleTop = Console2.CursorTop;

            try
            {
                if (!writeCharacter)
                {
                    _pendingHighSurrogate = null;
                }
                (action ?? WriteChar).Invoke();

                _cursorLimit = _text.Length;
                string currentText = _text.ToString();
                if (_cursorPos < 0 || _cursorPos > _cursorLimit ||
                    !CharUtil.IsTextElementBoundary(currentText, _cursorPos))
                {
                    throw new InvalidOperationException(
                        "A key action produced an invalid text cursor state.");
                }
            }
            catch (Exception ex)
            {
                RestoreState(oldText, oldCursor, oldHistoryIndex, oldHistoryDraft,
                    oldHistoryDraftCursor, oldPendingHighSurrogate);
                TryLogWarning(ex, $"The key action for {keyInput} failed and was rolled back.");

                if (Console2.CursorLeft != oldConsoleLeft || Console2.CursorTop != oldConsoleTop)
                {
                    EmergencyRedraw();
                }
                return;
            }

            string newText = _text.ToString();
            bool renderWidthChanged = UsableWidth != _renderedUsableWidth;
            if (oldText == newText && oldCursor == _cursorPos && !renderWidthChanged)
            {
                try
                {
                    Console2.Flush();
                }
                catch (Exception ex)
                {
                    TryLogWarning(ex, "Flushing the console after a key action failed.");
                    EmergencyRedraw();
                }
                return;
            }

            try
            {
                if (renderWidthChanged)
                {
                    RedrawInput(oldText, oldCursor, oldConsoleTop);
                }
                else if (oldText == newText)
                {
                    int originTop = GetOriginTop(
                        oldText, oldCursor, oldConsoleTop, _renderedUsableWidth);
                    SetCursorForText(
                        newText, _cursorPos, originTop, _renderedUsableWidth);
                }
                else if (oldCursor == oldText.Length &&
                    _cursorPos == newText.Length &&
                    newText.StartsWith(oldText, StringComparison.Ordinal) &&
                    CharUtil.IsTextElementBoundary(newText, oldText.Length))
                {
                    AppendRenderedInput(oldText, oldCursor, newText, oldConsoleTop);
                }
                else
                {
                    RedrawInput(oldText, oldCursor, oldConsoleTop);
                }
            }
            catch (Exception ex)
            {
                ResetAutoComplete();
                TryLogWarning(ex, "Redrawing the console input area failed; rebuilding it.");
                EmergencyRedraw();
            }
        }

        /// <summary>
        /// Clears the current input area while retaining this instance's state.
        /// </summary>
        internal void ClearWrittingStatus()
        {
            string text = _text.ToString();
            int originTop = UsableWidth != _renderedUsableWidth
                ? _renderOriginTop
                : GetOriginTop(text, _cursorPos, Console2.CursorTop,
                    _renderedUsableWidth);
            ClearRenderedRows(text, originTop, _renderedUsableWidth);
            Console2.SetCursorPosition(0, originTop);
            Console2.Flush();
        }

        /// <summary>
        /// Moves the physical cursor past the complete input before the caller
        /// writes the terminating newline. Submission must not happen at an
        /// arbitrary editing cursor (for example after Home).
        /// </summary>
        internal void MoveCursorToEndForSubmit()
        {
            string text = _text.ToString();
            int oldCursor = _cursorPos;
            int oldConsoleTop = Console2.CursorTop;
            _cursorPos = _cursorLimit = text.Length;

            try
            {
                if (UsableWidth != _renderedUsableWidth)
                {
                    RedrawInput(text, oldCursor, oldConsoleTop);
                }
                else
                {
                    int originTop = GetOriginTop(
                        text, oldCursor, oldConsoleTop, _renderedUsableWidth);
                    SetCursorForText(
                        text, _cursorPos, originTop, _renderedUsableWidth);
                }
            }
            catch (Exception ex)
            {
                TryLogWarning(ex, "Moving the console cursor for input submission failed.");
                EmergencyRedraw();
            }
        }

        /// <summary>
        /// Erases and forgets an abandoned interactive read so that its draft,
        /// completion state and pending surrogate cannot leak into the next one.
        /// </summary>
        internal void CancelInput()
        {
            try
            {
                ClearWrittingStatus();
            }
            finally
            {
                _text.Clear();
                _cursorPos = 0;
                _cursorLimit = 0;
                lock (_history)
                {
                    _historyIndex = _history.Count;
                }
                _historyDraft = null;
                _historyDraftCursor = 0;
                _pendingHighSurrogate = null;
                ResetAutoComplete();
                _renderOriginLeft = Math.Clamp(Console2.CursorLeft, 0, UsableWidth);
                _renderOriginTop = Console2.CursorTop;
                _renderedUsableWidth = UsableWidth;
            }
        }

        internal static KeyHandler RecoverWrittingStatus(string prompt,
            KeyHandler previous_stat, IAutoCompleteHandler? autoCompleteHandler)
        {
            KeyHandler keyHandler = new(previous_stat.Console2,
                previous_stat._history, autoCompleteHandler, prompt);

            string restoredText = SanitizeInput(previous_stat.Text);
            keyHandler._text = new StringBuilder(restoredText);
            keyHandler._cursorLimit = restoredText.Length;
            keyHandler._cursorPos = Math.Clamp(previous_stat._cursorPos, 0, restoredText.Length);
            if (!CharUtil.IsTextElementBoundary(restoredText, keyHandler._cursorPos))
            {
                keyHandler._cursorPos = CharUtil.PreviousTextElementIndex(
                    restoredText, keyHandler._cursorPos);
            }

            keyHandler._historyIndex = Math.Clamp(previous_stat._historyIndex,
                0, previous_stat._history.Count);
            keyHandler._historyDraft = previous_stat._historyDraft == null
                ? null
                : SanitizeInput(previous_stat._historyDraft);
            keyHandler._historyDraftCursor = Math.Clamp(
                previous_stat._historyDraftCursor, 0,
                keyHandler._historyDraft?.Length ?? 0);
            keyHandler._pendingHighSurrogate = previous_stat._pendingHighSurrogate;

            if (autoCompleteHandler != null &&
                ReferenceEquals(previous_stat._autoCompleteHandler, autoCompleteHandler) &&
                previous_stat.IsInAutoCompleteMode())
            {
                keyHandler._completions = previous_stat._completions;
                keyHandler._completionsIndex = previous_stat._completionsIndex;
                keyHandler._completionStart = previous_stat._completionStart;
                keyHandler._completionLength = previous_stat._completionLength;
            }

            int usableWidth = keyHandler.UsableWidth;
            keyHandler._renderedUsableWidth = usableWidth;
            keyHandler.WriteRenderedText(restoredText, usableWidth);
            DisplayPosition end = keyHandler.CalculateDisplayPosition(
                restoredText, restoredText.Length, usableWidth);
            int originTop = keyHandler.Console2.CursorTop - end.Line;
            if (originTop < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(originTop));
            }
            keyHandler.SetCursorForText(
                restoredText, keyHandler._cursorPos, originTop, usableWidth);

            Debug.Assert(keyHandler._cursorPos >= 0 &&
                keyHandler._cursorPos <= keyHandler._cursorLimit);
            Debug.Assert(keyHandler._cursorLimit == keyHandler._text.Length);
            return keyHandler;
        }
    }
}
