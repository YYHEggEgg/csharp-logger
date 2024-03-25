using Internal.ReadLine.Abstractions;

namespace YYHEggEgg.Logger.readline.Abstractions
{
    internal class DelayConsole : IConsole
    {
        private int _tmpCursorLeft = Console.CursorLeft;
        private int _tmpCursorTop = Console.CursorTop;
        private bool _tmpCursor_desync = false;

        public int CursorLeft => _tmpCursorLeft;

        public int CursorTop => _tmpCursorTop;

        public int BufferWidth => Console.WindowWidth;

        public int BufferHeight => Console.WindowHeight;

        public void Flush()
        {
            if (_tmpCursor_desync)
            {
                Console.SetCursorPosition(_tmpCursorLeft, _tmpCursorTop);
                _tmpCursor_desync = false;
            }
        }

        public void SetCursorPosition(int left, int top)
        {
            // Basic argument validation.  The PAL implementation may provide further validation.
            if (left < 0 || left >= short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(left));
            if (top < 0 || top >= short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(top));

            _tmpCursorLeft = left;
            _tmpCursorTop = top;
            _tmpCursor_desync = true;
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

        public void Resync()
        {
            _tmpCursorLeft = Console.CursorLeft;
            _tmpCursorTop = Console.CursorTop;
        }

        public void Clear()
        {
            Console.Clear();
            Resync();
        }
    }
}
