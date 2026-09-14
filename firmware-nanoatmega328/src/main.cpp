/*
  5-Relay Control via UART (Serial)
  MCU: Arduino Nano (ATmega328P)

  Command protocol (sent from PC, terminated with '\n'):
    R1ON   -> turn relay 1 ON
    R1OFF  -> turn relay 1 OFF
    R2ON / R2OFF ... R5ON / R5OFF
    STATUS -> request the current status of all 5 relays

  Nano replies over Serial:
    OK:R1ON              (command executed successfully)
    ERR:UNKNOWN_CMD       (invalid command)
    STATUS:10101           (5-character 0/1 string, relay 1..5, 1=ON, 0=OFF)

  IMPORTANT NOTE:
  - Many common 5-channel relay modules are ACTIVE-LOW
    (i.e. the control pin must be LOW for the relay to energize/turn ON).
  - Set RELAY_ACTIVE_LOW = true if your module is this type
    (verify by testing, or check the module's datasheet/silkscreen notes).
  - If unsure, leave it as true first (most cheap modules are this type);
    if the relay behaves inverted, change it to false.
*/

#include <Arduino.h>
#include <basetypes.h>

// ==== RELAY PIN CONFIGURATION ====
const u8 RELAY_PINS[5] = {2, 3, 4, 5, 6}; // Relay 1..5
const bool RELAY_ACTIVE_LOW = true;             // set to false if module is active-high

// ==== STATE VARIABLES ====
bool relayState[5] = {false, false, false, false, false}; // false = OFF, true = ON

// ==== SERIAL COMMAND BUFFER ====
String inputBuffer = "";

void setRelay(u8 index, bool on) {
  // index: 0..4 corresponds to relay 1..5
  relayState[index] = on;
  bool pinLevel = RELAY_ACTIVE_LOW ? !on : on; // invert level if module is active-low
  digitalWrite(RELAY_PINS[index], pinLevel ? HIGH : LOW);
}

void setup() {
  Serial.begin(9600);

  // Set relay pins as OUTPUT and turn all relays OFF right at startup
  for (u8 i = 0; i < 5; i++) {
    pinMode(RELAY_PINS[i], OUTPUT);
    setRelay(i, false); // ensure relays are OFF on power-up to avoid unwanted triggering
  }

  Serial.println("READY:5RELAY_UART_CONTROL");
}

void handleCommand(String cmd) {
  cmd.trim();
  cmd.toUpperCase();

  if (cmd == "STATUS") {
    String s = "STATUS:";
    for (u8 i = 0; i < 5u; i++) {
      s += relayState[i] ? "1" : "0";
    }
    Serial.println(s);
    return;
  }

  // Command format: R<n>ON or R<n>OFF, n = 1..5
  if (cmd.length() >= 4u && cmd.charAt(0) == 'R') {
    int relayNum = cmd.charAt(1) - '0'; // convert digit character to number
    if (relayNum >= 1 && relayNum <= 5) {
      u8 idx = relayNum - 1;
      String action = cmd.substring(2); // remaining part: "ON" or "OFF"

      if (action == "ON") {
        setRelay(idx, true);
        Serial.println("OK:R" + String(relayNum) + "ON");
        return;
      } else if (action == "OFF") {
        setRelay(idx, false);
        Serial.println("OK:R" + String(relayNum) + "OFF");
        return;
      }
    }
  }

  // No format matched above
  Serial.println("ERR:UNKNOWN_CMD");
}

void loop() {
  // Read Serial data, accumulate into buffer until a newline character is received
  while (Serial.available() > 0u) {
    char c = Serial.read();
    if (c == '\n') {
      handleCommand(inputBuffer);
      inputBuffer = "";
    } else if (c != '\r') { // ignore '\r' if present
      inputBuffer += c;
    }
  }
}
