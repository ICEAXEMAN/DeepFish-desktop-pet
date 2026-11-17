using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FishPet;

// 到点叫人（本机加，2026-11-08）：按这台电脑自己的时钟，不靠哪个 AI 的定时任务，谁拿到插件都一样到点就来。
//   睡觉提醒：起点（默认 00:30）第一层（温柔），+10 分钟第二层（严肃），+15 分钟第三层（生气「时间到了」），+17 分钟第四层（黑脸逐字质问）
//   午饭提醒：默认 11:30
// 时间可调（2026-11-18）：设置里 SleepAt 在 22:00–02:00、LunchAt 在 11:00–13:30，都按 15 分钟一档（托盘“更多”里有滑块）。
// 设置里 CoaxSleep / CoaxLunch 分别开关（托盘“更多”里有开关）。台词在插件根目录 alarms.txt，每次随机挑一条，不调模型、不花 token；
// 台词里写 {时间} 会换成这一层到点的时间（比如「十二点半」）。
// 只在到点后 2 分钟内补演：电脑睡着、桌宠没开时错过的就算了，不会一开机连演一串。
internal static class Alarms
{
    internal sealed record Slot(int Hour, int Minute, string Name, int Stage, bool Sleep);

    // 可调的范围：都按“一天里的第几分钟”算；睡觉的区间跨过午夜，从 22:00 往后数 0–240 分钟
    internal const int Step = 15;
    internal const int SleepFrom = 22 * 60, SleepSpan = 4 * 60;
    internal const int LunchFrom = 11 * 60, LunchTo = 13 * 60 + 30;
    internal const string DefaultSleepAt = "00:30", DefaultLunchAt = "11:30";

    // 第一层之后各层晚几分钟
    private static readonly (int After, string Name, int Stage)[] SleepLayers = [(0, "哄睡1", 1), (10, "哄睡2", 2), (15, "哄睡3", 3), (17, "哄睡4", 4)];

    internal static Slot[] Schedule(string sleepAt, string lunchAt)
    {
        int sleep = Minutes(SleepTime(sleepAt) ?? DefaultSleepAt), lunch = Minutes(LunchTime(lunchAt) ?? DefaultLunchAt);
        var slots = SleepLayers.Select(layer => { int m = (sleep + layer.After) % 1440; return new Slot(m / 60, m % 60, layer.Name, layer.Stage, true); }).ToList();
        slots.Add(new Slot(lunch / 60, lunch % 60, "午饭", 1, false));
        return [.. slots];
    }

    // 默认时间表（名字齐全，测试和台词检查用）
    internal static readonly Slot[] Slots = Schedule(DefaultSleepAt, DefaultLunchAt);

    internal static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

    // 上次看到这次之间，有哪些点到了（只算 2 分钟内的）。
    internal static List<Slot> Due(DateTime last, DateTime now, bool sleep, bool lunch, string sleepAt = DefaultSleepAt, string lunchAt = DefaultLunchAt)
    {
        var due = new List<Slot>();
        if (now <= last) return due;
        foreach (var slot in Schedule(sleepAt, lunchAt))
        {
            if (slot.Sleep ? !sleep : !lunch) continue;
            // 今天和昨天的都看：23:59 的点，过了午夜的 2 分钟宽限里也要补上
            foreach (var day in new[] { now.Date, now.Date.AddDays(-1) })
            {
                var at = day.AddHours(slot.Hour).AddMinutes(slot.Minute);
                if (at > last && at <= now && now - at < Grace) { due.Add(slot); break; }
            }
        }
        return due;
    }

    // "0:30"、"00:30"、"24:00" 这类 → 一天里的第几分钟；读不出来是 null
    internal static int? ParseTime(string? text)
    {
        var parts = (text ?? "").Trim().Replace('：', ':').Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m) || h is < 0 or > 24 || m is < 0 or > 59) return null;
        return (h * 60 + m) % 1440;
    }

    private static int Minutes(string hhmm) => ParseTime(hhmm) ?? 0;
    internal static string Format(int minutes) { minutes = ((minutes % 1440) + 1440) % 1440; return $"{minutes / 60:00}:{minutes % 60:00}"; }

    // 睡觉提醒的起点：对齐到 15 分钟，必须在 22:00–02:00 里；不在范围里是 null
    internal static string? SleepTime(string? text)
    {
        if (ParseTime(text) is not int m) return null;
        int offset = (m - SleepFrom + 1440) % 1440;
        offset = (int)Math.Round(offset / (double)Step, MidpointRounding.AwayFromZero) * Step;
        return offset <= SleepSpan ? Format(SleepFrom + offset) : null;
    }

    // 午饭提醒：对齐到 15 分钟，必须在 11:00–13:30 里；不在范围里是 null
    internal static string? LunchTime(string? text)
    {
        if (ParseTime(text) is not int m) return null;
        m = (int)Math.Round(m / (double)Step, MidpointRounding.AwayFromZero) * Step;
        return m is >= LunchFrom and <= LunchTo ? Format(m) : null;
    }

    // 台词里的 {时间}：00:30 →「十二点半」，11:45 →「十一点四十五」，13:00 →「一点」，22:15 →「十点十五」
    internal static string Spoken(int hour, int minute)
    {
        int h = hour % 12 == 0 ? 12 : hour % 12;
        string hh = h == 2 ? "两" : Number(h);
        string mm = minute switch { 0 => "", 30 => "半", < 10 => "零" + Number(minute), _ => Number(minute) };
        return hh + "点" + mm;
    }

    private static string Number(int n)
    {
        string[] d = ["零", "一", "二", "三", "四", "五", "六", "七", "八", "九"];
        if (n < 10) return d[n];
        if (n < 20) return "十" + (n % 10 == 0 ? "" : d[n % 10]);
        return d[n / 10] + "十" + (n % 10 == 0 ? "" : d[n % 10]);
    }

    // alarms.txt：「# 名字」开头一段，之后每行一条候选台词；「／」隔开的是一个个气泡，〔〕写动作。// 开头的是注释。
    internal static Dictionary<string, List<string>> Parse(IEnumerable<string> lines)
    {
        var all = new Dictionary<string, List<string>>();
        List<string>? current = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;
            if (line.StartsWith('#')) { var name = line[1..].Trim(); current = all.TryGetValue(name, out var have) ? have : all[name] = []; continue; }
            current?.Add(line);
        }
        return all;
    }

    // 文件丢了也有话说
    internal static readonly Dictionary<string, string> Fallback = new()
    {
        ["哄睡1"] = "〔前倾 八字眉 看过来〕这么晚了还不睡呀……／〔抚胸 微笑〕早点休息，明天再做也来得及",
        ["哄睡2"] = "〔皱眉 正视〕都十分钟了，怎么还没去睡／〔竖手指 微摇头〕熬夜伤身体，我是认真的",
        ["哄睡3"] = "〔皱眉 鼓腮〕时间到了！／〔竖手指〕现在、立刻、马上去睡觉！",
        ["哄睡4"] = "为什么还没睡？",
        ["午饭"] = "〔前倾 竖手指 微笑〕{时间}啦，该吃午饭咯／〔放下手 眨单眼〕先吃饱，再接着忙",
    };
}

