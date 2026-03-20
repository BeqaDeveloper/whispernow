using System.Runtime.InteropServices;
using WhisperNow.Native;

namespace WhisperNow.Services;

internal static class InputInjectionService
{
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

        var currentFg = NativeMethods.GetForegroundWindow();
        Log.Info($"Paste: target=0x{targetWindow:X}, currentFg=0x{currentFg:X}, same={currentFg == targetWindow}");

        if (targetWindow != IntPtr.Zero && currentFg != targetWindow)
        {
            bool ok = NativeMethods.SetForegroundWindow(targetWindow);
            Log.Info($"SetForegroundWindow={ok}");
            Thread.Sleep(50);
        }

        ReleaseModifiers();
        Thread.Sleep(40);

        // Try Ctrl+V via SendInput
        uint sent = SimulateCtrlV();
        Log.Info($"SendInput returned {sent} (expected 4)");

        // If SendInput didn't inject all 4 events, fall back to keybd_event
        if (sent < 4)
        {
            Log.Info("Falling back to keybd_event");
            KeybdCtrlV();
        }
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

    private static uint SimulateCtrlV()
    {
        var inputs = new NativeMethods.INPUT[]
        {
            MakeVirtualKey(0xA2, keyUp: false),  // LCtrl down
            MakeVirtualKey(0x56, keyUp: false),  // V down
            MakeVirtualKey(0x56, keyUp: true),   // V up
            MakeVirtualKey(0xA2, keyUp: true),   // LCtrl up
        };

        return NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static void KeybdCtrlV()
    {
        NativeMethods.keybd_event(0xA2, 0, 0, UIntPtr.Zero);                 // LCtrl down
        NativeMethods.keybd_event(0x56, 0, 0, UIntPtr.Zero);                 // V down
        NativeMethods.keybd_event(0x56, 0, NativeMethods.KEYEVENTF_KU, UIntPtr.Zero);  // V up
        NativeMethods.keybd_event(0xA2, 0, NativeMethods.KEYEVENTF_KU, UIntPtr.Zero);  // LCtrl up
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
