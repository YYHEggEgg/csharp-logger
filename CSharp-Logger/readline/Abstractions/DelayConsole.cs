using Internal.ReadLine.Abstractions;

namespace YYHEggEgg.Logger.readline.Abstractions;

internal class DelayConsole : IConsole
{
    private int _tmpCursorLeft = Console.CursorLeft;
    private int _tmpCursorTop = Console.CursorTop;
    /// <summary>
    /// 该值是控制台的更新版本，即应该在未来向控制台提交。
    /// </summary>
    private bool _tmpCursor_dirty = false;
    /// <summary>
    /// 该值落后于控制台状态，即应该在未来重新从控制台拉取。
    /// </summary>
    private bool _tmpCursor_desync = false;
    /// <summary>
    /// 在 Unix 平台上游标操作依赖 ANSI 兼容终端的 CPR 协议，即运行时向
    /// stdin 写请求 (<c>ESC [ 6 n</c>, <c>ESC</c> 为 <c>\e</c>)，
    /// 再从 stdout 读取响应 (<c>ESC [ [row] ; [column] R</c>)，
    /// 所以需要同时操作 stdin 和 stdout.
    /// 目前发现 <see cref="Console.KeyAvailable"/> 和
    /// <see cref="Console.GetCursorPosition()"/>
    /// 联合使用有概率造成死锁，故添加二次上锁机制。
    /// </summary>
    private object _consoleOpLock = new object();

    public int CursorLeft => _tmpCursorLeft;

    public int CursorTop => _tmpCursorTop;

    public int BufferWidth => Console.WindowWidth;

    public int BufferHeight => Console.WindowHeight;

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

    private void ThrowIfDesync()
    {
        if (_tmpCursor_desync)
            throw new InvalidOperationException("Local state became dirty before syncronizing with Console.");
    }

    private void ThrowIfDirty()
    {
        if (_tmpCursor_dirty)
            throw new InvalidOperationException("Trying to disturb Console before committing dirty state.");
    }

    public void Flush()
    {
        ThrowIfDesync();
        if (_tmpCursor_dirty)
        {
            Console.SetCursorPosition(_tmpCursorLeft, _tmpCursorTop);
            _tmpCursor_dirty = false;
        }
    }

    public void SetCursorPosition(int left, int top)
    {
        ThrowIfDesync();
        // Basic argument validation.  The PAL implementation may provide further validation.
        if (left < 0 || left >= short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(left));
        if (top < 0 || top >= short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(top));

        _tmpCursorLeft = left;
        _tmpCursorTop = top;
        _tmpCursor_dirty = true;
    }

    public void Write(char value)
    {
#if false
        Log.Verb("Flush", nameof(DelayConsole));
#endif
        Flush();
#if false
        Log.Verb($"Console.Write({value})", nameof(DelayConsole));
#endif
        Console.Write(value);
#if false
        Log.Verb("Resync start", nameof(DelayConsole));
#endif
        Resync();
#if false
        Log.Verb("Resync FIN", nameof(DelayConsole));
#endif
    }

    public void Write(string value)
    {
        Flush();
        Console.Write(value);
        Resync();
    }

    public void WriteLine(string value)
    {
        Flush();
        Console.WriteLine(value);
        Resync();
    }

    public void TryClear()
    {
        try
        {
            Console.Clear();
        }
        catch (Exception ex)
        {
            LogTrace.VerbTrace(ex, nameof(DelayConsole), $"Console Abstraction handler met error when cleaning console for emergency.");
        }
        Resync();
    }

    /// <summary>
    /// 进行 <see cref="Console.WriteLine(string?)"/> 但不将
    /// <see cref="_tmpCursorLeft"/> 等进行同步。完成全部操作后
    /// 需手动调用 <see cref="Resync()"/>.
    /// </summary>
    public void WriteNonSync(string value)
    {
        ThrowIfDirty();
        Console.Write(value);
        _tmpCursor_desync = true;
    }

    public void WriteLineNonSync(string value)
    {
        ThrowIfDirty();
        Console.WriteLine(value);
        _tmpCursor_desync = true;
    }

    public void Resync()
    {
        ThrowIfDirty();
        lock (_consoleOpLock)
        {
            (_tmpCursorLeft, _tmpCursorTop) = Console.GetCursorPosition();
        }
        _tmpCursor_desync = false;
    }
}
