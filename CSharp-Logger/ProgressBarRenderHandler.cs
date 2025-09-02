using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YYHEggEgg.Logger.Utils;

namespace YYHEggEgg.Logger;

/// <summary>
/// Provides a interface of <see cref="ConsoleWrapper"/> to persist content at a dedicated area at the bottom of Console.
/// </summary>
public abstract class PersistAreaRenderHandlerBase
{
    /// <summary>
    /// The minimum interval to invoke <see cref="Render()"/> (however not guaranteed to be fixed as this value).
    /// </summary>
    public abstract TimeSpan CallbackInterval { get; }
    /// <summary>
    /// 'Render' something that you wishes to persist at a dedicated area at the bottom of Console. Supports XML-like <c>color=</c> grammar as well as <see cref="Log"/>.
    /// </summary>
    /// <returns></returns>
    public abstract string Render();
}

public class ProgressBarRenderResult
{
    /// <summary>
    /// The description of the progress bar, rendered at the beginning of the line.
    /// </summary>
    public string? Topic { get; set; }
    /// <summary>
    /// The progress of the operation, between <c>0</c> and <c>1</c>.
    /// This will be used to render a percentage value and the virtual progress bar.
    /// </summary>
    public double Progress { get; set; }
}

/// <summary>
/// Provides an implementation of <see cref="PersistAreaRenderHandlerBase"/> while using it as a progress bar.
/// </summary>
public abstract class ProgressBarRenderHandlerBase : PersistAreaRenderHandlerBase
{
    /// <summary>
    /// The color you wish the dedicated line to be. Set it to <see langword="null"/> to specify you want default color (however tags in  still takes effect).
    /// </summary>
    public virtual ConsoleColor? Color => ConsoleColor.Green;
    /// <summary>
    /// The characters count of the virtual progress bar. Default is 50.
    /// </summary>
    public virtual int ProgressBarBlocks => 50;
    /// <summary>
    /// Render a progress bar. Result will be rendered as the following format:<para/>
    /// <see cref="ProgressBarRenderResult.Topic"/> <c>[Percentage]%</c> <c>[A bar depending on <see cref="ProgressBarRenderResult.Progress"/>]</c><para/>
    /// For example: <c>Downloading 4/10 Files 61.67%[==========>         ]</c>
    /// </summary>
    /// <returns>The progress bar(s) you want to show. Multiple bars will be rendered if multiple results are provided.</returns>
    protected abstract List<ProgressBarRenderResult> RenderProgressBar();

    public override string Render()
    {
        StringBuilder result = new();
        bool first = true;
        foreach (var rendered in RenderProgressBar())
        {
            if (first) first = false;
            else result.AppendLine();
            var progress = rendered.Progress * 100;
            var blocks = (int)progress / (100 / ProgressBarBlocks);
            if (blocks < 1) blocks = 1;
            else if (blocks > ProgressBarBlocks) blocks = ProgressBarBlocks;
            var bar = string.Format($"{{0,-{ProgressBarBlocks}}}", new string('=', blocks - 1) + '>');
            result.Append($"{rendered.Topic} {progress:F2}%[{bar}]");
        }
        if (result.Length == 0) return string.Empty;
        var color = Color;
        if (color != null)
        {
            result.Insert(0, $"<color={Color}>");
            result.Append("</color>");
        }
        return result.ToString();
    }
}
