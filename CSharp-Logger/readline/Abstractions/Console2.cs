namespace Internal.ReadLine.Abstractions
{
    internal class Console2 : IConsole
    {
        private object _consoleOpLock = new object();

        public int CursorLeft
        {
            get
            {
                lock (_consoleOpLock)
                {
                    return Console.CursorLeft;
                }
            }
        }

        public int CursorTop
        {
            get
            {
                lock (_consoleOpLock)
                {
                    return Console.CursorTop;
                }
            }
        }

        public int BufferWidth => Console.BufferWidth;

        public int BufferHeight => Console.BufferHeight;

        public bool PasswordMode { get; set; }

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

        // public void SetBufferSize(int width, int height) => Console.SetBufferSize(width, height);

        public void SetCursorPosition(int left, int top)
        {
            if (!PasswordMode)
                Console.SetCursorPosition(left, top);
        }

        public void Write(char value)
        {
            if (PasswordMode)
                value = default(char);

            Console.Write(value);
        }

        public void Write(string value)
        {
            if (PasswordMode)
                value = new String(default(char), value.Length);

            Console.Write(value);
        }

        public void WriteLine(string value) => Console.WriteLine(value);

        public void Flush() { }
        public void Resync() { }
        public void TryClear() => Console.Clear();
        public void WriteNonSync(string value) => Write(value);
        public void WriteLineNonSync(string value) => WriteLine(value);
    }
}
