namespace Internal.ReadLine.Abstractions
{
    internal interface IConsole
    {
        int CursorLeft { get; }
        int CursorTop { get; }
        int BufferWidth { get; }
        int BufferHeight { get; }
        void SetCursorPosition(int left, int top);
        // void SetBufferSize(int width, int height);
        void Write(char value);
        void Write(string value);
        void WriteLine(string value);
        /// <summary>
        /// 在最后一次调用 <see cref="SetCursorPosition(int, int)"/>
        /// 后调用。应保证其他 <see cref="Write(string)"/> 方法均自动调用
        /// <see cref="Flush"/>.
        /// </summary>
        void Flush();
        /// <summary>
        /// 在外部调用控制台方法后调用。应保证其他 <see cref="Write(string)"/>
        /// 方法均自动调用 <see cref="Resync"/>.
        /// </summary>
        void Resync();
        void Clear();
    }
}