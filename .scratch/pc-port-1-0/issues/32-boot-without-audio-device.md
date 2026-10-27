# 32: Boot without an audio device

**What to build:** The game boots and plays when the PC has no audio output device. Without one, the audio is silent and the log says why. Today two runs with no audio endpoint present logged `SDL_OpenAudioDeviceStream() failed: No default audio device available` and stopped at about frame 280, after `startup.xzp`, until the scene runner killed them. Both runs had `--audio_mute=true`. A run minutes earlier, with Realtek HD Audio present, went past the same point. So the missing device is the likely cause, but this is not yet proven. Evidence: `analysis/scenes/iq09/native-practice-scale2-noaudio-stall/`, `native-practice-scale2/` (image-quality ticket 09).

**Blocked by:** None

**Status:** needs-triage

- [ ] The hang is reproduced with no audio endpoint (e.g. the endpoints disabled) and its cause found
- [ ] With no device, the game reaches the main menu and a match, silent, and the log says audio is off and why
- [ ] With `--audio_mute=true`, no audio device is needed at all
- [ ] The smoke run passes with a device and without one

## Comments

- 2026-10-27 (Claude): opened from image-quality ticket 09's finding. The machine lost every audio endpoint after three Edge runs (`Get-PnpDevice -Class AudioEndpoint`: all not present; Audiosrv running).
