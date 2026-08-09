using Internal.ReadLine.Abstractions;
using System.Diagnostics;

namespace YYHEggEgg.Logger.readline.Abstractions;

internal sealed class DelayConsole : IConsole
{
    private readonly object _consoleOpLock = new();
    private readonly bool _cursorOperationsEnabled;
    private int _tmpCursorLeft;
    private int _tmpCursorTop;

    /// <summary>
    /// The cached cursor position is newer than the physical console and must
    /// be committed before writing.
    /// </summary>
    private bool _tmpCursor_dirty;

    /// <summary>
    /// The physical console is newer than the cached cursor position and must
    /// be queried before another cursor operation.
    /// </summary>
    private bool _tmpCursor_desync;

    /// <summary>
    /// On Unix, cursor queries and key reads may both use the terminal input
    /// stream. Keeping them under the same lock prevents the two protocols
    /// from consuming each other's input.
    /// </summary>
    public DelayConsole(bool cursorOperationsEnabled = true)
    {
        _cursorOperationsEnabled = cursorOperationsEnabled;
        lock (_consoleOpLock)
        {
            TryResyncCore();
        }
    }

    public int CursorLeft
    {
        get
        {
            lock (_consoleOpLock)
            {
                return _tmpCursorLeft;
            }
        }
    }

    public int CursorTop
    {
        get
        {
            lock (_consoleOpLock)
            {
                return _tmpCursorTop;
            }
        }
    }

    // Cursor coordinates are buffer-relative, so WindowWidth/WindowHeight
    // cannot be used here when the window is scrolled inside a larger buffer.
    public int BufferWidth
    {
        get
        {
            lock (_consoleOpLock)
            {
                return GetBufferWidthCore();
            }
        }
    }

    public int BufferHeight
    {
        get
        {
            lock (_consoleOpLock)
            {
                return GetBufferHeightCore();
            }
        }
    }

    public bool KeyAvailable
    {
        get
        {
            lock (_consoleOpLock)
            {
                return Console.KeyAvailable;
            }
        }
    }

    /// <summary>
    /// Atomically checks for and reads one key while holding the same lock as
    /// cursor queries. This avoids the race between KeyAvailable and ReadKey
    /// and also covers the actual ReadKey call on Unix.
    /// </summary>
    public bool TryReadKey(out ConsoleKeyInfo keyInfo)
    {
        lock (_consoleOpLock)
        {
            if (!Console.KeyAvailable)
            {
                keyInfo = default;
                return false;
            }

            keyInfo = Console.ReadKey(true);
            return true;
        }
    }

