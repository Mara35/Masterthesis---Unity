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

// === FÜR OBERARM: ===
//const byte strapId = 3;
//const int localPort = 9001;

// === FÜR UNTERARM: ===
const byte strapId = 4;
const int localPort = 9001;

//const char *ssid = "TP-Link_2582";
//const char *pwd = "24707985";

const char *ssid = "LAPTOP-Mara";
const char *pwd  = "2Rc20931";

// XIAO ESP32C6 I2C:
const int SDA_PIN = D4;
const int SCL_PIN = D5;

// -----------------------------
// MESSUNG: Sequenzzähler + ESP-interne Zeit
// -----------------------------
uint32_t      imuSeq        = 0;
const bool    QUIET_MODE    = true;   // MESS: serielle Ausgabe waehrend Messung aus
unsigned long espProcAccum  = 0;
uint32_t      espProcCount  = 0;
unsigned long lastEspReport = 0;

// -----------------------------
// Vibrationsmotor
// -----------------------------
struct VibrationMotor {
  uint8_t pin = D3;
  float value = 0.0f;
  unsigned long stopTime = 0;
  bool enabled = true;

  void begin() { pinMode(pin, OUTPUT); analogWrite(pin, 0); }

  void setVibration(float newValue, float durationSeconds = -1) {
    value = constrain(newValue, 0.0f, 1.0f);
    if (!enabled) value = 0.0f;
    analogWrite(pin, (int)(value * 255));
    if (durationSeconds > 0) stopTime = millis() + (unsigned long)(durationSeconds * 1000);
    else stopTime = 0;
  }

  void poll() {
    if (stopTime > 0 && millis() > stopTime) { setVibration(0.0f); stopTime = 0; }
  }

  void pulse(unsigned long durationMs) {
    if (!enabled) return;
    analogWrite(pin, 255);
    delay(durationMs);
    analogWrite(pin, 0);
    value = 0.0f; stopTime = 0;
  }
} vibrationMotor;

// -----------------------------
// IMU / BNO08x
// -----------------------------
Adafruit_BNO08x bno = Adafruit_BNO08x(BNO08X_RESET);
struct Imu {
  sh2_SensorValue_t sensorValue;

  void begin() {
    Wire.begin(SDA_PIN, SCL_PIN);
    delay(100);
    while (!bno.begin_I2C(0x4B, &Wire)) { Serial.println("Failed to find BNO08x chip"); delay(1000); }
    Serial.println("BNO08x found");
    setReport();
    sh2_setCalConfig(SH2_CAL_ACCEL | SH2_CAL_GYRO);
    delay(50);
  }

  bool poll() {
    delay(10);
    if (bno.wasReset()) { Serial.println("Sensor was reset"); setReport(); }
    return bno.getSensorEvent(&sensorValue);
  }

  void setReport() {
    if (!bno.enableReport(SH2_ROTATION_VECTOR)) Serial.println("Could not enable rotation vector");
    delay(2);
  }
} imu;

// -----------------------------
// WLAN / UDP
// -----------------------------
struct WifiConnection {
  WiFiUDP udp;
  IPAddress serverIp;
  byte* readBuffer = nullptr;

  bool connected = false;
  wl_status_t status = WL_IDLE_STATUS;
  bool wifiVibrationDone = false;

  std::function<void(byte*, size_t)> onMessage = nullptr;

  void begin() {
    WiFi.begin(ssid, pwd);
    udp.begin(localPort);
    esp_wifi_set_max_tx_power(15);
    WiFi.setSleep(false);                 // MESS: kein WiFi-Stromsparen -> niedrige RTT
  }

