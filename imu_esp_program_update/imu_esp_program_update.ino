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


const byte strapId = 6;


const char *ssid = "LAPTOP-Mara";
const char *pwd  = "2Rc209@3";

// XIAO ESP32C6 I2C:
// SDA = D4
// SCL = D5
const int SDA_PIN = D4;
const int SCL_PIN = D5;

// -----------------------------
// Vibrationsmotor
// -----------------------------
struct VibrationMotor {
  uint8_t pin = D3;
  float value = 0.0f;
  unsigned long stopTime = 0;
  bool enabled = true;

  void begin() {
    pinMode(pin, OUTPUT);
    analogWrite(pin, 0);   // sicher aus
  }

  void setVibration(float newValue, float durationSeconds = -1) {
    value = constrain(newValue, 0.0f, 1.0f);
    if (!enabled) {
      value = 0.0f;
    }
    analogWrite(pin, (int)(value * 255));

    if (durationSeconds > 0) {
      stopTime = millis() + (unsigned long)(durationSeconds * 1000);
    } else {
      stopTime = 0;
    }
  }

  void poll() {
    if (stopTime > 0 && millis() > stopTime) {
      setVibration(0.0f);
      stopTime = 0;
    }
  }

  void pulse(unsigned long durationMs) {
    if (!enabled) return;

    analogWrite(pin, 255);
    delay(durationMs);
    analogWrite(pin, 0);

    value = 0.0f;
    stopTime = 0;
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

    // Dein Scanner hat 0x4B gefunden
    while (!bno.begin_I2C(0x4B, &Wire)) {
      Serial.println("Failed to find BNO08x chip");
      delay(1000);
    }

    Serial.println("BNO08x found");
    setReport();

    sh2_setCalConfig(SH2_CAL_ACCEL | SH2_CAL_GYRO);
    delay(50);
  }

  bool poll() {
    delay(10);

    if (bno.wasReset()) {
      Serial.println("Sensor was reset");
      setReport();
    }

    return bno.getSensorEvent(&sensorValue);
  }

  void setReport() {
    if (!bno.enableReport(SH2_ROTATION_VECTOR)) {
      Serial.println("Could not enable rotation vector");
    }
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

  const int port = 9001;
  bool connected = false;
  wl_status_t status = WL_IDLE_STATUS;

  bool wifiVibrationDone = false; 

  std::function<void(byte*, size_t)> onMessage = nullptr;

  void begin() {
    WiFi.begin(ssid, pwd);
    udp.begin(port);
    esp_wifi_set_max_tx_power(20);
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
      serverIp = IPAddress(192,168,137,1);
      Serial.print("Connected to WiFi. Gateway IP: ");
      Serial.println(serverIp);

      
      if (!wifiVibrationDone) {
        vibrationMotor.pulse(300);
        wifiVibrationDone = true;
      }
    }

    if (!connectedNow) {
      wifiVibrationDone = false;
    }

    int len = udp.parsePacket();
    if (len > 0) {
      len = udp.read(readBuffer, 512);
      if (onMessage) {
        onMessage(readBuffer, len);
      }
    }

    connected = connectedNow;
  }

  WifiConnection() : readBuffer(new byte[512]) {}

  ~WifiConnection() {
    delete[] readBuffer;
  }

  void write(byte* buffer, size_t size) {
    if (!connected) {
      return;
    }

    udp.beginPacket(serverIp, port);
    udp.write(buffer, size);
    udp.endPacket();
  }
} wifiConnection;

// -----------------------------
// Quaternion senden
// -----------------------------
void sendRotationWithIndex() {
  auto& s = imu.sensorValue;

  float values[] = {
    0.0f,
    s.un.rotationVector.i,
    -s.un.rotationVector.k,
    s.un.rotationVector.j,
    s.un.rotationVector.real
  };

  byte* buffer = (byte*)values;

  memcpy(buffer, (byte[]){0xFF, 0x00, 0xFF, strapId}, 4);

  Serial.printf("%5.2f %5.2f %5.2f %5.2f\n",
                values[1], values[2], values[3], values[4]);

  wifiConnection.write(buffer, sizeof(float) * 5);
}

// -----------------------------
// UDP-Nachrichten empfangen
// -----------------------------
void onUdpMessage(byte* buffer, size_t size) {
  if (size < 4) return;

  if (size == sizeof(float) * 3 &&
      buffer[0] == 0xFA &&
      buffer[1] == 0xFB &&
      buffer[2] == 0x02) {
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

  Serial.println("Starting device");

  vibrationMotor.begin();

  // --- HINZUGEFÜGT: einmal kurz beim Einschalten ---
  vibrationMotor.pulse(300);

  imu.begin();

  wifiConnection.onMessage = onUdpMessage;
  wifiConnection.begin();
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

    if (millis() > nextTSendQuat + 100) {
      nextTSendQuat = millis() + 15;
    }

    sendRotationWithIndex();

    if (wifiConnection.connected) {
      Serial.println("Sending");
    } else {
      Serial.print("Not connected to WiFi, status=");
      Serial.println((int)wifiConnection.status);
    }
  }
}