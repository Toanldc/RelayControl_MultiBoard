# RelayControl MultiBoard

8-relay control over UART (Serial) and BLE, with firmware for multiple
boards sharing the same command protocol, plus a Windows desktop app to
drive it over either transport.

## Firmware

Each supported board is its own PlatformIO project so they can be opened,
built, and flashed independently, while sharing the same `main.cpp` logic
and command protocol. The only differences between projects are the
board/platform configuration in `platformio.ini` and the relay `RELAY_PINS`
mapping in `src/main.cpp` (GPIO numbering differs per board).

| Board                      | Project folder                                               | Platform      | Transports    |
|-----------------------------|---------------------------------------------------------------|---------------|---------------|
| Arduino Nano (ATmega328P)  | [firmware-nanoatmega328/](firmware-nanoatmega328/)            | `atmelavr`    | UART only (no BLE radio on this MCU) |
| ESP32-C3 SuperMini         | [firmware-esp32c3-supermini/](firmware-esp32c3-supermini/)    | `espressif32` | UART + BLE (simultaneously) |

### Command protocol (terminated with `\n`, identical over UART and BLE)

```
R1ON   -> turn relay 1 ON
R1OFF  -> turn relay 1 OFF
R2ON / R2OFF ... R8ON / R8OFF
STATUS -> request the current status of all 8 relays
```

Replies (sent back over whichever transport the command arrived on):

```
OK:R1ON              (command executed successfully)
ERR:UNKNOWN_CMD       (invalid command)
STATUS:10101010         (8-character 0/1 string, relay 1..8, 1=ON, 0=OFF)
```

### BLE (ESP32-C3 only)

Exposes a Nordic UART Service (NUS)-compatible GATT profile carrying the
same text protocol above as newline-terminated writes/notifications, so any
generic BLE-UART terminal app can talk to it too, not just the desktop app.

| | |
|---|---|
| Device name  | `RelayControl-ESP32C3` |
| Service UUID | `6E400001-B5A3-F393-E0A9-E50E24DCCA9E` |
| RX (Write, app → board)  | `6E400002-B5A3-F393-E0A9-E50E24DCCA9E` |
| TX (Notify, board → app) | `6E400003-B5A3-F393-E0A9-E50E24DCCA9E` |

The onboard WS2812 RGB LED (GPIO8) shows BLE connection state: blinking red
while no BLE central is connected, solid green once one connects. It does
not reflect UART, which has no protocol-level "connected" signal to show.

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
5. BLE only works if the target MCU actually has a BLE radio (the Nano's
   ATmega328P does not) — for a BLE-capable MCU, port the BLE setup/
   callbacks from `firmware-esp32c3-supermini/src/main.cpp` too; otherwise
   leave the board UART-only.

## App

[app/RelayControlWPF/](app/RelayControlWPF/) — Windows desktop app for
sending relay commands over UART or BLE. A mode switch at the top picks the
transport: UART lists COM ports, BLE scans for boards advertising the NUS
service above and connects by address (no OS-level pairing needed). It also
supports user-defined **Presets** (own tab) — named, ordered relay sequences with
per-step delays — run with one click; see
[app/RelayControlWPF/README.md](app/RelayControlWPF/README.md#presets) for
details.

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

Run `build/release.bat`. It prompts for a version string (e.g. `1.0.0`),
rebuilds every firmware and the app from source via `build-all.bat` (so a
release never reuses stale `out/` files), then zips each `out/` target into
`release/<version>/`. If any build fails, no release folder is created:

```
release/1.0.0/
├── firmware-nanoatmega328.zip
├── firmware-esp32c3-supermini.zip
└── RelayControlWPF.zip
```

Unlike `out/`, `release/` is committed to the repo, so each version's build
artifacts stay available directly from a clone.
