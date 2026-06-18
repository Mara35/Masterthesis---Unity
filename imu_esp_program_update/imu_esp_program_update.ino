#include <Arduino.h>
#include <WiFi.h>
#include <esp_wifi.h>
#include <WiFiUdp.h>
#include <Wire.h>
#include <SPI.h>
#include <Adafruit_BNO08x.h>

// -----------------------------
// Einstellungen
// -----------------------------
#define BNO08X_RESET -1

// === FÜR UNTERARM (SENSOR 6): ===
const byte strapId = 3;
const int localPort = 9001;

// === FÜR OBERARM (SENSOR 4): ===
//const byte strapId = 4;           // 
//const int localPort = 9002;       // 

const char *ssid = "TP-Link_2582";
const char *pwd = "24707985";

//const char *ssid = "LAPTOP-Mara"; // [cite: 3]
//const char *pwd  = "2Rc209@3";    // [cite: 3]

// XIAO ESP32C6 I2C:
const int SDA_PIN = D4;           // [cite: 4]
const int SCL_PIN = D5;           // [cite: 4]

// -----------------------------
// Vibrationsmotor
// -----------------------------
struct VibrationMotor {
  uint8_t pin = D3;               // [cite: 5]
  float value = 0.0f;             // [cite: 5]
  unsigned long stopTime = 0;     // [cite: 5]
  bool enabled = true;            // [cite: 6]

  void begin() {
    pinMode(pin, OUTPUT);         // [cite: 6]
    analogWrite(pin, 0);          // [cite: 6]
  }

  void setVibration(float newValue, float durationSeconds = -1) {
    value = constrain(newValue, 0.0f, 1.0f); // [cite: 7]
    if (!enabled) {               // [cite: 8]
      value = 0.0f;               // [cite: 8]
    }
    analogWrite(pin, (int)(value * 255)); // [cite: 8]
    if (durationSeconds > 0) {    // [cite: 9]
      stopTime = millis() + (unsigned long)(durationSeconds * 1000); // [cite: 9]
    } else {                      // [cite: 10]
      stopTime = 0;               // [cite: 10]
    }
  }

  void poll() {
    if (stopTime > 0 && millis() > stopTime) { // [cite: 11]
      setVibration(0.0f);         // [cite: 11]
      stopTime = 0;               // [cite: 12]
    }
  }

  void pulse(unsigned long durationMs) {
    if (!enabled) return;         // [cite: 12]

    analogWrite(pin, 255);        // [cite: 12]
    delay(durationMs);            // [cite: 13]
    analogWrite(pin, 0);          // [cite: 13]

    value = 0.0f;                 // [cite: 13]
    stopTime = 0;                 // [cite: 13]
  }
} vibrationMotor;

// -----------------------------
// IMU / BNO08x
// -----------------------------
Adafruit_BNO08x bno = Adafruit_BNO08x(BNO08X_RESET); // [cite: 13]
struct Imu {
  sh2_SensorValue_t sensorValue;  // [cite: 14]

  void begin() {
    Wire.begin(SDA_PIN, SCL_PIN); // [cite: 14]
    delay(100);                   // [cite: 14]
    
    while (!bno.begin_I2C(0x4B, &Wire)) { // [cite: 15]
      Serial.println("Failed to find BNO08x chip"); // [cite: 15]
      delay(1000);                // [cite: 16]
    }

    Serial.println("BNO08x found"); // [cite: 16]
    setReport();                  // [cite: 16]

    sh2_setCalConfig(SH2_CAL_ACCEL | SH2_CAL_GYRO); // [cite: 16]
    delay(50);                    // [cite: 16]
  }

  bool poll() {
    delay(10);                    // [cite: 17]

    if (bno.wasReset()) {         // [cite: 17]
      Serial.println("Sensor was reset"); // [cite: 17]
      setReport();                // [cite: 18]
    }

    return bno.getSensorEvent(&sensorValue); // [cite: 18]
  }

  void setReport() {
    if (!bno.enableReport(SH2_ROTATION_VECTOR)) { // [cite: 18]
      Serial.println("Could not enable rotation vector"); // [cite: 18]
    }
    delay(2);                     // [cite: 19]
  }
} imu;

// -----------------------------
// WLAN / UDP
// -----------------------------
struct WifiConnection {
  WiFiUDP udp;
  IPAddress serverIp;             // [cite: 19]
  byte* readBuffer = nullptr;     // 

  // KORREKTUR: Der globale 'localPort' wird nun hier genutzt!
  bool connected = false;         // 
  wl_status_t status = WL_IDLE_STATUS; // 
  bool wifiVibrationDone = false; // 

  std::function<void(byte*, size_t)> onMessage = nullptr; // 

  void begin() {
    WiFi.begin(ssid, pwd);        // 
    udp.begin(localPort);         // <--- GEÄNDERT: Nutzt jetzt localPort (9001 oder 9002)
    esp_wifi_set_max_tx_power(15); // 
  }

