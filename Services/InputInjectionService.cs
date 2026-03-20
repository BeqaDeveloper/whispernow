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

        if (targetWindow != IntPtr.Zero && NativeMethods.GetForegroundWindow() != targetWindow)
            NativeMethods.SetForegroundWindow(targetWindow);

        ReleaseModifiers();
        Thread.Sleep(30);

        NativeMethods.keybd_event(0xA2, 0, 0, UIntPtr.Zero);                                // LCtrl down
        NativeMethods.keybd_event(0x56, 0, 0, UIntPtr.Zero);                                // V down
        NativeMethods.keybd_event(0x56, 0, NativeMethods.KEYEVENTF_KU, UIntPtr.Zero);       // V up
        NativeMethods.keybd_event(0xA2, 0, NativeMethods.KEYEVENTF_KU, UIntPtr.Zero);       // LCtrl up
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

    private static void ReleaseModifiers()
    {
        NativeMethods.keybd_event(0xA2, 0, NativeMethods.KEYEVENTF_KU, UIntPtr.Zero);  // LCtrl
        NativeMethods.keybd_event(0xA3, 0, NativeMethods.KEYEVENTF_KU, UIntPtr.Zero);  // RCtrl
        NativeMethods.keybd_event(0xA4, 0, NativeMethods.KEYEVENTF_KU, UIntPtr.Zero);  // LAlt
        NativeMethods.keybd_event(0xA5, 0, NativeMethods.KEYEVENTF_KU, UIntPtr.Zero);  // RAlt
        NativeMethods.keybd_event(0xA0, 0, NativeMethods.KEYEVENTF_KU, UIntPtr.Zero);  // LShift
        NativeMethods.keybd_event(0xA1, 0, NativeMethods.KEYEVENTF_KU, UIntPtr.Zero);  // RShift
    }
}
