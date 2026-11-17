using System;
using System.Threading;
using System.Threading.Tasks;

namespace FishPet;

// 第一次真正出场时说欢迎（台词和规则见 Welcome.cs、welcome.txt / welcome_calm.txt）。
// 每句先看语音包里有没有这条录音（W-0x.wav），有就放、气泡跟着录音走；没有就只冒气泡。
// 2026-11-18 合上游：每段说完等够（「|秒数」）再换下一段；开头「@道具」只在说这一段时戴上。
internal sealed partial class PetWindow
{
    private bool welcoming;

    // 她露面的时候调一下：还没欢迎过、不是自检用的看不见的桌宠、也不是收着待命，才会说。
    // Ready()（程序一开就出场）和 Wake()（本来收着、被叫出来）都会调到这里。
    private void WelcomeSoon()
    {
        if (settings.Welcomed || test || standby || welcoming) return;
        welcoming = true;
        _ = WelcomeAsync();
    }

    private async Task WelcomeAsync()
    {
        try
        {
            await Task.Delay(1800); // 让出场的问候动作先演完
            // 全屏的东西盖着、正在收尾、还没画出来：等到她真的看得见（最多一分钟）。等不到就算了，下次出场再说，不记“说过了”。
            for (int wait = 0; wait < 120 && (standby || hiding || hiddenForFullscreen || !ready) && !exiting; wait++) await Task.Delay(500);
            if (exiting || standby || hiding || hiddenForFullscreen || !ready) return;
            settings.Welcomed = true;
            settings.Save();
            var text = Welcome.Text(Places.Root, settings.VoicePack);
            var bubbles = Welcome.Bubbles(text);
            Places.Log($"第一次出场：说欢迎（{settings.VoicePack}，{Welcome.FileFor(settings.VoicePack)}，{bubbles.Length} 句）");
            for (int i = 0; i < bubbles.Length && !exiting; i++)
            {
                var bubble = bubbles[i];
                var (piece, seconds, prop) = Welcome.Piece(bubble);
                // 一段一句来：先放录音（气泡跟着录音走），这一段演完再停够时间换下一句。
                // 查录音要用去掉〔动作〕的正文，index.json 的键就是那个。
                var voiceSeconds = Speak(Tts.Plain(piece));
                await InTurn(() => { if (!exiting) SayLine(piece, voiceSeconds); return Task.CompletedTask; }, CancellationToken.None);
                // 这一段要戴的临时道具（比如第六段的黑框眼镜）：下一段一开口就被摘掉
                if (prop is { Length: > 0 }) PutOn([prop], Math.Max(seconds, 6) + 4);
                if (exiting) return;
                // 先等她把这一段说完，再开始停。不这样的话这个停和她说话同时跑，
                // 说完就已经停了，等于没停（2026-11-18 修）。
                while (speech.IsSaying && !exiting) await Task.Delay(50);
                if (exiting) return;
                double wait;
                if (seconds > 0) wait = seconds;
                else if (settings.BubbleSeconds == -1) wait = 20;
                else if (settings.BubbleSeconds > 0) wait = settings.BubbleSeconds;
                else wait = Speech.StaysFor(Welcome.Plain(piece)).TotalSeconds;
                if (wait > 0) await Task.Delay(TimeSpan.FromSeconds(wait));
            }
        }
        finally { welcoming = false; }
    }
}
