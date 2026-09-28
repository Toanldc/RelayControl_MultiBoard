/*
  8-Relay Control via UART (Serial) and BLE
  MCU: ESP32-C3 SuperMini

  Command protocol, identical over both transports (terminated with '\n'):
    R1ON   -> turn relay 1 ON
    R1OFF  -> turn relay 1 OFF
    R2ON / R2OFF ... R8ON / R8OFF
    STATUS -> request the current status of all 8 relays

  Board replies, sent back over whichever transport the command arrived on:
    OK:R1ON              (command executed successfully)
    ERR:UNKNOWN_CMD       (invalid command)
    STATUS:10101010         (8-character 0/1 string, relay 1..8, 1=ON, 0=OFF)

  BLE exposes a Nordic UART Service (NUS)-compatible GATT profile, so the
  same text protocol above is carried as newline-terminated notifications/
  writes instead of Serial bytes - any generic BLE-UART terminal app can
  talk to it too, for debugging without the desktop app.
    Device name  : RelayControl-ESP32C3
    Service UUID : 6E400001-B5A3-F393-E0A9-E50E24DCCA9E
    RX (Write)   : 6E400002-B5A3-F393-E0A9-E50E24DCCA9E  (app -> board)
    TX (Notify)  : 6E400003-B5A3-F393-E0A9-E50E24DCCA9E  (board -> app)

  IMPORTANT NOTE:
  - Many common relay modules are ACTIVE-LOW
    (i.e. the control pin must be LOW for the relay to energize/turn ON).
  - Set RELAY_ACTIVE_LOW = true if your module is this type
    (verify by testing, or check the module's datasheet/silkscreen notes).
  - If unsure, leave it as true first (most cheap modules are this type);
    if the relay behaves inverted, change it to false.
  - ESP32-C3 SuperMini GPIO 0,1,3,4,5,6,7,10 are used here (the 8 safe
    general-purpose pins on this board, avoiding strapping pins 2/8/9,
    the native-USB pins 18/19, and UART0 pins 20/21).
  - UART and BLE both work at the same time; each connected transport gets
    replies only for the commands it sent.
  - Onboard WS2812 RGB status LED (GPIO8) reflects BLE connection state only
    (UART has no protocol-level "connected" signal to show): blinks red
    once per second while no BLE central is connected, solid green once one
    connects.
*/

#include <Arduino.h>
#include <basetypes.h>
#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>
#include <Adafruit_NeoPixel.h>

// ==== RELAY PIN CONFIGURATION ====
const u8 RELAY_COUNT = 8;
const u8 RELAY_PINS[RELAY_COUNT] = {0, 1, 3, 4, 5, 6, 7, 10}; // Relay 1..8
const bool RELAY_ACTIVE_LOW = true;             // set to false if module is active-high

// ==== BLE CONFIGURATION ====
#define BLE_DEVICE_NAME  "RelayControl-ESP32C3"
#define NUS_SERVICE_UUID "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"
#define NUS_RX_CHAR_UUID "6E400002-B5A3-F393-E0A9-E50E24DCCA9E" // app -> board
#define NUS_TX_CHAR_UUID "6E400003-B5A3-F393-E0A9-E50E24DCCA9E" // board -> app

// ==== STATUS LED CONFIGURATION ====
const u8 STATUS_LED_PIN = 8; // onboard WS2812, most ESP32-C3 SuperMini boards
const unsigned long STATUS_LED_BLINK_INTERVAL_MS = 1000;
Adafruit_NeoPixel statusLed(1, STATUS_LED_PIN, NEO_GRB + NEO_KHZ800);

// ==== STATE VARIABLES ====
bool relayState[RELAY_COUNT] = {false, false, false, false, false, false, false, false}; // false = OFF, true = ON

// ==== COMMAND BUFFERS ====
// Kept separate per transport so a partial UART line and a partial BLE
// write never get mixed into the same command.
String uartInputBuffer = "";
String bleInputBuffer = "";

BLECharacteristic* bleTxCharacteristic = nullptr;
volatile bool bleClientConnected = false;

// ==== STATUS LED STATE ====
bool statusLedShowingConnected = false; // last rendered state, to avoid re-sending the same color every loop
bool statusLedBlinkOn = false;
unsigned long statusLedLastToggle = 0;

void setRelay(u8 index, bool on) {
  // index: 0..7 corresponds to relay 1..8
  relayState[index] = on;
  bool pinLevel = RELAY_ACTIVE_LOW ? !on : on; // invert level if module is active-low
  digitalWrite(RELAY_PINS[index], pinLevel ? HIGH : LOW);
}

void replyOverSerial(const String& line) {
  Serial.println(line);
}

void replyOverBle(const String& line) {
  if (bleTxCharacteristic == nullptr || !bleClientConnected) return;
  String framed = line + "\n"; // same line-framing as Serial, so the app can reuse one reader
  bleTxCharacteristic->setValue((uint8_t*)framed.c_str(), framed.length());
  bleTxCharacteristic->notify();
}

