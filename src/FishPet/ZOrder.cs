using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FishPet;

// 别的程序的窗口（包括刚被点到的普通窗口）跑到她上面时，她就被盖住了：WPF 的 Topmost 只在设置那一刻生效。
// 这里每隔一会儿看一眼：有没有别的进程的、可见的、会挡住她的窗口排在她前面，有就把她（和气泡）重新放回最上面。
// 不抢焦点（SWP_NOACTIVATE）。
//
// 2026-11-18 把上游那套排除规则并回来（原来的精简版会把她抬到开始菜单、搜索框、截图条上面）：
//   · 任务栏/开始菜单/搜索/输入法这些系统外壳，不算“压住她”；
//   · 不进任务栏的工具浮层（截图框、录制条、弹出面板）和铺满整屏的浮层，不去盖，也不跟它们抢；
//   · 最小化的窗口、被 DWM 隐藏（cloaked）的窗口，不算；
//   · 截图/录屏工具不算。
// 判断逻辑和上游 PetWindow 的 CoveredBy 一致，只是挪到了这里，KeepTopmost 也调它。
internal static class ZOrder
{
    private const int GwHwndPrev = 3, ExTopmost = 0x8, ExTransparent = 0x20;
    private const uint NoSize = 1, NoMove = 2, NoActivate = 0x10;

    // 系统外壳：开始菜单、搜索、输入法、锁屏这些盖在上面是正常的，不去抢
    private static readonly HashSet<string> ShellProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "SearchApp", "TextInputHost", "LockApp",
    };

    internal static bool CoveredByOthers(nint me)
    {
        if (me == 0 || !GetWindowRect(me, out var mine)) return false;
        var above = me;
        for (int step = 0; step < 300 && (above = GetWindow(above, GwHwndPrev)) != 0; step++)
        {
            if (!IsWindowVisible(above) || IsIconic(above)) continue;
            if (((long)GetWindowLongPtr(above, -20) & ExTransparent) != 0) continue;
            if (!GetWindowRect(above, out var r) || r.Right - r.Left < 8 || r.Bottom - r.Top < 8) continue;
            if (r.Right <= mine.Left || r.Left >= mine.Right || r.Bottom <= mine.Top || r.Top >= mine.Bottom) continue;
            // 工具浮层（截图框、录制条、弹出面板）和铺满整屏的浮层：她不去盖，也不抢着压过去
            if (Fullscreen.IsToolWindow(above) || Fullscreen.FillsMonitor(above)) continue;
            // 被 DWM 隐藏的（切走的虚拟桌面、UWP 的隐藏壳）不算
            if (DwmGetWindowAttribute(above, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) continue;
            GetWindowThreadProcessId(above, out var id);
            if ((int)id == Environment.ProcessId) continue;
            try
            {
                var name = Process.GetProcessById((int)id).ProcessName;
                if (ShellProcesses.Contains(name) || Fullscreen.IsCaptureTool(name)) continue;
                return true;
            }
            catch { /* 程序刚好退了：下一秒再看 */ }
        }
        return false;
    }

    internal static void Raise(nint window)
    {
        if (window != 0) SetWindowPos(window, -1, 0, 0, 0, 0, NoMove | NoSize | NoActivate);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out RawRect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
}
