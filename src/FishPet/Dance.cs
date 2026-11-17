using System;

namespace FishPet;

// 音乐律动的「拍子换算」（本机加，2026-11-10）：BPM 决定一拍多长，舞步就走多快。
//
// 2026-11-12 用户定了：放歌时跳「摇摆一」（身子左右转、头反着甩，见 Body.cs 的 Dance），
// 这里负责算「这一句该演几秒」：BPM 越高一句越短，律动就跟着快；BPM 越低一句越长。
internal static class Dance
{
    // 一句一共走这么多拍（一拍 = 60/BPM 秒）：摇摆一 12 拍、摇摆二 6 拍、慢摇 3 拍。
    internal static int Beats(string word) => word switch
    {
        "摇摆一" => 12,
        "摇摆二" => 6,
        "慢摇" => 3,
        _ => 6,
    };

    // 一拍多长（秒）：BPM 越高越短。40~190 BPM 之外按边界走，快歌别把她甩飞。
    internal static double BeatSeconds(double bpm) => Math.Clamp(60 / Math.Clamp(bpm, 40, 190), .32, 1.5);

    // 摇摆强度（2026-11-18）：soft 柔和（默认，跟以前一样）、normal 标准、strong 激烈。
    //   Size 是幅度（原动作的几倍），Stretch 是一句拉长几倍（2 = 放慢一半）。
    //   节拍检测只认到 180~190 BPM，而且 200 多拍的快歌常被听成一半，所以快档靠少放慢一些来提速：标准 1.6 倍、激烈 1.2 倍（柔和是 2 倍，用户 2026-11-18 定）。
    internal static readonly string[] Strengths = ["soft", "normal", "strong"];
    internal static string NormalStrength(string? level) => Array.IndexOf(Strengths, level) >= 0 ? level! : "soft";
    internal static (double Size, double Stretch) Strength(string? level) => NormalStrength(level) switch
    {
        "strong" => (.5, 1.2),
        "normal" => (.45, 1.6),
        _ => (.4, 2),
    };

    // 这一句该演多久（秒）= 拍数 × 一拍的长度。120 BPM 时：摇摆一 6 秒（PetWindow.Dance 再乘 2）。
    internal static double Seconds(string word, double bpm) => Beats(word) * BeatSeconds(bpm);
}
