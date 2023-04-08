using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YYHEggEgg.Logger
{
    public class LoggerConfig
    {
        /// <summary>
        /// The maximum line length to output to the console. <para/>
        /// If the length of the line is longer than this value, it won't be output to console and will be replaced by a tip.<para/>
        /// The default value is 16KB. Set it to negative value or 0 to disable the feature.
        /// </summary>
        public readonly int Max_Output_Char_Count;
        /// <summary>
        /// If the Logger uses <see cref="Console"/> or <see cref="ConsoleWrapper"/> implements. <para/>
        /// Generally, <see cref="Console"/> is enough. But if you wants to read the user's input while outputing logs parallel (e.g. making a command line program), you'll need <see cref="ConsoleWrapper"/>.<para/>
        /// If you decided to use <see cref="ConsoleWrapper"/>, you can set the value of <see cref="ConsoleWrapper.InputPrefix"/> as a waiting-input prefix, just like <c>mysql> </c> or <c>ubuntu ~$ </c>, and use <see cref="ConsoleWrapper.ReadLineAsync"/> to read inputs from the user.<para/>
        /// Or if you want to get your output with color, you may use <see cref="ConsoleWrapper"/> implement. The usage is described at <see cref="ConsoleWrapper.WriteLine(string)"/>.<para/>
        /// But if your program won't output anything until get input from the user, or <b>the input string is expected to be very large</b>, you should use common <see cref="Console"/> implements.<para/>
        /// No matter you decide to use either <see cref="Console"/> or <see cref="ConsoleWrapper"/>, <b>DON'T MIX THEM!</b> The side-effect has never been tested!
        /// </summary>
        public readonly bool Use_Console_Wrapper;

        /// <summary>
        /// Because of efficiency reasons, all features are defined as readonly variables and can only be set in this constructor. <para/>
        /// You may check their usages in these variables' description.
        /// </summary>
        public LoggerConfig(int max_Output_Char_Count = 16 * 1024, bool use_Console_Wrapper = false)
        {
            Max_Output_Char_Count = max_Output_Char_Count;
            Use_Console_Wrapper = use_Console_Wrapper;
        }
    }
}
