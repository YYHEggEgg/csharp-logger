using Cyjb;
using Internal.ReadLine.Abstractions;
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
        private string _prompt;
        private IAutoCompleteHandler? _autoCompleteHandler;
        private char? _pendingHighSurrogate;
        private int _renderOriginLeft;
        private int _renderOriginTop;
        private int _renderedUsableWidth;
        // Only this rectangle is still addressable after a terminal has
        // scrolled a long input line.  Do not derive it from the complete
        // logical input: ConPTY commonly exposes a buffer no taller than its
        // window, so older rows cease to have usable coordinates.
        private int _renderVisibleTop;
        private int _renderVisibleRows;
        private int _renderVisibleFirstColumn;
        private bool _renderStateValid;
        private bool _renderWarningActive;

        /// <summary>
        /// Raised when the user presses Control+C (^C).
        /// </summary>
        public event Action? EOFSent;

        private readonly struct RenderedRow
        {
            public RenderedRow(int start, int end)
            {
                Start = start;
                End = end;
            }

            public int Start { get; }
            public int End { get; }
        }

        private readonly struct ViewportContent
        {
            public ViewportContent(string display, int cursorIndex, int startColumn)
            {
                Display = display;
                CursorIndex = cursorIndex;
                StartColumn = startColumn;
            }

            public string Display { get; }
            public int CursorIndex { get; }
            public int StartColumn { get; }
        }

        // Console.BufferHeight can describe a large scrollback buffer rather
        // than the visible window. Keep recovery independent of that value so
        // a log write can never replay an arbitrarily long command.
        private const int MaximumViewportRows = 64;
        private const int MaximumViewportCharacters = 16 * 1024;
        private const int ViewportBoundaryContext = 256;

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

        private List<RenderedRow> BuildRenderedRows(string display, int usableWidth,
            int initialColumn)
        {
            List<RenderedRow> rows = new();
            int column = Math.Clamp(initialColumn, 0, usableWidth);
            int rowStart = 0;
            int[] starts = StringInfo.ParseCombiningCharacters(display);
            for (int i = 0; i < starts.Length; i++)
            {
                int start = starts[i];
                int end = i + 1 < starts.Length ? starts[i + 1] : display.Length;
                int elementWidth = Math.Min(
                    CharUtil.TextElementWidth(display, start, end - start), usableWidth);
                if (elementWidth > 0 && column + elementWidth > usableWidth)
                {
                    rows.Add(new RenderedRow(rowStart, start));
                    rowStart = start;
                    column = 0;
                }

                column += elementWidth;
                if (elementWidth > 0 && column >= usableWidth)
                {
                    rows.Add(new RenderedRow(rowStart, end));
                    rowStart = end;
                    column = 0;
                }
            }
            rows.Add(new RenderedRow(rowStart, display.Length));
            return rows;
        }

        private static int GetRowForIndex(IReadOnlyList<RenderedRow> rows, int index)
        {
            for (int row = 0; row < rows.Count; row++)
            {
                RenderedRow current = rows[row];
                if (index < current.End || row == rows.Count - 1)
                {
                    return row;
                }
                if (index == current.End &&
                    (row + 1 >= rows.Count || rows[row + 1].Start != index))
                {
                    return row;
                }
            }
            return rows.Count - 1;
        }

        private static int GetColumnInRow(string display, RenderedRow row, int index,
            int rowIndex, int usableWidth, int initialColumn)
        {
            int length = Math.Clamp(index - row.Start, 0, row.End - row.Start);
            int line = 0;
            int column = rowIndex == 0
                ? Math.Clamp(initialColumn, 0, usableWidth)
                : 0;
            if (length > 0)
            {
                string part = display.Substring(row.Start, length);
                AdvanceText(ref line, ref column, part, part.Length, usableWidth);
            }
            return Math.Clamp(column, 0, usableWidth);
        }

        private int GetViewportRowLimit()
        {
            return Math.Min(Math.Max(1, Console2.BufferHeight), MaximumViewportRows);
        }

        private int FindViewportStart(int desiredStart)
        {
            if (desiredStart <= 0)
            {
                return 0;
            }

            int textLength = _text.Length;
            if (desiredStart >= textLength)
            {
                return textLength;
            }

            int sampleStart = Math.Max(0, desiredStart - ViewportBoundaryContext);
            int sampleEnd = Math.Min(textLength,
                desiredStart + ViewportBoundaryContext);
            string sample = _text.ToString(sampleStart, sampleEnd - sampleStart);
            int target = desiredStart - sampleStart;
            foreach (int boundary in StringInfo.ParseCombiningCharacters(sample))
            {
                if (boundary >= target)
                {
                    return sampleStart + boundary;
                }
            }

            // A pathological single text element can be longer than the
            // context window. Do not let it turn recovery into a full-line
            // scan; begin after the inspected slice instead.
            return sampleEnd;
        }

        private int FindViewportEnd(int contentStart, int desiredEnd)
        {
            int textLength = _text.Length;
            if (desiredEnd >= textLength)
            {
                return textLength;
            }

            desiredEnd = Math.Clamp(desiredEnd, contentStart, textLength);
            int sampleEnd = Math.Min(textLength,
                desiredEnd + ViewportBoundaryContext);
            string sample = _text.ToString(contentStart, sampleEnd - contentStart);
            int result = contentStart;
            foreach (int boundary in StringInfo.ParseCombiningCharacters(sample))
            {
                int absoluteBoundary = contentStart + boundary;
                if (absoluteBoundary > desiredEnd)
                {
                    break;
                }
                result = absoluteBoundary;
            }
            return result;
        }

        private ViewportContent BuildViewportContent(int requestedCursor,
            int usableWidth, int rowLimit)
        {
            int textLength = _text.Length;
            requestedCursor = Math.Clamp(requestedCursor, 0, textLength);
            int maxContentLength = (int)Math.Clamp(
                (long)usableWidth * rowLimit * 2, 256, MaximumViewportCharacters);

            int contentStart = 0;
            int contentEnd = textLength;
            if (textLength > maxContentLength)
            {
                int beforeCursor = maxContentLength / 2;
                int afterCursor = maxContentLength - beforeCursor;
                int desiredStart = Math.Max(0, requestedCursor - beforeCursor);
                int desiredEnd = Math.Min(textLength, requestedCursor + afterCursor);
                if (desiredStart == 0)
                {
                    desiredEnd = Math.Min(textLength, maxContentLength);
                }
                else if (desiredEnd == textLength)
                {
                    desiredStart = Math.Max(0, textLength - maxContentLength);
                }

                contentStart = FindViewportStart(desiredStart);
                if (contentStart > requestedCursor)
                {
                    contentStart = requestedCursor;
                }
                contentEnd = FindViewportEnd(contentStart, desiredEnd);
                if (contentEnd < requestedCursor)
                {
                    contentEnd = requestedCursor;
                }
            }

            bool omittedLeadingText = contentStart > 0;
            bool omittedTrailingText = contentEnd < textLength;
            string prefix = omittedLeadingText ? "..." : _prompt;
            string display = prefix + _text.ToString(contentStart,
                contentEnd - contentStart);
            if (omittedTrailingText)
            {
                display += "...";
            }

            return new ViewportContent(display,
                prefix.Length + requestedCursor - contentStart,
                omittedLeadingText ? 0 : Math.Clamp(_renderOriginLeft, 0, usableWidth));
        }

        private void ClearRenderedRows()
        {
            if (!_renderStateValid || _renderVisibleRows <= 0)
            {
                return;
            }

            int usableWidth = UsableWidth;
            int height = Math.Max(1, Console2.BufferHeight);
            int top = Math.Clamp(_renderVisibleTop, 0, height - 1);
            int rowCount = Math.Min(_renderVisibleRows,
                Math.Min(height - top, MaximumViewportRows));
            for (int row = 0; row < rowCount; row++)
            {
                int startColumn = row == 0 ? _renderVisibleFirstColumn : 0;
                startColumn = Math.Clamp(startColumn, 0, usableWidth);
                Console2.SetCursorPosition(startColumn, top + row);
                int clearLength = usableWidth - startColumn;
                if (clearLength > 0)
                {
                    Console2.Write(new string(' ', clearLength));
                }
            }

            _renderStateValid = false;
            _renderVisibleRows = 0;
            Console2.SetCursorPosition(0, top);
            Console2.Flush();
        }

        /// <summary>
        /// Rebuilds a bounded window around the editing cursor. The logical
        /// line may be arbitrarily long, but recovery only parses and writes a
        /// small tail/viewport that remains practical after terminal scrolling.
        /// </summary>
        private void RenderViewport(int requestedCursor)
        {
            int usableWidth = UsableWidth;
            int height = Math.Max(1, Console2.BufferHeight);
            int rowLimit = GetViewportRowLimit();
            ViewportContent viewport = BuildViewportContent(requestedCursor,
                usableWidth, rowLimit);
            List<RenderedRow> rows = BuildRenderedRows(viewport.Display,
                usableWidth, viewport.StartColumn);
            int cursorRow = GetRowForIndex(rows, viewport.CursorIndex);
            int firstRow = Math.Clamp(cursorRow - rowLimit / 2, 0,
                Math.Max(0, rows.Count - rowLimit));
            int lastRow = Math.Min(rows.Count - 1, firstRow + rowLimit - 1);

            int startTop = _renderStateValid
                ? _renderVisibleTop
                : Console2.CursorTop;
            startTop = Math.Clamp(startTop, 0, height - 1);
            ClearRenderedRows();

            int startColumn = firstRow == 0 ? viewport.StartColumn : 0;
            Console2.SetCursorPosition(startColumn, startTop);
            Console2.Flush();

            for (int row = firstRow; row <= lastRow; row++)
            {
                RenderedRow range = rows[row];
                if (range.End > range.Start)
                {
                    Console2.Write(viewport.Display.Substring(range.Start,
                        range.End - range.Start));
                }
                if (row < lastRow)
                {
                    Console2.WriteLine(string.Empty);
                }
            }

            int visibleRows = lastRow - firstRow + 1;
            int expectedEndTop = startTop + visibleRows - 1;
            int observedEndTop = Console2.CursorTop;
            int scrollRows = Math.Max(0, expectedEndTop - observedEndTop);
            int baseTop = Math.Max(0, startTop - scrollRows);
            int cursorVisibleRow = cursorRow - firstRow;
            int cursorTop = Math.Clamp(baseTop + cursorVisibleRow, 0, height - 1);
            int cursorColumn = GetColumnInRow(viewport.Display, rows[cursorRow],
                viewport.CursorIndex, cursorRow, usableWidth, viewport.StartColumn);
            Console2.SetCursorPosition(cursorColumn, cursorTop);
            Console2.Flush();

            _renderOriginTop = baseTop;
            _renderedUsableWidth = usableWidth;
            _renderVisibleTop = baseTop;
            _renderVisibleRows = Math.Min(visibleRows, height - baseTop);
            _renderVisibleFirstColumn = scrollRows == 0 && firstRow == 0
                ? startColumn
                : 0;
            _renderStateValid = true;
            _renderWarningActive = false;
        }

        private void RedrawInput()
        {
            RenderViewport(_cursorPos);
        }

        private void AppendRenderedInput(string appendedText)
        {
            if (appendedText.Length == 0)
            {
                return;
            }
            if (!_renderStateValid || UsableWidth != _renderedUsableWidth)
            {
                RenderViewport(_cursorPos);
                return;
            }

            int usableWidth = UsableWidth;
            int previousLeft = Math.Clamp(Console2.CursorLeft, 0, usableWidth);
            int previousTop = Console2.CursorTop;
            int rowAdvance = 0;
            int column = previousLeft;
            AdvanceText(ref rowAdvance, ref column, appendedText,
                appendedText.Length, usableWidth);
            WriteRenderedText(appendedText, usableWidth);

            // The terminal is authoritative about scroll position. Keeping a
            // bounded tail is enough for clearing/recovery; never attempt to
            // seek back to the logical prompt after it has scrolled away.
            int height = Math.Max(1, Console2.BufferHeight);
            int rowLimit = GetViewportRowLimit();
            int previousRows = Math.Max(1, _renderVisibleRows);
            int visibleRows = Math.Min(rowLimit, previousRows + rowAdvance);
            int expectedEndTop = previousTop + rowAdvance;
            int scrollRows = Math.Max(0, expectedEndTop - Console2.CursorTop);
            _renderVisibleRows = Math.Min(visibleRows, height);
            _renderVisibleTop = Math.Max(0, Console2.CursorTop - _renderVisibleRows + 1);
            if (scrollRows > 0 || previousRows + rowAdvance > rowLimit)
            {
                _renderVisibleFirstColumn = 0;
            }
            _renderedUsableWidth = usableWidth;
            _renderWarningActive = false;
        }

        private void EmergencyRedraw()
        {
            try
            {
                Console2.Resync();
                RenderViewport(_cursorPos);
            }
            catch
            {
                // There is no safe cursor operation when even a resync fails
                // (for example after the terminal has been closed).
                _renderStateValid = false;
            }
        }

        private void TryLogRenderWarning(Exception ex, string prompt)
        {
            if (_renderWarningActive)
            {
                return;
            }
            _renderWarningActive = true;
            TryLogWarning(ex, prompt);
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

        private void InsertSanitizedText(string sanitized)
        {
            if (sanitized.Length == 0)
            {
                return;
            }

            if (_cursorPos == _text.Length)
            {
                _text.Append(sanitized);
            }
            else
            {
                _text.Insert(_cursorPos, sanitized);
            }
            _cursorPos += sanitized.Length;
            _cursorLimit = _text.Length;
            // Appending leaves the cursor at the end of the text, which is
            // always a text-element boundary. Avoid copying the complete line
            // for every native-paste batch just to establish that fact.
            if (_cursorPos != _cursorLimit)
            {
                string currentText = _text.ToString();
                if (!CharUtil.IsTextElementBoundary(currentText, _cursorPos))
                {
                    // Inserting a joiner/modifier between existing elements can
                    // merge them. Never leave the cursor inside the merged cluster.
                    _cursorPos = CharUtil.NextTextElementIndex(currentText, _cursorPos);
                }
            }
        }

        private void WriteString(string str) => InsertSanitizedText(SanitizeInput(str));

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

            RenderViewport(0);
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

        private void AppendKeyCharacter(StringBuilder pendingText, char value)
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
                    pendingText.Append(high);
                    pendingText.Append(value);
                }
                _pendingHighSurrogate = null;
                return;
            }

            _pendingHighSurrogate = null;
            if (IsPotentialTextUnit(value))
            {
                pendingText.Append(value);
            }
        }

        private bool FlushPendingText(StringBuilder pendingText)
        {
            if (pendingText.Length == 0)
            {
                return false;
            }

            string sanitized = SanitizeInput(pendingText.ToString());
            pendingText.Clear();
            if (sanitized.Length == 0)
            {
                return false;
            }
            InsertSanitizedText(sanitized);
            return true;
        }

        private void ValidateCursorState(string text)
        {
            _cursorLimit = _text.Length;
            if (_cursorPos < 0 || _cursorPos > _cursorLimit ||
                !CharUtil.IsTextElementBoundary(text, _cursorPos))
            {
                throw new InvalidOperationException(
                    "A key action produced an invalid text cursor state.");
            }
        }

        private bool TryHandleAppendOnlyTextBatch(IReadOnlyList<ConsoleKeyInfo> keyInfos)
        {
            if (_cursorPos != _cursorLimit || IsInAutoCompleteMode())
            {
                return false;
            }

            foreach (ConsoleKeyInfo keyInfo in keyInfos)
            {
                if (!IsPotentialTextUnit(keyInfo.KeyChar))
                {
                    return false;
                }

                // Keep the general path for registered shortcuts while
                // leaving normal Shift/Alt/layout text on this allocation-light
                // native-paste route. AltGr remains text by definition.
                if (!IsAltGrText(keyInfo) && keyInfo.Modifiers != 0)
                {
                    _keyInfo = keyInfo;
                    if (_keyActions.ContainsKey(BuildKeyInput()))
                    {
                        return false;
                    }
                }
            }

            StringBuilder pendingText = new(keyInfos.Count);
            foreach (ConsoleKeyInfo keyInfo in keyInfos)
            {
                AppendKeyCharacter(pendingText, keyInfo.KeyChar);
            }

            if (pendingText.Length == 0)
            {
                try
                {
                    Console2.Flush();
                }
                catch (Exception ex)
                {
                    TryLogRenderWarning(ex,
                        "Flushing the console after native terminal input failed.");
                    EmergencyRedraw();
                }
                return true;
            }

            string sanitized = SanitizeInput(pendingText.ToString());
            if (sanitized.Length == 0)
            {
                return true;
            }

            InsertSanitizedText(sanitized);
            try
            {
                AppendRenderedInput(sanitized);
            }
            catch (Exception ex)
            {
                TryLogRenderWarning(ex,
                    "Rendering native terminal input failed; rebuilding its visible input window.");
                EmergencyRedraw();
            }
            return true;
        }

        /// <summary>
        /// Handles queued terminal input as one transaction. Consecutive
        /// printable keys are inserted and rendered together; navigation and
        /// editor commands preserve their original ordering as batch bounds.
        /// </summary>
        internal void HandleBatch(IReadOnlyList<ConsoleKeyInfo> keyInfos)
        {
            ArgumentNullException.ThrowIfNull(keyInfos);
            if (keyInfos.Count == 0)
            {
                return;
            }

            if (TryHandleAppendOnlyTextBatch(keyInfos))
            {
                return;
            }

            string oldText = _text.ToString();
            int oldCursor = _cursorPos;
            int oldHistoryIndex = _historyIndex;
            string? oldHistoryDraft = _historyDraft;
            int oldHistoryDraftCursor = _historyDraftCursor;
            char? oldPendingHighSurrogate = _pendingHighSurrogate;
            int oldConsoleLeft = Console2.CursorLeft;
            int oldConsoleTop = Console2.CursorTop;
            bool appendOnly = oldCursor == oldText.Length;
            bool changed = false;
            StringBuilder pendingText = new();
            string? failedKey = null;

            try
            {
                foreach (ConsoleKeyInfo keyInfo in keyInfos)
                {
                    _keyInfo = keyInfo;
                    string keyInput = BuildKeyInput();
                    failedKey = keyInput;
                    Action? action = null;
                    // Printable Ctrl+Alt input is AltGr text, not a shortcut.
                    if (!IsAltGrText(keyInfo))
                    {
                        _keyActions.TryGetValue(keyInput, out action);
                    }

                    bool writeCharacter = action == null && !BlockKey(keyInfo);
                    if (action == null && !writeCharacter)
                    {
                        if (FlushPendingText(pendingText))
                        {
                            changed = true;
                        }
                        _pendingHighSurrogate = null;
                        continue;
                    }

                    bool completionKey = keyInput is "Tab" or "ShiftTab";
                    if (IsInAutoCompleteMode() && !completionKey)
                    {
                        if (FlushPendingText(pendingText))
                        {
                            changed = true;
                        }
                        ResetAutoComplete();
                        appendOnly = false;
                    }

                    if (writeCharacter)
                    {
                        if (_cursorPos != _text.Length)
                        {
                            appendOnly = false;
                        }
                        AppendKeyCharacter(pendingText, keyInfo.KeyChar);
                        continue;
                    }

                    if (FlushPendingText(pendingText))
                    {
                        changed = true;
                    }
                    _pendingHighSurrogate = null;
                    // Any command can replace text with an equally long value
                    // (history and completion are common examples). Treat it
                    // as a render boundary instead of relying on length alone.
                    appendOnly = false;
                    action!.Invoke();
                }

                if (FlushPendingText(pendingText))
                {
                    changed = true;
                }

                string newTextForValidation = _text.ToString();
                ValidateCursorState(newTextForValidation);
            }
            catch (Exception ex)
            {
                RestoreState(oldText, oldCursor, oldHistoryIndex, oldHistoryDraft,
                    oldHistoryDraftCursor, oldPendingHighSurrogate);
                string keyDescription = failedKey ?? _keyInfo.Key.ToString();
                TryLogWarning(ex, $"The key action for {keyDescription} failed and was rolled back.");

                if (Console2.CursorLeft != oldConsoleLeft || Console2.CursorTop != oldConsoleTop)
                {
                    EmergencyRedraw();
                }
                return;
            }

            string newText = _text.ToString();
            if (!changed)
            {
                changed = oldCursor != _cursorPos || !string.Equals(oldText, newText,
                    StringComparison.Ordinal);
            }
            bool renderWidthChanged = UsableWidth != _renderedUsableWidth;
            if (!changed && oldText == newText && oldCursor == _cursorPos && !renderWidthChanged)
            {
                try
                {
                    Console2.Flush();
                }
                catch (Exception ex)
                {
                    TryLogRenderWarning(ex, "Flushing the console after a key action failed.");
                    EmergencyRedraw();
                }
                return;
            }

            try
            {
                if (!renderWidthChanged && appendOnly && _cursorPos == newText.Length &&
                    newText.Length >= oldText.Length)
                {
                    AppendRenderedInput(newText.Substring(oldText.Length));
                }
                else
                {
                    RedrawInput();
                }
            }
            catch (Exception ex)
            {
                ResetAutoComplete();
                TryLogRenderWarning(ex,
                    "Redrawing the console input area failed; rebuilding its visible input window.");
                EmergencyRedraw();
            }
        }

        public void Handle(ConsoleKeyInfo keyInfo) => HandleBatch(new[] { keyInfo });

        /// <summary>
        /// Clears the current input area while retaining this instance's state.
        /// </summary>
        internal void ClearWrittingStatus()
        {
            int top = _renderStateValid ? _renderVisibleTop : Console2.CursorTop;
            ClearRenderedRows();
            Console2.SetCursorPosition(0, Math.Clamp(top, 0,
                Math.Max(1, Console2.BufferHeight) - 1));
            Console2.Flush();
        }

        /// <summary>
        /// Moves the physical cursor past the complete input before the caller
        /// writes the terminating newline. Submission must not happen at an
        /// arbitrary editing cursor (for example after Home).
        /// </summary>
        internal void MoveCursorToEndForSubmit()
        {
            bool alreadyAtEnd = _cursorPos == _cursorLimit && _renderStateValid &&
                UsableWidth == _renderedUsableWidth;
            _cursorPos = _cursorLimit = _text.Length;

            try
            {
                if (alreadyAtEnd)
                {
                    Console2.Flush();
                }
                else
                {
                    RenderViewport(_cursorPos);
                }
            }
            catch (Exception ex)
            {
                TryLogRenderWarning(ex, "Moving the console cursor for input submission failed.");
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
                _renderStateValid = false;
                _renderVisibleRows = 0;
            }
        }

        /// <summary>
        /// Restores this handler after console output occupied its visible
        /// rectangle. Keeping the same instance also applies a changed input
        /// configuration without copying the complete logical command.
        /// </summary>
        internal void RecoverWrittingStatus(string prompt,
            IAutoCompleteHandler? autoCompleteHandler)
        {
            bool autoCompleteHandlerChanged = !ReferenceEquals(
                _autoCompleteHandler, autoCompleteHandler);
            _prompt = SanitizeInput(prompt);
            _autoCompleteHandler = autoCompleteHandler;
            if (autoCompleteHandlerChanged)
            {
                ResetAutoComplete();
            }
            RenderViewport(_cursorPos);
        }
    }
}
