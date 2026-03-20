using System.Runtime.InteropServices;
using WhisperNow.Native;

namespace WhisperNow.Services;

internal static class InputInjectionService
{
    /// <summary>
    /// Clears the clipboard so a stale result can't be pasted while Whisper runs.
    /// </summary>
    public static void ClearClipboard()
    {
        if (NativeMethods.OpenClipboard(IntPtr.Zero))
        {
            NativeMethods.EmptyClipboard();
            NativeMethods.CloseClipboard();
        }
    }

    public static void CopyAndPaste(string text, IntPtr targetWindow)
    {
        SetClipboardText(text);

        if (targetWindow != IntPtr.Zero)
            NativeMethods.SetForegroundWindow(targetWindow);

        ReleaseModifiers();
        Thread.Sleep(40);
        SimulateCtrlV();
    }

    private static void SetClipboardText(string text)
    {
        int bytes = (text.Length + 1) * sizeof(char);
        var hGlobal = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)bytes);
        if (hGlobal == IntPtr.Zero)
            throw new InvalidOperationException("GlobalAlloc failed");

        var locked = NativeMethods.GlobalLock(hGlobal);
        if (locked == IntPtr.Zero)
            throw new InvalidOperationException("GlobalLock failed");

        Marshal.Copy(text.ToCharArray(), 0, locked, text.Length);
        Marshal.WriteInt16(locked + text.Length * sizeof(char), 0);
        NativeMethods.GlobalUnlock(hGlobal);

        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                NativeMethods.EmptyClipboard();
                NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, hGlobal);
                NativeMethods.CloseClipboard();
                return;
            }
            Thread.Sleep(10);
        }

        throw new InvalidOperationException("Could not open clipboard after 10 attempts");
    }

    private static void SimulateCtrlV()
    {
        var inputs = new NativeMethods.INPUT[]
        {
            MakeVirtualKey(0xA2, keyUp: false),  // LCtrl down
            MakeVirtualKey(0x56, keyUp: false),  // V down
            MakeVirtualKey(0x56, keyUp: true),   // V up
            MakeVirtualKey(0xA2, keyUp: true),   // LCtrl up
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static void ReleaseModifiers()
    {
        var releases = new[]
        {
            MakeVirtualKey(NativeMethods.VK_LCONTROL, keyUp: true),
            MakeVirtualKey(NativeMethods.VK_RCONTROL, keyUp: true),
            MakeVirtualKey(NativeMethods.VK_LMENU, keyUp: true),
            MakeVirtualKey(NativeMethods.VK_RMENU, keyUp: true),
            MakeVirtualKey(NativeMethods.VK_LSHIFT, keyUp: true),
            MakeVirtualKey(NativeMethods.VK_RSHIFT, keyUp: true),
        };

        NativeMethods.SendInput((uint)releases.Length, releases, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT MakeVirtualKey(int vk, bool keyUp) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        union = new NativeMethods.INPUTUNION
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = (ushort)vk,
                dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0,
            }
        }
    };
}
