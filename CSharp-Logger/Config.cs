using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YYHEggEgg.Logger
{
    public struct LoggerConfig
    {
        /// <summary>
        /// The maximum line length to output to the console. <para/>
        /// If the length of the line is longer than this value, it won't be output to console and will be replaced by a tip.<para/>
        /// The default value is 16KB. Set it to negative value or 0 to disable the feature.
        /// </summary>
        public readonly int Max_Output_Char_Count;
        /// <summary>
        /// Whether the Logger uses <see cref="Console"/> or <see cref="ConsoleWrapper"/> implements. <para/>
        /// Generally, <see cref="Console"/> is enough. But if you wants to read the user's input while outputing logs parallel (e.g. making a command line program), you'll need <see cref="ConsoleWrapper"/>.<para/>
        /// If you decided to use <see cref="ConsoleWrapper"/>, you can set the value of <see cref="ConsoleWrapper.InputPrefix"/> as a waiting-input prefix, just like <c>mysql> </c> or <c>ubuntu ~$ </c>, and use <see cref="ConsoleWrapper.ReadLineAsync"/> to read inputs from the user.<para/>
        /// But if your program won't output anything until get input from the user, or <b>the input string is expected to be very large</b>, you should use common <see cref="Console"/> implements.<para/>
        /// No matter you decide to use either <see cref="Console"/> or <see cref="ConsoleWrapper"/>, <b>DON'T MIX THEM!</b> The side-effect has never been tested!
        /// </summary>
        public readonly bool Use_Console_Wrapper;
        /// <summary>
        /// Whether the log files will be storaged at the working directory or the program path.<para/>
        /// The working directory is equal to <see cref="Environment.CurrentDirectory"/>. <para/>
        /// If you start the program in explorer, it will be set to where the program at. If you start it in bash/terminal, it will be set to the present path of the Console. It can have massive effects. <para/>
        /// We recommend to use program path, or <see cref="Environment.ProcessPath"/>. It will always be where the program at. <para/>
        /// e.g. If you invoke the program with command <c>PS C:\> dotnet ExampleProgram\ExampleProgram.dll</c>, <see cref="Environment.CurrentDirectory"/> will be set to <c>C:\</c>, and <see cref="Environment.ProcessPath"/> will be set to <c>C:\ExampleProgram</c>.<para/>
        /// But actually, working directory is changeable while running (as <see cref="Environment.CurrentDirectory"/> is writeable), but the log file path <b>won't change since <see cref="Log.Initialize(LoggerConfig)"/> is invoked,</b> for it maintains a <see cref="StreamWriter"/> instance until the process is terminated.<para/>
        /// Notice that Logger will still use the working directory when <see cref="Environment.ProcessPath"/> isn't accessible, or more clearly, when <see cref="Environment.ProcessPath"/><c> == null</c>. If it happens, you'll receive a warning. 
        /// </summary>
        public readonly bool Use_Working_Directory;

        /// <summary>
        /// Because of efficiency reasons, all features are defined as readonly variables and can only be set in this constructor. <para/>
        /// You may check their usages in these variables' description.
        /// </summary>
        public LoggerConfig(int max_Output_Char_Count = 16 * 1024,
                            bool use_Console_Wrapper = false,
                            // Though not recommended, previous versions use Working Directory as default,
                            // so the default value is set to true.
                            bool use_Working_Directory = true)
        {
            Max_Output_Char_Count = max_Output_Char_Count;
            Use_Console_Wrapper = use_Console_Wrapper;
            Use_Working_Directory = use_Working_Directory;
        }
    }
}