// handleCommand takes a reply sink so the same parsing logic serves both the
// UART loop and the BLE RX write callback, and replies go back out over
// whichever transport the command came in on.
void handleCommand(String cmd, void (*reply)(const String&)) {
  cmd.trim();
  cmd.toUpperCase();

  if (cmd == "STATUS") {
    String s = "STATUS:";
    for (u8 i = 0; i < RELAY_COUNT; i++) {
      s += relayState[i] ? "1" : "0";
    }
    reply(s);
    return;
  }

  // Command format: R<n>ON or R<n>OFF, n = 1..8
  if (cmd.length() >= 4u && cmd.charAt(0) == 'R') {
    int relayNum = cmd.charAt(1) - '0'; // convert digit character to number
    if (relayNum >= 1 && relayNum <= RELAY_COUNT) {
      u8 idx = relayNum - 1;
      String action = cmd.substring(2); // remaining part: "ON" or "OFF"

      if (action == "ON") {
        setRelay(idx, true);
        reply("OK:R" + String(relayNum) + "ON");
        return;
      } else if (action == "OFF") {
        setRelay(idx, false);
        reply("OK:R" + String(relayNum) + "OFF");
        return;
      }
    }
  }

  // No format matched above
  reply("ERR:UNKNOWN_CMD");
}

// Blinks red once per second while no BLE central is connected, solid green
// once one is. Non-blocking (no delay()) so it doesn't stall Serial/BLE
// handling, and only calls show() on an actual state change - not every
// loop() iteration - to avoid needlessly re-driving the WS2812 data line.
void updateStatusLed() {
  if (bleClientConnected) {
    if (!statusLedShowingConnected) {
      statusLed.setPixelColor(0, statusLed.Color(0, 255, 0)); // solid green
      statusLed.show();
      statusLedShowingConnected = true;
    }
    return;
  }

  if (statusLedShowingConnected) {
    // Just disconnected; restart the blink cycle from "on" immediately.
    statusLedShowingConnected = false;
    statusLedBlinkOn = false;
    statusLedLastToggle = 0;
  }

  unsigned long now = millis();
  if (now - statusLedLastToggle >= STATUS_LED_BLINK_INTERVAL_MS) {
    statusLedLastToggle = now;
    statusLedBlinkOn = !statusLedBlinkOn;
    statusLed.setPixelColor(0, statusLedBlinkOn ? statusLed.Color(255, 0, 0) : statusLed.Color(0, 0, 0));
    statusLed.show();
  }
}

// ==== BLE SERVER CALLBACKS ====
class RelayServerCallbacks : public BLEServerCallbacks {
  void onConnect(BLEServer* server) override {
    bleClientConnected = true;
    bleInputBuffer = "";
  }
  void onDisconnect(BLEServer* server) override {
    bleClientConnected = false;
    server->getAdvertising()->start(); // advertising stops on disconnect; restart so the app can reconnect
  }
};

// ==== BLE RX CHARACTERISTIC CALLBACKS ====
class RelayRxCallbacks : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic* characteristic) override {
    String chunk = characteristic->getValue().c_str();
    for (size_t i = 0; i < chunk.length(); i++) {
      char c = chunk.charAt(i);
      if (c == '\n') {
        handleCommand(bleInputBuffer, replyOverBle);
        bleInputBuffer = "";
      } else if (c != '\r') {
        bleInputBuffer += c;
      }
    }
  }
};

void setupBle() {
  BLEDevice::init(BLE_DEVICE_NAME);

  BLEServer* server = BLEDevice::createServer();
  server->setCallbacks(new RelayServerCallbacks());

  BLEService* service = server->createService(NUS_SERVICE_UUID);

  BLECharacteristic* rxCharacteristic = service->createCharacteristic(
      NUS_RX_CHAR_UUID,
      BLECharacteristic::PROPERTY_WRITE | BLECharacteristic::PROPERTY_WRITE_NR);
  rxCharacteristic->setCallbacks(new RelayRxCallbacks());

  bleTxCharacteristic = service->createCharacteristic(
      NUS_TX_CHAR_UUID,
      BLECharacteristic::PROPERTY_NOTIFY);
  bleTxCharacteristic->addDescriptor(new BLE2902()); // required for the client to enable notifications

  service->start();

  BLEAdvertising* advertising = BLEDevice::getAdvertising();
  advertising->addServiceUUID(NUS_SERVICE_UUID);
  advertising->setScanResponse(true);
  advertising->start();
}

void setup() {
  Serial.begin(9600);

  // Set relay pins as OUTPUT and turn all relays OFF right at startup
  for (u8 i = 0; i < RELAY_COUNT; i++) {
    pinMode(RELAY_PINS[i], OUTPUT);
    setRelay(i, false); // ensure relays are OFF on power-up to avoid unwanted triggering
  }

  statusLed.begin();
  statusLed.setBrightness(40); // full brightness is uncomfortably bright for a status indicator
  statusLed.show(); // off; updateStatusLed() starts the red blink on the first loop() call

  setupBle();

  Serial.println("READY:8RELAY_UART_BLE_CONTROL");
}

void loop() {
  // Read Serial data, accumulate into buffer until a newline character is received
  while (Serial.available() > 0u) {
    char c = Serial.read();
    if (c == '\n') {
      handleCommand(uartInputBuffer, replyOverSerial);
      uartInputBuffer = "";
    } else if (c != '\r') { // ignore '\r' if present
      uartInputBuffer += c;
    }
  }
  // BLE commands are handled event-driven in RelayRxCallbacks::onWrite, not polled here.

  updateStatusLed();
}
