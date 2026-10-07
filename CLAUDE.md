# CLAUDE.md

- All `firmware-*/src/main.cpp` files must stay protocol-identical: `R<n>ON`/`R<n>OFF`, `STATUS`, same reply formats. Only `RELAY_PINS` (and the pin-choice comment) may differ per board.
- Relay count lives in one constant (`RELAY_COUNT` in firmware, `RelayCount` in the WPF app) — every loop, array size, and STATUS string length must derive from it, never a hardcoded number.
- When choosing `RELAY_PINS` for a new/changed board, avoid strapping pins, native-USB pins, and pins already used for Serial/UART; document the reasoning in the file header comment.
- UI/UX rule: every control must have readable contrast against its own background (never same/near-same color for text and fill), and font sizes must fit their container. Reuse the `App.xaml` styles/brushes instead of ad-hoc colors; when a custom `ControlTemplate` is written, bind `Foreground`, `Padding`, and font properties through (`TemplateBinding`) and check new/auto-sized controls (buttons, textboxes, labels) in the running app for clipped, overflowing, or invisible text before reporting done.
- After editing the WPF app, verify with `dotnet build` in `app/RelayControlWPF`. If it fails on a file lock, `RelayControlWPF.exe` is still running — ask the user to close it (or kill the PID) before rebuilding.
- Firmware changes only take effect after a manual `pio run -t upload` to the physical board — flag this explicitly to the user, since it cannot be done remotely.
- When relay count or pin mapping changes, update both READMEs (root and `app/RelayControlWPF/`) in the same change, not as a follow-up.
- When a new project-wide principle or convention emerges during work, propose adding it to this file and wait for confirmation before editing it.
