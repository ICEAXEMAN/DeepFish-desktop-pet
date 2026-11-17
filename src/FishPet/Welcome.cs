using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FishPet;

// 第一次真正出场时她自己说的几句欢迎（2026-11-13）：不经过模型、不花 token，每个用户第一次看到的都一样。
// 台词在插件根目录的 welcome.txt：「# 欢迎」下面写一条，「／」隔开的是一个个气泡，〔〕写动作，// 开头的是注释。
// 只说一次：说完在 settings.json 里记 Welcomed。已经有 settings.json 的老用户不会补说（见 Settings.Load）。
internal static class Welcome
{
    internal const string Name = "欢迎";

    // welcome.txt 丢了、或者里面没写，也有话说
    internal const string Fallback =
        "〔看过来 微笑〕嗨，老大！我是大肥鱼，〔微点头〕从今天起就住在你的桌面上了。|2／" +
        "〔竖手指〕我的控制面板一般是在任务栏右下角，〔看下面 歪一下头〕图标是一只小虎鲸；没看见就点开“^”把它拖出来。|2／" +
        "〔放下手 微笑〕关于控制面板要不要常驻在任务栏上，设置里能换。|2／" +
        "〔看过来 微笑〕如果你电脑上所有的 Agent 工具关掉了，我过几秒会自己离开——〔微摇头〕这是正常的，不是坏了。|2／" +
        "〔竖手指〕想让我先躲起来：在 Agent 的聊天里说“收起来”或者“退下”都行，〔放下手 歪一下头:右〕也可以点控制面板顶上的按钮。|2／" +
        "@黑框眼镜〔站直 微笑〕我还能陪你干活：你忙的时候我在旁边报进度，〔竖手指〕做到第几步、接下来做什么；〔放下手 笑眼〕一件活干完，再说一句简报。|2／" +
        "〔眯眼笑 晃头〕电脑里放音乐的时候，我会跟着节奏轻轻摇——〔看过来 微笑〕音乐软件和浏览器放出来的都听得出来。|2／" +
        "〔竖手指〕想调体型、大小、提醒时间这些，控制面板里都有，〔放下手 眨单眼〕更多设置里藏着更多东西，可以慢慢看看哦~|2／" +
        "〔抚胸 眯眼笑 微笑 低头 前倾〕所以，很高兴与你相遇。〔站直 正视 放下手 笑眼 看过来〕今天想一起做些什么〔慢歪一下头 慢单左眨眼 默认眼〕吗？";

    // 取 welcome.txt 里「# 欢迎」下面的第一条；没有就用内置的
    internal static string Text(IEnumerable<string>? fileLines)
    {
        if (fileLines != null && Alarms.Parse(fileLines).TryGetValue(Name, out var lines))
            foreach (var line in lines)
                if (Bubbles(line).Length > 0) return line;
        return Fallback;
    }

    // 新装才欢迎：启动时没有 settings.json 就是新装；有（哪怕里面没有 Welcomed 这一项）就是老用户，不补说。
    internal static bool Pending(bool settingsFileExisted) => !settingsFileExisted;

    internal static string[] Bubbles(string text) =>
        text.Split('／', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // 一段欢迎可以带两样额外的东西（用户 2026-11-18 要的）：
    //   开头写「@道具名」——只在说这一段时戴上，下一段开口就摘（比如第六段戴黑框眼镜）；
    //   结尾写「|秒数」——这一段说完之后停几秒再换下一段，1～120；不写就按全局的「气泡停留」算。
    internal static (string Text, int Seconds, string? Prop) Piece(string bubble)
    {
        var rest = (bubble ?? "").Trim();
        string? prop = null;
        if (rest.StartsWith('@'))
        {
            int end = 1;
            while (end < rest.Length && rest[end] is not ('〔' or '|')) end++;
            prop = rest[1..end].Trim();
            rest = rest[end..].Trim();
            if (prop.Length == 0) prop = null;
        }
        int seconds = 0;
        int bar = rest.LastIndexOf('|');
        if (bar > 0 && int.TryParse(rest[(bar + 1)..].Trim(), out var parsed) && parsed is >= 1 and <= 120)
        {
            seconds = parsed;
            rest = rest[..bar].Trim();
        }
        return (rest, seconds, prop);
    }

    // 这段话真正说出来的字（去掉〔〕里的动作），算停留时间用
    internal static string Plain(string text) => Regex.Replace(text ?? "", "〔[^〕]*〕", "");
}