  void poll() {
    status = WiFi.status();       // [cite: 22]
    static unsigned long lastPoll = 0; // [cite: 22]
    if ((status == WL_DISCONNECTED || status == WL_CONNECT_FAILED || status == WL_NO_SSID_AVAIL) && // [cite: 23]
        millis() > lastPoll + 10000) { // [cite: 23]
      WiFi.disconnect(true);      // [cite: 23]
      WiFi.begin(ssid, pwd);      // [cite: 24]
      lastPoll = millis();        // [cite: 24]
    }

    bool connectedNow = (status == WL_CONNECTED); // [cite: 24]
    if (connectedNow && !connected) { // [cite: 25]
      serverIp = IPAddress(192,168,137,2); // [cite: 25]
      Serial.print("Connected to WiFi. Gateway IP: "); // [cite: 25]
      Serial.println(serverIp);   // [cite: 25]
      if (!wifiVibrationDone) {   // [cite: 26]
        vibrationMotor.pulse(300); // [cite: 26]
        wifiVibrationDone = true;  // [cite: 26]
      }
    }

    if (!connectedNow) {          // [cite: 27]
      wifiVibrationDone = false;  // [cite: 27]
    }

    int len = udp.parsePacket();  // [cite: 28]
    if (len > 0) {                // [cite: 28]
      len = udp.read(readBuffer, 512); // [cite: 28]
      if (onMessage) {            // [cite: 29]
        onMessage(readBuffer, len); // [cite: 29]
      }
    }

    connected = connectedNow;     // [cite: 30]
  }

  WifiConnection() : readBuffer(new byte[512]) {} // [cite: 31]

  ~WifiConnection() {
    delete[] readBuffer;          // [cite: 31]
  }

  void write(byte* buffer, size_t size) {
    if (!connected) {             // [cite: 32]
      return;                     // [cite: 32]
    }

    udp.beginPacket(serverIp, localPort); // <--- GEÄNDERT: Sendet an den korrekten Port zurück
    udp.write(buffer, size);      // 
    udp.endPacket();              // 
  }
} wifiConnection;

// -----------------------------
// Quaternion senden
// -----------------------------
void sendRotationWithIndex() {
  auto& s = imu.sensorValue;       // [cite: 34]
  float values[] = {
    0.0f,                         // [cite: 35]
    s.un.rotationVector.i,        // [cite: 35]
    -s.un.rotationVector.k,       // [cite: 35]
    s.un.rotationVector.j,        // [cite: 35]
    s.un.rotationVector.real      // [cite: 35]
  };
  byte* buffer = (byte*)values;   // [cite: 36]

  memcpy(buffer, (byte[]){0xFF, 0x00, 0xFF, strapId}, 4); // [cite: 36]
  Serial.printf("%5.2f %5.2f %5.2f %5.2f\n", // [cite: 37]
                values[1], values[2], values[3], values[4]); // [cite: 37]
  wifiConnection.write(buffer, sizeof(float) * 5); // [cite: 38]
}

// -----------------------------
// UDP-Nachrichten empfangen
// -----------------------------
void onUdpMessage(byte* buffer, size_t size) {
  if (size < 4) return;           // [cite: 38]
  if (size == sizeof(float) * 3 && // [cite: 39]
      buffer[0] == 0xFA &&        // [cite: 39]
      buffer[1] == 0xFB &&        // [cite: 39]
      buffer[2] == 0x02) {        // [cite: 39]
    float* values = (float*)buffer; // [cite: 39]
    vibrationMotor.setVibration(values[1], values[2]); // [cite: 40]
  }
}

// -----------------------------
// Setup
// -----------------------------
void setup() {
  Serial.begin(115200);           // [cite: 40]
  delay(300);                     // [cite: 40]

  Serial.println("Starting device"); // [cite: 40]

  vibrationMotor.begin();         // [cite: 40]
  vibrationMotor.pulse(300);      // [cite: 41]

  imu.begin();                    // [cite: 41]

  wifiConnection.onMessage = onUdpMessage; // [cite: 41]
  wifiConnection.begin();         // [cite: 41]
}

// -----------------------------
// Loop
// -----------------------------
void loop() {
  wifiConnection.poll();          // [cite: 42]
  vibrationMotor.poll();          // [cite: 42]

  if (!imu.poll()) {              // [cite: 42]
    Serial.println("Failed to read imu"); // [cite: 42]
    return;                       // [cite: 42]
  }

  static unsigned long nextTSendQuat = 0; // [cite: 43]

  if (millis() >= nextTSendQuat) { // [cite: 43]
    nextTSendQuat += 15;          // [cite: 43]
    if (millis() > nextTSendQuat + 100) { // [cite: 44]
      nextTSendQuat = millis() + 15; // [cite: 44]
    }

    sendRotationWithIndex();      // [cite: 45]

    if (wifiConnection.connected) { // [cite: 45]
      Serial.println("Sending");  // [cite: 45]
    } else {                      // [cite: 46]
      Serial.print("Not connected to WiFi, status="); // [cite: 46]
      Serial.println((int)wifiConnection.status); // [cite: 46]
    }
  }
}