  void poll() {
    status = WiFi.status();
    static unsigned long lastPoll = 0;
    if ((status == WL_DISCONNECTED || status == WL_CONNECT_FAILED || status == WL_NO_SSID_AVAIL) &&
        millis() > lastPoll + 10000) {
      WiFi.disconnect(true);
      WiFi.begin(ssid, pwd);
      lastPoll = millis();
    }

    bool connectedNow = (status == WL_CONNECTED);
    if (connectedNow && !connected) {
      serverIp = WiFi.broadcastIP();
      Serial.print("Connected to WiFi. Broadcast IP: ");
      Serial.println(serverIp);
      if (!wifiVibrationDone) { vibrationMotor.pulse(300); wifiVibrationDone = true; }
    }
    if (!connectedNow) wifiVibrationDone = false;

    int len = udp.parsePacket();
    if (len > 0) {
      IPAddress rip   = udp.remoteIP();    // Absender merken (für Ping-Echo)
      uint16_t  rport = udp.remotePort();
      len = udp.read(readBuffer, 512);

      // --- RTT-Ping (FE EE FE EE + uint32 id) -> direkt an Absender zurueckspiegeln ---
      if (len >= 8 && readBuffer[0]==0xFE && readBuffer[1]==0xEE &&
          readBuffer[2]==0xFE && readBuffer[3]==0xEE) {
        udp.beginPacket(rip, rport);
        udp.write(readBuffer, len);
        udp.endPacket();
      } else if (onMessage) {
        onMessage(readBuffer, len);
      }
    }

    connected = connectedNow;
  }

  WifiConnection() : readBuffer(new byte[512]) {}
  ~WifiConnection() { delete[] readBuffer; }

  void write(byte* buffer, size_t size) {
    if (!connected) return;
    udp.beginPacket(serverIp, localPort);  // Broadcast
    udp.write(buffer, size);
    udp.endPacket();
  }
} wifiConnection;

// -----------------------------
// Quaternion senden
// Paket: 24 Byte = Header(4) + Quat-Floats(16) + Seq(4)
// -----------------------------
void sendRotationWithIndex() {
  auto& s = imu.sensorValue;

  byte buffer[24];
  buffer[0]=0xFF; buffer[1]=0x00; buffer[2]=0xFF; buffer[3]=strapId;   // Header
  float quat[4] = {
    s.un.rotationVector.i,
    -s.un.rotationVector.k,
    s.un.rotationVector.j,
    s.un.rotationVector.real
  };
  memcpy(buffer + 4,  quat, sizeof(quat));   // Bytes 4..19 (Offsets unveraendert)
  memcpy(buffer + 20, &imuSeq, 4);           // Bytes 20..23: Sequenzzaehler

  if (!QUIET_MODE) {
    Serial.printf("%5.2f %5.2f %5.2f %5.2f seq=%lu\n",
                  quat[0], quat[1], quat[2], quat[3], (unsigned long)imuSeq);
  }

  wifiConnection.write(buffer, sizeof(buffer));
  imuSeq++;
}

// -----------------------------
// UDP-Nachrichten empfangen (Pings werden schon in poll() behandelt)
// -----------------------------
void onUdpMessage(byte* buffer, size_t size) {
  if (size < 4) return;
  if (size == sizeof(float) * 3 &&
      buffer[0] == 0xFA && buffer[1] == 0xFB && buffer[2] == 0x02) {
    float* values = (float*)buffer;
    vibrationMotor.setVibration(values[1], values[2]);
  }
}

// -----------------------------
// Setup
// -----------------------------
void setup() {
  Serial.begin(115200);
  delay(300);
  Serial.println("Starting device [MEAS]");
  vibrationMotor.begin();
  vibrationMotor.pulse(300);
  imu.begin();
  wifiConnection.onMessage = onUdpMessage;
  wifiConnection.begin();
  lastEspReport = millis();
}

// -----------------------------
// Loop
// -----------------------------
void loop() {
  wifiConnection.poll();
  vibrationMotor.poll();

  if (!imu.poll()) {
    Serial.println("Failed to read imu");
    return;
  }

  static unsigned long nextTSendQuat = 0;
  if (millis() >= nextTSendQuat) {
    nextTSendQuat += 15;
    if (millis() > nextTSendQuat + 100) nextTSendQuat = millis() + 15;

    unsigned long tProc0 = micros();
    sendRotationWithIndex();
    unsigned long tProc1 = micros();
    espProcAccum += (tProc1 - tProc0);
    espProcCount++;

    if (millis() - lastEspReport >= 1000) {
      float avgUs = espProcCount ? (float)espProcAccum / espProcCount : 0.0f;
      Serial.print("[ESP-INT] avg send = ");
      Serial.print(avgUs/1000.0f, 3);
      Serial.println(" ms");
      espProcAccum = 0; espProcCount = 0; lastEspReport = millis();
    }

    if (!wifiConnection.connected) {
      Serial.print("Not connected to WiFi, status=");
      Serial.println((int)wifiConnection.status);
    }
  }
}