    /// <summary>
    /// Drains only keys which have already reached the terminal input queue.
    /// Keeping the complete drain under the console-operation lock prevents a
    /// cursor query on Unix from interleaving with a read-key protocol.
    /// </summary>
    public int ReadAvailableKeys(Span<ConsoleKeyInfo> destination,
        Func<ConsoleKeyInfo, bool>? stopAfterKey)
    {
        lock (_consoleOpLock)
        {
            int count = 0;
            while (count < destination.Length && Console.KeyAvailable)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);
                destination[count++] = keyInfo;
                if (stopAfterKey?.Invoke(keyInfo) == true)
                    break;
            }
            return count;
        }
    }

    private void ThrowIfDesync()
    {
        if (_tmpCursor_desync)
            throw new InvalidOperationException("Local state became dirty before synchronizing with Console.");
    }

    private void ThrowIfDirty()
    {
        if (_tmpCursor_dirty)
            throw new InvalidOperationException("Trying to disturb Console before committing dirty state.");
    }

    public void Flush()
    {
        lock (_consoleOpLock)
        {
            ThrowIfDesync();
            FlushCore();
        }
    }

    private void FlushCore()
    {
        if (!_tmpCursor_dirty)
            return;

        if (!_cursorOperationsEnabled)
        {
            _tmpCursor_dirty = false;
            return;
        }

        int left = Math.Clamp(_tmpCursorLeft, 0, GetBufferWidthCore() - 1);
        int top = Math.Clamp(_tmpCursorTop, 0, GetBufferHeightCore() - 1);
        try
        {
            Console.SetCursorPosition(left, top);
            _tmpCursorLeft = left;
            _tmpCursorTop = top;
        }
        catch (Exception ex) when (IsRecoverableConsoleException(ex))
        {
            // A resize can invalidate a previously valid position between the
            // calculation and SetCursorPosition. Discard the pending position
            // and recover from the terminal instead of poisoning all future
            // writes with a permanently dirty cache.
            Debug.WriteLine($"DelayConsole cursor flush failed: {ex}");
            TryResyncCore();
        }
        finally
        {
            _tmpCursor_dirty = false;
        }
    }

    public void SetCursorPosition(int left, int top)
    {
        lock (_consoleOpLock)
        {
            ThrowIfDesync();
            if (left < 0 || left >= short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(left));
            if (top < 0 || top >= short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(top));

            _tmpCursorLeft = left;
            _tmpCursorTop = top;
            _tmpCursor_dirty = true;
        }
    }

    public void Write(char value)
    {
        lock (_consoleOpLock)
        {
            ThrowIfDesync();
            FlushCore();
            try
            {
                Console.Write(value);
            }
            finally
            {
                TryResyncCore();
            }
        }
    }

    public void Write(string value)
    {
        lock (_consoleOpLock)
        {
            ThrowIfDesync();
            FlushCore();
            try
            {
                Console.Write(value);
            }
            finally
            {
                TryResyncCore();
            }
        }
    }

    public void WriteLine(string value)
    {
        lock (_consoleOpLock)
        {
            ThrowIfDesync();
            FlushCore();
            try
            {
                Console.WriteLine(value);
            }
            finally
            {
                TryResyncCore();
            }
        }
    }

    public void TryClear()
    {
        lock (_consoleOpLock)
        {
            try
            {
                Console.Clear();
            }
            catch (Exception ex) when (IsRecoverableConsoleException(ex))
            {
                Debug.WriteLine($"DelayConsole emergency clear failed: {ex}");
            }
            finally
            {
                _tmpCursor_dirty = false;
                _tmpCursor_desync = false;
                TryResyncCore();
            }
        }
    }

    /// <summary>
    /// Writes without refreshing the cached cursor. Call <see cref="Resync"/>
    /// after a group of non-synchronized operations.
    /// </summary>
    public void WriteNonSync(string value)
    {
        lock (_consoleOpLock)
        {
            ThrowIfDirty();
            try
            {
                Console.Write(value);
            }
            finally
            {
                _tmpCursor_desync = _cursorOperationsEnabled;
            }
        }
    }

    public void WriteLineNonSync(string value)
    {
        lock (_consoleOpLock)
        {
            ThrowIfDirty();
            try
            {
                Console.WriteLine(value);
            }
            finally
            {
                _tmpCursor_desync = _cursorOperationsEnabled;
            }
        }
    }

    public void Resync()
    {
        lock (_consoleOpLock)
        {
            // Resync is also the recovery primitive after an interrupted
            // multi-step cursor operation. Discard any uncommitted position;
            // otherwise one failed render can leave the abstraction poisoned
            // and make every subsequent recovery attempt throw.
            _tmpCursor_dirty = false;
            TryResyncCore();
        }
    }

    private bool TryResyncCore()
    {
        if (!_cursorOperationsEnabled)
        {
            _tmpCursorLeft = 0;
            _tmpCursorTop = 0;
            _tmpCursor_dirty = false;
            _tmpCursor_desync = false;
            return true;
        }

        try
        {
            (_tmpCursorLeft, _tmpCursorTop) = Console.GetCursorPosition();
            _tmpCursor_desync = false;
            return true;
        }
        catch (Exception ex) when (IsRecoverableConsoleException(ex))
        {
            _tmpCursorLeft = Math.Clamp(_tmpCursorLeft, 0, GetBufferWidthCore() - 1);
            _tmpCursorTop = Math.Clamp(_tmpCursorTop, 0, GetBufferHeightCore() - 1);
            _tmpCursor_desync = false;
            Debug.WriteLine($"DelayConsole cursor resync failed: {ex}");
            return false;
        }
    }

    private static bool IsRecoverableConsoleException(Exception ex) =>
        ex is IOException ||
        ex is InvalidOperationException ||
        ex is ArgumentOutOfRangeException ||
        ex is PlatformNotSupportedException;

    private static int GetBufferWidthCore()
    {
        try
        {
            return Math.Max(1, Console.BufferWidth);
        }
        catch (Exception ex) when (IsRecoverableConsoleException(ex))
        {
            try
            {
                return Math.Max(1, Console.WindowWidth);
            }
            catch (Exception fallbackEx) when (IsRecoverableConsoleException(fallbackEx))
            {
                return 80;
            }
        }
    }

    private static int GetBufferHeightCore()
    {
        try
        {
            return Math.Max(1, Console.BufferHeight);
        }
        catch (Exception ex) when (IsRecoverableConsoleException(ex))
        {
            try
            {
                return Math.Max(1, Console.WindowHeight);
            }
            catch (Exception fallbackEx) when (IsRecoverableConsoleException(fallbackEx))
            {
                return 1;
            }
        }
    }
}
