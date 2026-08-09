using Internal.ReadLine;
using Internal.ReadLine.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;
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
    public static class ConsoleWrapper
    {
        private const int MaxWriteBatchSize = 128;
        private const int MaxReadKeyBatchSize = 4096;
        private static readonly long InputOutputFairnessStopwatchTicks =
            Math.Max(1, Stopwatch.Frequency / 20);
        private static readonly object InitializationLock = new();
        private static readonly object InputStateLock = new();
        private static readonly object WriteQueueLock = new();
        private static readonly object KeyHandlerLock = new();
        private static readonly object RedirectedInputLock = new();
        private static readonly SemaphoreSlim ReadLineLock = new(1, 1);
        private static readonly SemaphoreSlim UpdateSignal = new(0, 1);
        private static List<string> lines = null!; // 记录每行输入的列表
        private static readonly ConcurrentQueue<CompletedReadLine> readqueue = new();
        private static readonly ConcurrentQueue<int> PendingInputCancellations = new();
        public static event ConsoleWrapperCancelKeyPressEventHandler? ShutDownRequest; // 退出事件

        /// <summary>
        /// The refresh time for <see cref="ReadLine"/> waiting input.
        /// <para/>It should refer to milliseconds not ticks, but it won't change since it has been published.
        /// </summary>
        public static int RefreshTicks
        {
            get => Volatile.Read(ref _refreshTicks);
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "RefreshTicks must be greater than zero.");
                Volatile.Write(ref _refreshTicks, value);
            }
        }
        private static int _refreshTicks = 2;
        private static int _nextReadSessionId;
        private static int _activeReadSessionId;
        private static volatile bool _initialized;
        private static bool _interactiveConsole;
        private static CancellationTokenSource? _backgroundCancellation;
        private static Task? _backgroundReadKeyTask;
        private static Task? _backgroundUpdateTask;
        private static Task<string?>? _pendingRedirectedRead;

        internal static bool IsInitialized => _initialized;

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

            lock (InitializationLock)
            {
                if (_initialized) return;
                if (!Tools.CheckIfSupportedOS())
                    throw new InvalidOperationException(
                        "ConsoleWrapper and any features related " +
                        "to console is not supported on current OS.");

                // Register the logger cleanup handler before starting the
                // wrapper. It must drain log producers before this consumer.
                BaseLogger.EnsureCleanupRegistered();

                bool subscribedCancelKeyPress = false;
                CancellationTokenSource? cancellation = null;
                Task? updateTask = null;
                Task? readKeyTask = null;
                try
                {
                    bool interactiveConsole = IsInteractiveConsole();
                    var absConsole = new DelayConsole(interactiveConsole);
                    lines ??= new List<string>();
                    var inputState = GetInputStateSnapshot();
                    KeyHandler? initialKeyHandler = interactiveConsole
                        ? new(absConsole, lines, inputState.handler, string.Empty)
                        : null;

                    if (interactiveConsole)
                        Console.TreatControlCAsInput = false;
                    Console.CancelKeyPress += Console_CancelKeyPress;
                    subscribedCancelKeyPress = true;

                    cancellation = new CancellationTokenSource();
                    shared_absconsole = absConsole;
                    keyHandler = initialKeyHandler;
                    _interactiveConsole = interactiveConsole;
                    _backgroundCancellation = cancellation;
                    lock (WriteQueueLock)
                    {
                        _shutdownRequested = 0;
                    }
                    _clearup_completed = false;
                    updateTask = Task.Run(BackgroundUpdate);
                    readKeyTask = interactiveConsole
                        ? Task.Run(() => BackgroundReadkey(cancellation.Token))
                        : Task.CompletedTask;
                    _backgroundUpdateTask = updateTask;
                    _backgroundReadKeyTask = readKeyTask;

                    // Publish initialization only after every shared reference
                    // and task handle is installed.
                    _initialized = true;
                }
                catch
                {
                    lock (WriteQueueLock)
                    {
                        _shutdownRequested = 1;
                    }
                    cancellation?.Cancel();
                    SignalUpdate();
                    var startedTasks = new[] { updateTask, readKeyTask }
                        .Where(task => task != null).Cast<Task>().ToArray();
                    if (startedTasks.Length > 0)
                    {
                        try
                        {
                            Task.WaitAll(startedTasks, TimeSpan.FromMilliseconds(250));
                        }
                        catch (AggregateException ex)
                        {
                            ReportBackgroundException(ex.Flatten(),
                                "Partially initialized console tasks failed during rollback.");
                        }
                    }
                    if (subscribedCancelKeyPress)
                        Console.CancelKeyPress -= Console_CancelKeyPress;
                    _backgroundCancellation?.Dispose();
                    _backgroundCancellation = null;
                    _backgroundReadKeyTask = null;
                    _backgroundUpdateTask = null;
                    shared_absconsole = null;
                    keyHandler = null;
                    _interactiveConsole = false;
                    _initialized = false;
                    throw;
                }
            }
        }

        private static bool IsInteractiveConsole()
        {
            try
            {
                return !Console.IsInputRedirected && !Console.IsOutputRedirected;
            }
            catch (Exception ex) when (IsRecoverableConsoleException(ex))
            {
                return false;
            }
        }

        private static void AssertInitialized()
        {
            if (!_initialized) Initialize();
        }

        private static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            var callback = ShutDownRequest;
            if (callback == null) return;
            callback(sender, e);
            if (!e.Cancel) InputPrefix = string.Empty;
        }

        #region Refresh Prefix
        private static string _inputPrefix = string.Empty;
        private static int _inputStateRevision;
        /// <summary>
        /// The command line input prefix. When you invoke <see cref="ConsoleWrapper.ReadLine()"/> or <see cref="ConsoleWrapper.ReadLineAsync()"/>, <see cref="ConsoleWrapper"/> will add a prefix to the user's input.
        /// <para>For example, if this is set to "&gt; ", then user will see "&gt; " at the bottom of the console.</para>
        /// </summary>
        public static string InputPrefix
        {
            get
            {
                lock (InputStateLock)
                {
                    return _inputPrefix;
                }
            }
            set
            {
                lock (InputStateLock)
                {
                    _inputPrefix = value ?? string.Empty;
                    Interlocked.Increment(ref _inputStateRevision);
                }
                SignalUpdate();
            }
        }
        #endregion

        #region Read & Write
        #region ReadLine
        public static async Task<string> ReadLineAsync(bool reserve_in_history = true, CancellationToken cancellationToken = default)
        {
            AssertInitialized();
            await ReadLineLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            bool interactiveRead = _interactiveConsole;
            int readSessionId = interactiveRead ? StartInteractiveReadSession() : 0;
            bool inputCompleted = false;
            try
            {
                Volatile.Write(ref _isReading, 1);
                SignalUpdate();

                string? result;
                if (!interactiveRead)
                {
                    result = await ReadRedirectedLineAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    while (!TryTakeCompletedReadLine(readSessionId, out result))
                        await Task.Delay(RefreshTicks, cancellationToken).ConfigureAwait(false);
                    inputCompleted = true;
                }

                result ??= string.Empty;
                AddHistoryRecord(result, reserve_in_history);
                return result;
            }
            finally
            {
                Volatile.Write(ref _isReading, 0);
                if (interactiveRead)
                {
                    Interlocked.CompareExchange(ref _activeReadSessionId, 0, readSessionId);
                    if (!inputCompleted)
                        RequestInteractiveInputCancellation(readSessionId);
                }
                SignalUpdate();
                ReadLineLock.Release();
            }
        }

        public static string ReadLine(bool reserve_in_history = true)
        {
            AssertInitialized();
            ReadLineLock.Wait();
            bool interactiveRead = _interactiveConsole;
            int readSessionId = interactiveRead ? StartInteractiveReadSession() : 0;
            bool inputCompleted = false;
            try
            {
                Volatile.Write(ref _isReading, 1);
                SignalUpdate();

                string? result;
                if (!interactiveRead)
                {
                    result = ReadRedirectedLine();
                }
                else
                {
                    while (!TryTakeCompletedReadLine(readSessionId, out result))
                        Thread.Sleep(RefreshTicks);
                    inputCompleted = true;
                }

                result ??= string.Empty;
                AddHistoryRecord(result, reserve_in_history);
                return result;
            }
            finally
            {
                Volatile.Write(ref _isReading, 0);
                if (interactiveRead)
                {
                    Interlocked.CompareExchange(ref _activeReadSessionId, 0, readSessionId);
                    if (!inputCompleted)
                        RequestInteractiveInputCancellation(readSessionId);
                }
                SignalUpdate();
                ReadLineLock.Release();
            }
        }

        private static void AddHistoryRecord(string content, bool reserve_in_history)
        {
            if (reserve_in_history)
            {
                int _history_char_limit = HistoryMaximumChars;
                if (_history_char_limit == 0) return;

                lock (KeyHandlerLock)
                {
                    lock (lines)
                    {
                        if ((lines.Count == 0 || lines[lines.Count - 1] != content)
                            && !string.IsNullOrEmpty(content)
                            && content.Length <= _history_char_limit)
                        {
                            lines.Add(content);
                            if (keyHandler != null && keyHandler._historyIndex == lines.Count - 1)
                                keyHandler._historyIndex++;
                        }
                    }
                }
            }
        }

        private readonly struct CompletedReadLine
        {
            public int SessionId { get; }
            public string Text { get; }

            public CompletedReadLine(int sessionId, string text)
            {
                SessionId = sessionId;
                Text = text;
            }
        }

        private static int StartInteractiveReadSession()
        {
            int sessionId;
            do
            {
                sessionId = Interlocked.Increment(ref _nextReadSessionId);
            }
            while (sessionId == 0);
            Volatile.Write(ref _activeReadSessionId, sessionId);
            return sessionId;
        }

        private static bool TryTakeCompletedReadLine(int sessionId, out string? result)
        {
            while (readqueue.TryDequeue(out var completedLine))
            {
                if (completedLine.SessionId == sessionId)
                {
                    result = completedLine.Text;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static void RequestInteractiveInputCancellation(int readSessionId)
        {
            PendingInputCancellations.Enqueue(readSessionId);
            SignalUpdate();
        }

        private static async Task<string?> ReadRedirectedLineAsync(CancellationToken cancellationToken)
        {
            Task<string?> pendingRead;
            lock (RedirectedInputLock)
            {
                pendingRead = _pendingRedirectedRead ??= StartRedirectedRead();
            }

            bool shouldClearPendingRead = false;
            try
            {
                string? result = await pendingRead.WaitAsync(cancellationToken).ConfigureAwait(false);
                shouldClearPendingRead = true;
                return result;
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested && !pendingRead.IsCanceled)
            {
                throw;
            }
            catch
            {
                shouldClearPendingRead = true;
                throw;
            }
            finally
            {
                // Cancellation only detaches this waiter. The single underlying
                // read remains stored and its eventual result is consumed by the
                // next ReadLine call instead of silently swallowing that input.
                if (shouldClearPendingRead)
                {
                    lock (RedirectedInputLock)
                    {
                        if (ReferenceEquals(_pendingRedirectedRead, pendingRead))
                            _pendingRedirectedRead = null;
                    }
                }
            }
        }

        private static string? ReadRedirectedLine()
        {
            Task<string?> pendingRead;
            lock (RedirectedInputLock)
            {
                pendingRead = _pendingRedirectedRead ??= StartRedirectedRead();
            }

            try
            {
                return pendingRead.GetAwaiter().GetResult();
            }
            finally
            {
                lock (RedirectedInputLock)
                {
                    if (ReferenceEquals(_pendingRedirectedRead, pendingRead))
                        _pendingRedirectedRead = null;
                }
            }
        }

        private static Task<string?> StartRedirectedRead() =>
            Task.Run(static () => Console.ReadLine());
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
        {
            if (_interactiveConsole)
                input.WriteToConsole(shared_absconsole!);
            else
                Console.WriteLine(input.TextWithoutColor);
        }

        private static void EnqueueWriteLine(string? input)
        {
            if (input == null) return;
            lock (WriteQueueLock)
            {
                ThrowIfConsoleWritesCompleted();
                writelines.Enqueue(new PendingConsoleLine(input));
                MarkConsoleOutputPending();
            }
            SignalUpdate();
        }

        private static void EnqueueWriteLine(ColorLineResult input)
        {
            lock (WriteQueueLock)
            {
                ThrowIfConsoleWritesCompleted();
                writelines.Enqueue(new PendingConsoleLine(input));
                MarkConsoleOutputPending();
            }
            SignalUpdate();
        }

        private static void ThrowIfConsoleWritesCompleted()
        {
            if (_shutdownRequested != 0)
                throw new InvalidOperationException(
                    "ConsoleWrapper is shutting down and no longer accepts output.");
        }

        #region Outer WriteLine
        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        public static void WriteLine(string input)
        {
            AssertInitialized();

            EnqueueWriteLine(input);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        public static void WriteLine(string input1, string input2)
        {
            AssertInitialized();

            EnqueueWriteLine(input1);
            EnqueueWriteLine(input2);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        public static void WriteLine(string input1, string input2, string input3)
        {
            AssertInitialized();

            EnqueueWriteLine(input1);
            EnqueueWriteLine(input2);
            EnqueueWriteLine(input3);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        public static void WriteLine(IEnumerable<string> inputs)
        {
            AssertInitialized();

            foreach (var input in inputs) EnqueueWriteLine(input);
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

            EnqueueWriteLine(input);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        internal static void WriteLine(ColorLineResult input1, ColorLineResult input2)
        {
            AssertInitialized();

            EnqueueWriteLine(input1);
            EnqueueWriteLine(input2);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        internal static void WriteLine(ColorLineResult input1, ColorLineResult input2, ColorLineResult input3)
        {
            AssertInitialized();

            EnqueueWriteLine(input1);
            EnqueueWriteLine(input2);
            EnqueueWriteLine(input3);
        }

        /// <summary>
        /// WriteLine
        /// </summary>
        /// <param name="input">输出至控制台的文字。支持使用颜色，在文本中加入xml标签即可：&lt;color=Red&gt;红色文字&lt;&#47;color&gt;。颜色代码必须为<see cref="ConsoleColor"/>中的有效值，如"Red"、"Green"等。</param>
        internal static void WriteLine(IEnumerable<ColorLineResult> inputs)
        {
            AssertInitialized();

            foreach (var input in inputs) EnqueueWriteLine(input);
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
            ArgumentNullException.ThrowIfNull(initHistory);
            int historyMaximumChars = HistoryMaximumChars;
            List<string> replacement = initHistory
                .Where(x => x != null && x.Length <= historyMaximumChars)
                .ToList();
            lock (KeyHandlerLock)
            {
                lock (lines)
                {
                    lines.Clear();
                    lines.AddRange(replacement);
                    if (keyHandler != null) keyHandler._historyIndex = lines.Count;
                    Interlocked.Increment(ref _inputStateRevision);
                }
            }
            SignalUpdate();
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
                {
                    try
                    {
                        return checked(Math.Max(1, Console.BufferHeight) *
                            Math.Max(1, Console.BufferWidth));
                    }
                    catch (Exception ex) when (IsRecoverableConsoleException(ex))
                    {
                        return 2000;
                    }
                }
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
            try
            {
                ShutDownRequest?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                ReportBackgroundException(ex, "Shutdown request callback failed.");
            }
        }

        private static DelayConsole? shared_absconsole;
        private readonly struct QueuedConsoleKeyBatch
        {
            public int SessionId { get; }
            public ConsoleKeyInfo[] Keys { get; }
            public int SubmissionIndex { get; }
            public bool ContainsLineSubmission { get; }

            public QueuedConsoleKeyBatch(int sessionId, ConsoleKeyInfo[] keys)
            {
                SessionId = sessionId;
                Keys = keys;
                SubmissionIndex = Array.FindIndex(keys, IsLineSubmissionKey);
                ContainsLineSubmission = SubmissionIndex >= 0;
            }
        }

        private static readonly ConcurrentQueue<QueuedConsoleKeyBatch> qhandle_consolekeys = new();
        private static int _queuedSubmissionSessionId;
        private static KeyHandler? keyHandler;

        private static async Task BackgroundReadkey(CancellationToken cancellationToken)
        {
            var keyBuffer = new ConsoleKeyInfo[MaxReadKeyBatchSize];
            int failureDelay = 15;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Do not consume or queue terminal input before a caller is
                    // actually waiting for a line.
                    if (!IsReading)
                    {
                        await Task.Delay(25, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    int readSessionId = Volatile.Read(ref _activeReadSessionId);
                    if (readSessionId == 0)
                    {
                        await Task.Delay(15, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var console = shared_absconsole;
                    if (console == null)
                    {
                        await Task.Delay(15, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    int keyCount = console.ReadAvailableKeys(keyBuffer,
                        IsInputBatchBoundary);
                    if (keyCount == 0)
                    {
                        await Task.Delay(15, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    failureDelay = 15;
                    int ctrlCIndex = Array.FindIndex(keyBuffer, 0, keyCount,
                        static keyInfo => keyInfo.Modifiers == ConsoleModifiers.Control &&
                            keyInfo.Key == ConsoleKey.C);
                    int queuedKeyCount = ctrlCIndex >= 0 ? ctrlCIndex : keyCount;
                    if (queuedKeyCount > 0 &&
                        Volatile.Read(ref _activeReadSessionId) == readSessionId)
                    {
                        var queuedKeys = new ConsoleKeyInfo[queuedKeyCount];
                        Array.Copy(keyBuffer, queuedKeys, queuedKeyCount);
                        var queuedBatch = new QueuedConsoleKeyBatch(readSessionId, queuedKeys);
                        qhandle_consolekeys.Enqueue(queuedBatch);
                        if (queuedBatch.ContainsLineSubmission)
                            Volatile.Write(ref _queuedSubmissionSessionId, readSessionId);
                        SignalUpdate();
                    }

                    if (ctrlCIndex >= 0)
                    {
                        // Ctrl+C must remain observable as soon as the terminal
                        // delivers it, even when earlier pasted text has not
                        // reached the update worker yet.
                        ShutdownRequest_Callback();
                        continue;
                    }

                    if (queuedKeyCount > 0 &&
                        IsLineSubmissionKey(keyBuffer[queuedKeyCount - 1]))
                    {
                        // Do not pre-read pasted/type-ahead characters into the
                        // session whose Enter key is already queued. The update
                        // worker closes that session before the next read begins.
                        while (!cancellationToken.IsCancellationRequested &&
                            IsReading &&
                            Volatile.Read(ref _activeReadSessionId) == readSessionId)
                        {
                            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // KeyAvailable may repeatedly fail on detached terminals.
                    // Back off instead of pinning one CPU core.
                    ReportBackgroundException(ex, "Console key reader failed.");
                    try
                    {
                        await Task.Delay(failureDelay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    failureDelay = Math.Min(failureDelay * 2, 500);
                }
            }
        }

        private static bool IsInputBatchBoundary(ConsoleKeyInfo keyInfo) =>
            (keyInfo.Modifiers == ConsoleModifiers.Control && keyInfo.Key == ConsoleKey.C) ||
            IsLineSubmissionKey(keyInfo);

        private readonly struct PendingConsoleLine
        {
            public string? Text { get; }
            public ColorLineResult? ColoredText { get; }

            public PendingConsoleLine(string text)
            {
                Text = text;
                ColoredText = null;
            }

            public PendingConsoleLine(ColorLineResult coloredText)
            {
                Text = null;
                ColoredText = coloredText;
            }
        }

        private static readonly ConcurrentQueue<PendingConsoleLine> writelines = new();
        private static long _firstPendingConsoleOutputTimestamp;

        private static void MarkConsoleOutputPending()
        {
            Interlocked.CompareExchange(ref _firstPendingConsoleOutputTimestamp,
                Stopwatch.GetTimestamp(), 0);
        }

        private static void StartNextConsoleOutputFairnessWindow()
        {
            lock (WriteQueueLock)
            {
                Volatile.Write(ref _firstPendingConsoleOutputTimestamp,
                    writelines.IsEmpty ? 0 : Stopwatch.GetTimestamp());
            }
        }

        internal static volatile bool _clearup_completed;

        private static int _isReading;
        private static int _shutdownRequested;
        private static bool IsReading => Volatile.Read(ref _isReading) != 0;

        private static bool Writelines_waiting_handle
            => !writelines.IsEmpty;

        private static IAutoCompleteHandler? _autoCompleteHandler;
        public static IAutoCompleteHandler? AutoCompleteHandler
        {
            get
            {
                lock (InputStateLock)
                {
                    return _autoCompleteHandler;
                }
            }
            set
            {
                lock (InputStateLock)
                {
                    _autoCompleteHandler = value;
                    Interlocked.Increment(ref _inputStateRevision);
                }
                SignalUpdate();
            }
        }

        private static (string prefix, IAutoCompleteHandler? handler, int revision) GetInputStateSnapshot()
        {
            lock (InputStateLock)
            {
                return (_inputPrefix, _autoCompleteHandler, _inputStateRevision);
            }
        }

        private static void SignalUpdate()
        {
            try
            {
                UpdateSignal.Release();
            }
            catch (SemaphoreFullException)
            {
                // A pending signal already represents all accumulated changes.
            }
        }

        internal static void RequestShutdown()
        {
            if (!_initialized) return;
            lock (WriteQueueLock)
            {
                _shutdownRequested = 1;
            }
            _backgroundCancellation?.Cancel();
            SignalUpdate();
        }

        private static bool CanCompleteShutdown()
        {
            lock (WriteQueueLock)
            {
                return _shutdownRequested != 0 && writelines.IsEmpty;
            }
        }

        internal static bool WaitForCleanup(TimeSpan timeout)
        {
            Task? updateTask;
            lock (InitializationLock)
            {
                updateTask = _backgroundUpdateTask;
            }

            if (updateTask == null) return true;
            try
            {
                return updateTask.Wait(timeout);
            }
            catch (AggregateException ex)
            {
                ReportBackgroundException(ex.Flatten(), "Console cleanup task failed.");
                return true;
            }
        }

        private static async Task BackgroundUpdate()
        {
            bool pre_reading = false;
            int preReadSessionId = 0;
            int keyHandlerSessionId = 0;
            int renderedInputRevision = -1;
            int handledPersistAreaRevision = -1;
            try
            {
                while (true)
                {
                    try
                    {
                        bool reading = IsReading;
                        int observedReadSessionId = reading
                            ? Volatile.Read(ref _activeReadSessionId)
                            : 0;
                        bool curHandleWritelines = Writelines_waiting_handle;
                        var inputState = GetInputStateSnapshot();
                        var persistAreaState = GetPersistAreaStateSnapshot();
                        bool inputStateChanged = renderedInputRevision != inputState.revision;
                        bool persistAreaChanged = handledPersistAreaRevision != persistAreaState.revision;
                        bool readingChanged = pre_reading != reading;
                        bool readSessionChanged = preReadSessionId != observedReadSessionId;
                        int persistAreaWaitMilliseconds = _interactiveConsole
                            ? GetPersistAreaWaitMilliseconds(persistAreaState.renderer)
                            : Timeout.Infinite;
                        bool progressDue = persistAreaWaitMilliseconds == 0;
                        bool needProgressUpdate = _interactiveConsole &&
                            (curHandleWritelines || persistAreaChanged || progressDue);
                        bool haveKeysToHandle = reading && observedReadSessionId != 0 &&
                            !qhandle_consolekeys.IsEmpty;
                        bool haveInputCancellation = !PendingInputCancellations.IsEmpty;
                        bool needInputRefresh = inputStateChanged || readingChanged ||
                            readSessionChanged;

                        if (needProgressUpdate)
                        {
                            MarkConsoleOutputPending();
                        }

                        long now = Stopwatch.GetTimestamp();
                        long pendingConsoleOutputSince = Volatile.Read(
                            ref _firstPendingConsoleOutputTimestamp);

                        // Once an Enter batch is queued, finish every earlier
                        // batch from the same session before yielding to output.
                        // This preserves a paste followed immediately by Enter
                        // as one submitted line even under sustained logging.
                        bool submissionPending = observedReadSessionId != 0 &&
                            Volatile.Read(ref _queuedSubmissionSessionId) ==
                                observedReadSessionId;
                        bool consoleOutputBudgetExpired = pendingConsoleOutputSince != 0 &&
                            now - pendingConsoleOutputSince >=
                                InputOutputFairnessStopwatchTicks;
                        bool deferConsoleOutputForInput = needProgressUpdate &&
                            haveKeysToHandle && (submissionPending ||
                            !consoleOutputBudgetExpired);
                        bool handleConsoleOutput = needProgressUpdate &&
                            !deferConsoleOutputForInput;

                        if (!curHandleWritelines && !haveKeysToHandle &&
                            !inputStateChanged && !readingChanged && !readSessionChanged &&
                            !needProgressUpdate &&
                            !haveInputCancellation)
                        {
                            if (CanCompleteShutdown())
                                break;
                            await UpdateSignal.WaitAsync(persistAreaWaitMilliseconds).ConfigureAwait(false);
                            continue;
                        }

                        if (!_interactiveConsole)
                        {
                            ProcessWriteBatch();
                            StartNextConsoleOutputFairnessWindow();
                            renderedInputRevision = inputState.revision;
                            handledPersistAreaRevision = persistAreaState.revision;
                            pre_reading = reading;
                            preReadSessionId = observedReadSessionId;
                            if (CanCompleteShutdown())
                                break;
                            continue;
                        }

                        var console = shared_absconsole
                            ?? throw new InvalidOperationException("Console wrapper has no console abstraction.");

                        // Drain on every active iteration as well as on the
                        // snapshot that woke us. A cancellation can arrive
                        // after the snapshot while a new read session starts.
                        if (ProcessPendingInputCancellations(ref keyHandlerSessionId))
                        {
                            pre_reading = false;
                            continue;
                        }

                        if (!reading && pre_reading && keyHandler != null)
                        {
                            lock (KeyHandlerLock)
                            {
                                try
                                {
                                    keyHandler?.ClearWrittingStatus();
                                }
                                catch (Exception ex) when (IsRecoverableConsoleException(ex))
                                {
                                    console.Resync();
                                }
                                keyHandler = null;
                                keyHandlerSessionId = 0;
                            }
                        }

                        // Input configuration changes must apply before the
                        // next key. Log and persistent-area output, in contrast,
                        // may wait briefly while an already queued paste drains.
                        if (handleConsoleOutput || needInputRefresh)
                        {
                            // The idle handler created at initialization has no
                            // read session and may point at output written since
                            // then. Only erase a rectangle owned by a live read.
                            if (reading && keyHandler != null && keyHandlerSessionId != 0)
                            {
                                lock (KeyHandlerLock)
                                {
                                    try
                                    {
                                        if (IsReading)
                                            keyHandler?.ClearWrittingStatus();
                                    }
                                    catch (Exception ex) when (IsRecoverableConsoleException(ex))
                                    {
                                        console.Resync();
                                    }
                                }
                            }

                            if (handleConsoleOutput)
                            {
                                if (persistAreaChanged)
                                    _cachedProgressInfo = null;
                                ClearProgressBar();
                                if (curHandleWritelines)
                                    ProcessWriteBatch();
                                console.Resync();
                                if (persistAreaState.renderer != null)
                                    RenderProgressBar(persistAreaState.renderer,
                                        persistAreaChanged || progressDue);
                                handledPersistAreaRevision = persistAreaState.revision;
                                // The next group of pending output gets a fresh
                                // fairness window after this bounded write pass.
                                StartNextConsoleOutputFairnessWindow();
                            }

                            if (reading)
                            {
                                lock (KeyHandlerLock)
                                {
                                    if (IsReading)
                                    {
                                        int readSessionId = Volatile.Read(ref _activeReadSessionId);
                                        if (readSessionId != 0)
                                        {
                                            bool createdForSession = EnsureKeyHandlerForSession(
                                                console, readSessionId, inputState.prefix,
                                                inputState.handler, ref keyHandlerSessionId);
                                            if (!createdForSession && keyHandler != null)
                                            {
                                                RecoverKeyHandlerAfterExternalOutput(
                                                    console, inputState.prefix,
                                                    inputState.handler);
                                            }
                                        }
                                    }
                                }
                            }

                            renderedInputRevision = inputState.revision;
                            pre_reading = reading;
                            preReadSessionId = observedReadSessionId;
                            if (CanCompleteShutdown())
                                break;
                        }

                        if (reading && haveKeysToHandle)
                        {
                            lock (KeyHandlerLock)
                            {
                                if (IsReading)
                                {
                                    int readSessionId = Volatile.Read(ref _activeReadSessionId);
                                    if (readSessionId != 0)
                                    {
                                        EnsureKeyHandlerForSession(console, readSessionId,
                                            inputState.prefix, inputState.handler,
                                            ref keyHandlerSessionId);

                                        if (keyHandler != null &&
                                            TryDequeueInputBatch(readSessionId,
                                                out var queuedBatch))
                                        {
                                            int keyCount = queuedBatch.Keys.Length;
                                            int handledKeyCount = queuedBatch.ContainsLineSubmission
                                                ? queuedBatch.SubmissionIndex
                                                : keyCount;
                                            if (handledKeyCount > 0)
                                            {
                                                keyHandler.HandleBatch(
                                                    new ArraySegment<ConsoleKeyInfo>(
                                                        queuedBatch.Keys, 0, handledKeyCount));
                                            }

                                            if (queuedBatch.ContainsLineSubmission && IsReading &&
                                                Interlocked.CompareExchange(
                                                    ref _activeReadSessionId, 0,
                                                    readSessionId) == readSessionId)
                                            {
                                                string completedText = keyHandler.Text;
                                                try
                                                {
                                                    keyHandler.MoveCursorToEndForSubmit();
                                                    console.WriteLine(string.Empty);
                                                }
                                                catch (Exception ex)
                                                {
                                                    ReportBackgroundException(ex,
                                                        "Submitting interactive input failed to update the terminal.");
                                                    if (IsRecoverableConsoleException(ex))
                                                    {
                                                        try
                                                        {
                                                            console.Resync();
                                                        }
                                                        catch (Exception recoverEx)
                                                        {
                                                            ReportBackgroundException(recoverEx,
                                                                "Submitting interactive input failed to resynchronize the terminal.");
                                                        }
                                                    }
                                                }
                                                readqueue.Enqueue(new CompletedReadLine(
                                                    readSessionId, completedText));
                                                // Stop consuming terminal input until the caller
                                                // starts the next session. Buffered type-ahead is
                                                // then tagged with that new session instead of the
                                                // line that has already completed.
                                                keyHandler = null;
                                                keyHandlerSessionId = 0;
                                                Interlocked.CompareExchange(
                                                    ref _queuedSubmissionSessionId, 0,
                                                    readSessionId);
                                            }
                                            else if (queuedBatch.ContainsLineSubmission)
                                            {
                                                Interlocked.CompareExchange(
                                                    ref _queuedSubmissionSessionId, 0,
                                                    readSessionId);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        pre_reading = reading;
                        preReadSessionId = observedReadSessionId;
                        if (CanCompleteShutdown())
                            break;
                    }
                    catch (Exception ex)
                    {
                        ReportBackgroundException(ex, "Console update worker failed.");
                        try
                        {
                            shared_absconsole?.Resync();
                        }
                        catch (Exception recoverEx)
                        {
                            ReportBackgroundException(recoverEx, "Console state recovery failed.");
                        }
                        await Task.Delay(20).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                try
                {
                    if (_interactiveConsole)
                        ClearProgressBar();
                }
                catch (Exception ex)
                {
                    ReportBackgroundException(ex, "Persist area cleanup failed.");
                }
                try
                {
                    Console.CancelKeyPress -= Console_CancelKeyPress;
                }
                catch (Exception ex)
                {
                    ReportBackgroundException(ex, "Console event cleanup failed.");
                }
                finally
                {
                    _clearup_completed = true;
                }
            }
        }

        private static bool EnsureKeyHandlerForSession(DelayConsole console,
            int readSessionId, string inputPrefix, IAutoCompleteHandler? autoCompleteHandler,
            ref int keyHandlerSessionId)
        {
            if (keyHandler != null && keyHandlerSessionId == readSessionId)
                return false;

            if (keyHandler != null && keyHandlerSessionId != 0)
            {
                try
                {
                    // Drafts and queued keys belong to exactly one read
                    // session. Never carry an abandoned draft into a newer one.
                    keyHandler.CancelInput();
                }
                catch (Exception ex) when (IsRecoverableConsoleException(ex))
                {
                    console.Resync();
                }
            }

            keyHandler = new KeyHandler(console, lines, autoCompleteHandler, inputPrefix);
            keyHandlerSessionId = readSessionId;
            return true;
        }

        private static void RecoverKeyHandlerAfterExternalOutput(DelayConsole console,
            string inputPrefix, IAutoCompleteHandler? autoCompleteHandler)
        {
            if (keyHandler == null)
                return;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    keyHandler.RecoverWrittingStatus(inputPrefix, autoCompleteHandler);
                    return;
                }
                catch (Exception ex) when (IsRecoverableConsoleException(ex))
                {
                    console.Resync();
                }
            }
        }

        private static bool TryDequeueInputBatch(int readSessionId,
            out QueuedConsoleKeyBatch queuedBatch)
        {
            while (qhandle_consolekeys.TryDequeue(out queuedBatch))
            {
                if (queuedBatch.SessionId == readSessionId)
                {
                    return true;
                }

                if (queuedBatch.ContainsLineSubmission)
                {
                    Interlocked.CompareExchange(ref _queuedSubmissionSessionId, 0,
                        queuedBatch.SessionId);
                }
            }

            queuedBatch = default;
            return false;
        }

        private static void ProcessWriteBatch()
        {
            int handled = 0;
            while (handled++ < MaxWriteBatchSize && writelines.TryDequeue(out var line))
            {
                try
                {
                    if (line.ColoredText is ColorLineResult coloredText)
                        InnerWriteLine(coloredText);
                    else if (line.Text != null)
                        InnerWriteLine(line.Text);
                }
                catch (Exception ex)
                {
                    ReportBackgroundException(ex, "Console line rendering failed.");
                    string? fallbackText = line.Text ?? line.ColoredText?.TextWithoutColor;
                    if (fallbackText != null)
                    {
                        try
                        {
                            Console.WriteLine(fallbackText);
                        }
                        catch (Exception fallbackEx)
                        {
                            ReportBackgroundException(fallbackEx, "Plain console fallback failed.");
                        }
                    }
                }
            }
        }

        private static bool ProcessPendingInputCancellations(ref int keyHandlerSessionId)
        {
            bool haveCancellation = false;
            bool cancelCurrentHandler = false;
            while (PendingInputCancellations.TryDequeue(out int cancelledSessionId))
            {
                haveCancellation = true;
                Interlocked.CompareExchange(ref _queuedSubmissionSessionId, 0,
                    cancelledSessionId);
                if (cancelledSessionId != 0 && cancelledSessionId == keyHandlerSessionId)
                    cancelCurrentHandler = true;
            }
            if (!haveCancellation)
                return false;

            if (!cancelCurrentHandler)
                return true;

            lock (KeyHandlerLock)
            {
                try
                {
                    keyHandler?.CancelInput();
                }
                catch (Exception ex)
                {
                    ReportBackgroundException(ex, "Cancelling interactive input failed.");
                }
                keyHandlerSessionId = 0;

                // Queued items are tagged with their read session. Leaving them
                // in place avoids deleting input that already belongs to a new
                // session; the normal dequeue paths discard stale generations.
            }
            return true;
        }

        private static bool IsLineSubmissionKey(ConsoleKeyInfo keyInfo) =>
            keyInfo.Key == ConsoleKey.Enter ||
            (keyInfo.Modifiers == ConsoleModifiers.Control &&
            (keyInfo.Key == ConsoleKey.M || keyInfo.Key == ConsoleKey.J));

        private static void ReportBackgroundException(Exception ex, string message)
        {
            // Never report a console worker failure through the logger itself:
            // doing so can recurse into this queue, and disk-disabled configs
            // may throw while trying to create an internal trace file.
            Debug.WriteLine($"{nameof(ConsoleWrapper)}: {message} {ex}");
        }

        private static bool IsRecoverableConsoleException(Exception ex) =>
            ex is IOException ||
            ex is InvalidOperationException ||
            ex is ArgumentOutOfRangeException ||
            ex is PlatformNotSupportedException;
        #endregion

        #region Progress Bar
        /// <summary>
        /// Set the handler to render a persisted area at the bottom of Console. Usually pass an implementation of <see cref="ProgressBarRenderHandlerBase"/>.
        /// </summary>
        public static PersistAreaRenderHandlerBase? PersistAreaRenderer
        {
            get
            {
                lock (PersistAreaLock)
                {
                    return _persistAreaRenderer;
                }
            }
            set
            {
                lock (PersistAreaLock)
                {
                    if (ReferenceEquals(_persistAreaRenderer, value)) return;
                    _persistAreaRenderer = value;
                    Interlocked.Increment(ref _persistAreaRevision);
                }
                MarkConsoleOutputPending();
                SignalUpdate();
            }
        }

        private static readonly object PersistAreaLock = new();
        private static PersistAreaRenderHandlerBase? _persistAreaRenderer;
        private static int _persistAreaRevision;
        private static ColorLineResult? _cachedProgressInfo;
        private static int _progressBarTakenLines;
        private static DateTimeOffset _renderedTime;
        private static readonly TimeSpan MinimumPersistAreaInterval = TimeSpan.FromMilliseconds(15);

        private static (PersistAreaRenderHandlerBase? renderer, int revision) GetPersistAreaStateSnapshot()
        {
            lock (PersistAreaLock)
            {
                return (_persistAreaRenderer, _persistAreaRevision);
            }
        }

        private static int GetPersistAreaWaitMilliseconds(PersistAreaRenderHandlerBase? renderer)
        {
            if (renderer == null) return Timeout.Infinite;

            TimeSpan interval;
            try
            {
                interval = renderer.CallbackInterval;
            }
            catch (Exception ex)
            {
                ReportBackgroundException(ex, "Persist area interval callback failed.");
                interval = MinimumPersistAreaInterval;
            }

            if (interval < MinimumPersistAreaInterval)
                interval = MinimumPersistAreaInterval;

            long elapsedTicks = Math.Max(0, (DateTimeOffset.UtcNow - _renderedTime).Ticks);
            if (elapsedTicks >= interval.Ticks)
                return 0;

            long remainingTicks = interval.Ticks - elapsedTicks;
            long remainingMilliseconds = remainingTicks / TimeSpan.TicksPerMillisecond;
            if (remainingTicks % TimeSpan.TicksPerMillisecond != 0)
                remainingMilliseconds++;
            return (int)Math.Clamp(remainingMilliseconds, 1, int.MaxValue);
        }

        private static void ClearProgressBar()
        {
            int takenLines = _progressBarTakenLines;
            _progressBarTakenLines = 0;
            var console = shared_absconsole;
            if (takenLines <= 0 || console == null) return;

            try
            {
                console.Resync();
                int cursorTop = Math.Clamp(console.CursorTop, 0, console.BufferHeight - 1);
                int rowsToClear = Math.Min(takenLines, cursorTop + 1);
                int bufferWidth = console.BufferWidth;
                int clearWidth = Math.Max(0, bufferWidth - 1);
                string blankLine = new(' ', clearWidth);

                for (int i = 0; i < rowsToClear; i++)
                {
                    int row = cursorTop - i;
                    console.SetCursorPosition(0, row);
                    console.Write(blankLine);
                    // Writing BufferWidth spaces at once can wrap and scroll
                    // the terminal. Clear the final cell with one positioned
                    // write so the rightmost character cannot remain as a ghost.
                    console.SetCursorPosition(bufferWidth - 1, row);
                    console.Write(' ');
                    console.SetCursorPosition(0, row);
                }

                int firstClearedRow = Math.Max(0, cursorTop - rowsToClear + 1);
                console.SetCursorPosition(0, firstClearedRow);
                console.Flush();
            }
            catch (Exception ex) when (IsRecoverableConsoleException(ex))
            {
                // A resize can make every saved row invalid. The count was
                // cleared before attempting recovery so the worker will not
                // repeatedly underflow CursorTop on every refresh.
                console.Resync();
            }
        }

        private static void RenderProgressBar(PersistAreaRenderHandlerBase renderer, bool refreshCache)
        {
            if (refreshCache)
            {
                string? text;
                try
                {
                    text = renderer.Render();
                }
                catch (Exception ex)
                {
                    text = $"<color=Red><PROGRESS BAR> ??.??%[ERROR {ex.GetType().Name} {ex.Message}]</color>";
                    ReportBackgroundException(ex, $"Persist area renderer {renderer.GetType().Name} failed.");
                }
                if (!string.IsNullOrEmpty(text))
                {
                    // 复习经典理论：\r（回车）将光标移动到行的开头，\n（换行）
                    // 将光标移动到下一行但不回到行首。但在实践中，我们观察到仅
                    // 使用 Console.Write('\n') 进行换行也会同时达成回车的效果
                    // （而不是不回到行首），故这里为了与 ColorLineUtil 计算行
                    // 数的方式兼容，不使用当前系统下标准的换行实现。
                    // 理论上更合适的方式是开个洞然后使用标准的
                    // Console.WriteLine，但是为了兼容性考虑使用后期处理的方式。
                    text = text.ReplaceLineEndings("\n");
                    _cachedProgressInfo = ColorLineUtil.AnalyzeColorText(text);
                }
                else _cachedProgressInfo = null;
                _renderedTime = DateTimeOffset.UtcNow;
            }

            var console = shared_absconsole;
            if (_cachedProgressInfo == null || console == null)
            {
                _progressBarTakenLines = 0;
                return;
            }

            try
            {
                _progressBarTakenLines = _cachedProgressInfo.Value.WriteAndCountLines(console);
            }
            catch (Exception ex) when (IsRecoverableConsoleException(ex))
            {
                _progressBarTakenLines = 0;
                console.Resync();
            }
        }
        #endregion
    }
}
