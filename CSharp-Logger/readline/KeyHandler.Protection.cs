namespace Internal.ReadLine
{
    internal partial class KeyHandler
    {
        /// <summary>
        /// Blocks an undefined key unless the console supplied a printable
        /// KeyChar.  KeyChar is authoritative for keyboard-layout output such
        /// as AltGr and Oem102.
        /// </summary>
        private static bool BlockKey(ConsoleKeyInfo keyInfo)
        {
            // Explicit actions are resolved before this method is called.
            // Consequently an undefined NUL/TAB/control combination is always
            // ignored, while a real printable layout result is inserted.
            return !IsPotentialTextUnit(keyInfo.KeyChar);
        }

        /// <summary>
        /// Keys that have no textual meaning unless an explicit handler is
        /// registered.  This list remains available for compatibility with the
        /// original readline implementation.
        /// </summary>
        public static readonly ConsoleKey[] IgnoreUndefinedKeys = new ConsoleKey[]
        {
            ConsoleKey.LeftArrow,
            ConsoleKey.RightArrow,
            ConsoleKey.UpArrow,
            ConsoleKey.DownArrow,
            ConsoleKey.Delete,
            ConsoleKey.Home,
            ConsoleKey.End,
            ConsoleKey.Enter,
            ConsoleKey.Clear,
            ConsoleKey.Pause,
            ConsoleKey.PageUp,
            ConsoleKey.PageDown,
            ConsoleKey.Select,
            ConsoleKey.Print,
            ConsoleKey.Execute,
            ConsoleKey.PrintScreen,
            ConsoleKey.Insert,
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
    }
}
