Build a lightweight Windows background application using .NET 8 (C#) that performs push-to-talk transcription of system audio and pastes raw English text into the active window.

Core behavior:

1. HOTKEY (Push-to-Talk)

* Register a global hotkey (e.g. Ctrl+Alt)
* While the hotkey is held:

  * Start capturing system audio
  * Start transcription
* When released:

  * Stop capture
  * Paste final text into active window

2. AUDIO CAPTURE

* Use NAudio with WASAPI loopback
* Capture system audio (YouTube, meetings, etc.)
* Convert audio to 16kHz mono PCM for processing

3. SPEECH-TO-TEXT

* Use a lightweight offline engine:
  Option A: Whisper.cpp (tiny.en model)
  Option B: Vosk (preferred for speed)
* Stream audio chunks for near real-time transcription
* Output raw English text only (no formatting, no punctuation correction)

4. TEXT OUTPUT

* Collect transcription while hotkey is held
* On release:

  * Combine text into a single sentence
  * Inject into active window using Windows SendInput API

5. PERFORMANCE

* Low latency priority (<300–500ms)
* Minimal CPU usage
* No UI required (optional tray icon)

6. ARCHITECTURE

* AudioService (NAudio loopback capture)
* TranscriptionService (Whisper or Vosk)
* HotkeyService
* InputInjectionService

7. SIMPLICITY RULES

* No AI post-processing
* No summaries
* No formatting improvements
* Just raw detected English words

Deliver a working solution with clear setup instructions and dependencies.
