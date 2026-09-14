# NanoAtmega328-RelayControl

5-relay control over UART (Serial), with firmware for multiple boards sharing
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
R2ON / R2OFF ... R5ON / R5OFF
STATUS -> request the current status of all 5 relays
```

Replies:

```
OK:R1ON              (command executed successfully)
ERR:UNKNOWN_CMD       (invalid command)
STATUS:10101           (5-character 0/1 string, relay 1..5, 1=ON, 0=OFF)
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

### Building

```
pio run -d firmware-nanoatmega328
pio run -d firmware-esp32c3-supermini
```

## App

[app/RelayControlWPF/](app/RelayControlWPF/) — Windows desktop app for
sending relay commands over Serial.
