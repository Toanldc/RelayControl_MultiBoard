# NanoAtmega328-RelayControl

8-relay control over UART (Serial), with firmware for multiple boards sharing
the same command protocol, plus a Windows desktop app to drive it.

## Firmware

Each supported board is its own PlatformIO project so they can be opened,
built, and flashed independently, while sharing the same `main.cpp` logic
and command protocol. The only differences between projects are the
board/platform configuration in `platformio.ini` and the relay `RELAY_PINS`
mapping in `src/main.cpp` (GPIO numbering differs per board).

| Board                | Project folder                 | Platform      |
|-----------------------|--------------------------------|---------------|
| Arduino Nano (ATmega328P) | [firmware-nanoatmega328/](firmware-nanoatmega328/) | `atmelavr` |
| ESP32-C3 SuperMini    | [firmware-esp32c3-supermini/](firmware-esp32c3-supermini/) | `espressif32` |

### Command protocol (Serial, terminated with `\n`)

```
R1ON   -> turn relay 1 ON
R1OFF  -> turn relay 1 OFF
R2ON / R2OFF ... R8ON / R8OFF
STATUS -> request the current status of all 8 relays
```

Replies:

```
OK:R1ON              (command executed successfully)
ERR:UNKNOWN_CMD       (invalid command)
STATUS:10101010         (8-character 0/1 string, relay 1..8, 1=ON, 0=OFF)
```

### Adding a new board

To port the firmware to another board:

1. Copy an existing `firmware-*/` folder to a new `firmware-<board>/` folder.
2. Update `platformio.ini`: set `platform`/`board` for the new MCU (and any
   board-specific `build_flags`, e.g. USB CDC settings for native-USB
   boards).
3. In `src/main.cpp`, update `RELAY_PINS` to GPIOs that are safe/available
   on the new board (avoid strapping, USB, or otherwise reserved pins).
4. Leave the rest of `main.cpp` untouched — it only uses portable Arduino
   framework APIs (`Serial`, `digitalWrite`, `pinMode`, `String`).

## App

[app/RelayControlWPF/](app/RelayControlWPF/) — Windows desktop app for
sending relay commands over Serial.

## Build & Release

[build/](build/) contains `.bat` scripts that build every firmware and the
app in one step, without needing `pio` or `dotnet` on PATH manually. Run
from anywhere — no need to `cd` into a project folder first.

| Script | Builds |
|---|---|
| `build/build-nano.bat` | `firmware-nanoatmega328` |
| `build/build-esp32.bat` | `firmware-esp32c3-supermini` |
| `build/build-app.bat` | `app/RelayControlWPF` (published, win-x64) |
| `build/build-all.bat` | all of the above, in order |

Each build copies its output into `out/` (gitignored — regenerated every
build, not tracked):

```
out/
├── firmware-nanoatmega328/      firmware.hex, firmware.elf
├── firmware-esp32c3-supermini/  firmware.bin, bootloader.bin, partitions.bin, firmware.elf
└── app/                         RelayControlWPF.exe + runtime files
```

### Cutting a release

Run `build/build-all.bat` first, then `build/release.bat`. It prompts for a
version string (e.g. `1.0.0`) and zips each `out/` target into
`release/<version>/`:

```
release/1.0.0/
├── firmware-nanoatmega328.zip
├── firmware-esp32c3-supermini.zip
└── RelayControlWPF.zip
```

Unlike `out/`, `release/` is committed to the repo, so each version's build
artifacts stay available directly from a clone.
