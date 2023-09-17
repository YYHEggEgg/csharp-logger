using System.Collections.ObjectModel;

namespace Internal.ReadLine
{
    internal partial class KeyHandler
    {
        /// <summary>
        /// 除非显式定义了处理程序，否则禁止此键文本进入控制台。
        /// </summary>
        /// <param name="keyInfo"></param>
        /// <returns></returns>
        private bool BlockKey(ConsoleKeyInfo keyInfo)
        {
            // 首先处理必须阻止的键。
            if (_ignore_undefined_querykeys.Contains(BuildKeyInput()))
                return true;

            // 然后，处理键盘上的符号按键，它们按 Ctrl 时可能有页数效果。
            if (keyInfo.Modifiers != 0 && keyInfo.Modifiers != ConsoleModifiers.Shift && 
                ((keyInfo.Key >= ConsoleKey.A && keyInfo.Key <= ConsoleKey.Z) ||
                 (keyInfo.Key >= ConsoleKey.Oem1 && keyInfo.Key <= ConsoleKey.Oem102) ||
                 (keyInfo.Key == ConsoleKey.OemClear)))
                return true;

            return false;
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
            // 回车键应由外部逻辑处理（以结束输入）。
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
    }
}
