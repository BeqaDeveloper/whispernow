using System.Runtime.InteropServices;
using WhisperNow.Native;

namespace WhisperNow.Services;

internal sealed class HotkeyService : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _hookProc;
    private bool _leftCtrlDown;
    private bool _leftAltDown;
    private bool _isActive;

    public event Action? Activated;
    public event Action? Deactivated;

    public void Start()
    {
        _hookProc = HookCallback;

        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(null),
            0);

        if (_hookId == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            Log.Error($"SetWindowsHookEx failed with error {error}");
            throw new InvalidOperationException(
                $"Failed to install keyboard hook (error {error}).");
        }

        Log.Info($"Keyboard hook installed (handle={_hookId})");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            // Ignore synthetic keys injected by our own SendInput
            if ((hookStruct.flags & NativeMethods.LLKHF_INJECTED) != 0)
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

            int msg = wParam.ToInt32();
            bool isDown = msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
            bool isUp = msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;

            switch (hookStruct.vkCode)
            {
                case NativeMethods.VK_LCONTROL:
                    if (isDown) _leftCtrlDown = true;
                    else if (isUp) _leftCtrlDown = false;
                    break;
                case NativeMethods.VK_LMENU:
                    if (isDown) _leftAltDown = true;
                    else if (isUp) _leftAltDown = false;
                    break;
            }

            bool bothDown = _leftCtrlDown && _leftAltDown;

            if (bothDown && !_isActive)
            {
                _isActive = true;
                Log.Info(">>> ACTIVATED");
                Activated?.Invoke();
            }
            else if (!bothDown && _isActive)
            {
                _isActive = false;
                Log.Info("<<< DEACTIVATED");
                Deactivated?.Invoke();
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}
