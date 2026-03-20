# WhisperNow

Lightweight Windows background app that captures system audio while you hold a hotkey, transcribes it offline with Whisper, and pastes raw English text into the active window.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or later)
- An active audio output device (speakers / headphones)

## Quick Start

```powershell
# 1. Clone the repo
git clone https://github.com/BeqaDeveloper/whispernow.git
cd whispernow

# 2. Download the Whisper model (~460 MB, one-time)
.\download-model.ps1

# 3. Build & run
dotnet run
```

The model can also be downloaded automatically on first launch from Hugging Face.

## Usage

| Action | What happens |
|---|---|
| **Hold** Left Ctrl + Left Alt | Starts capturing system audio |
| **Release** either key | Stops capture, transcribes, copies to clipboard and auto-pastes into the focused window |

- Only **system audio** (speakers/headphones) is captured — your microphone is ignored.
- Text is always placed on the clipboard, so you can also paste manually with Ctrl+V.
- The app lives in the system tray. Right-click the tray icon to **Exit**.

## Architecture

```
Program.cs                 → entry point, model init
WhisperNowApp.cs           → tray icon, event wiring
Log.cs                     → file logger
Services/
  HotkeyService.cs         → low-level keyboard hook (LCtrl+LAlt hold/release)
  AudioCaptureService.cs   → WASAPI loopback via NAudio, resamples to 16 kHz mono
  TranscriptionService.cs  → Whisper.net (whisper.cpp) with ggml-small.en
  InputInjectionService.cs → Clipboard + SendInput Ctrl+V
Native/
  NativeMethods.cs         → Win32 P/Invoke declarations
```

## Key Dependencies

| Package | Purpose |
|---|---|
| NAudio 2.3 | WASAPI loopback audio capture |
| Whisper.net 1.9 | .NET bindings for whisper.cpp |
| Whisper.net.Runtime 1.9 | CPU inference runtime |

## Notes

- **No AI post-processing** — output is raw detected English words.
- Uses `ggml-small.en` model with a technical vocabulary prompt for better recognition of programming terms (SQL, SOLID, .NET, etc.).
- The hotkey (LCtrl + LAlt) is detected via a low-level keyboard hook. Modify `HotkeyService.cs` to change it.
- Recordings shorter than ~0.5s are silently discarded.
