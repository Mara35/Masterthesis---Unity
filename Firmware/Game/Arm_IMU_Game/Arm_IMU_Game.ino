/*
 * Project:    Arm motion tracking node (game firmware)
 * File:       ArmIMU_Game
 * Board:      Seeed XIAO ESP32C6 + BNO086 IMU
 *
 * Summary:
 *   Firmware for one arm-tracking strap. Reads the absolute orientation of a BNO086
 *   over I2C and broadcasts it as a UDP quaternion packet at roughly 66 Hz, so the
 *   Unity application can drive the corresponding avatar bone.
 *
 *   Two identical nodes are used, distinguished only by strapId:
 *     strapId 3 = upper arm, strapId 4 = forearm.
 *   Set strapId below before flashing each node.
 *
 *   The node also listens for vibration commands from Unity and drives a small
 *   vibration motor (used for haptic feedback and as a connection indicator).
 *
 * Packet format (sent, 20 bytes):
 *   [0..2] header 0xFF 0x00 0xFF, [3] strapId, [4..19] quaternion x, y, z, w as floats
 *
 * Packet format (received, 12 bytes):
 *   [0..2] header 0xFA 0xFB 0x02, then float intensity and float duration in seconds
 */


#include <Arduino.h>
#include <WiFi.h>
#include <esp_wifi.h>
#include <WiFiUdp.h>
#include <Wire.h>
#include <SPI.h>
#include <Adafruit_BNO08x.h>

// -----------------------------
// Configuration
// -----------------------------
#define BNO08X_RESET -1   // no hardware reset pin wired

// Identifies this node in every packet. Unity maps it to a bone via StreamSensorModel.
// Flash one node with 4 (forearm) and the other with 3 (upper arm).

// === FOR FOREARM (Sensor 4): ===
const byte strapId = 4;
const int localPort = 9001;

// === For Upper Arm (SENSOR 6): ===
//const byte strapId = 3;
//const int localPort = 9001;

const char *ssid = "LAPTOP-Mara";
const char *pwd  = "2Rc20931";

// XIAO ESP32C6 I2C:
const int SDA_PIN = D4;
const int SCL_PIN = D5;

// -----------------------------
// Vibration motor
// -----------------------------
struct VibrationMotor {
  uint8_t pin = D3; 
  float value = 0.0f; 
  unsigned long stopTime = 0;     // 0 = run until told otherwise
  bool enabled = true;            

  void begin() {
    pinMode(pin, OUTPUT);
    analogWrite(pin, 0);
  }

  // intensity 0..1; durationSeconds <= 0 means "no timeout".
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

  // Called every loop: switches the motor off once its timeout has elapsed.
  void poll() {
    if (stopTime > 0 && millis() > stopTime) {
      setVibration(0.0f);
      stopTime = 0;
    }
  }

  // Blocking single pulse, used as a startup and WiFi-connected indicator.
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

    // Block until the sensor answers; without it there is nothing to send.
    while (!bno.begin_I2C(0x4B, &Wire)) {
      Serial.println("Failed to find BNO08x chip");
      delay(1000);
    }

    Serial.println("BNO08x found");
    setReport();

    // Let the sensor keep calibrating accelerometer and gyroscope at runtime.
    sh2_setCalConfig(SH2_CAL_ACCEL | SH2_CAL_GYRO);
    delay(50);
  }

  bool poll() {
    delay(10);

    // The BNO can reset itself (e.g. on a power glitch) and then forgets which
    // report was enabled, so re-enable it after every reset.
    if (bno.wasReset()) {
      Serial.println("Sensor was reset");
      setReport();
    }

    return bno.getSensorEvent(&sensorValue);
  }

  void setReport() {
    // Rotation vector = the fused 9-axis absolute orientation quaternion.
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

 
  bool connected = false;
  wl_status_t status = WL_IDLE_STATUS;
  bool wifiVibrationDone = false; // makes sure the connect pulse fires only once

  std::function<void(byte*, size_t)> onMessage = nullptr;

  void begin() {
    WiFi.begin(ssid, pwd);
    udp.begin(localPort);
    esp_wifi_set_max_tx_power(15); 
  }

  void poll() {
    status = WiFi.status();
    static unsigned long lastPoll = 0;

     // Retry the connection at most every 10 s while disconnected.
    if ((status == WL_DISCONNECTED || status == WL_CONNECT_FAILED || status == WL_NO_SSID_AVAIL) &&
        millis() > lastPoll + 10000) {
      WiFi.disconnect(true);
      WiFi.begin(ssid, pwd);
      lastPoll = millis();
    }

    bool connectedNow = (status == WL_CONNECTED);
    if (connectedNow && !connected) {
      // Broadcast instead of a fixed host address, so the PC's IP may change without
      // reflashing. Note that broadcast UDP has no acknowledgement or retransmission.
      serverIp = WiFi.broadcastIP();
      Serial.print("Connected to WiFi. Gateway IP: ");
      Serial.println(serverIp);
      if (!wifiVibrationDone) {
        vibrationMotor.pulse(300); // haptic "connected" confirmation
        wifiVibrationDone = true;
      }
    }

    if (!connectedNow) {
      wifiVibrationDone = false; // arm the pulse again for the next reconnect
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

    udp.beginPacket(serverIp, localPort);
    udp.write(buffer, size);
    udp.endPacket();
  }
} wifiConnection;

// -----------------------------
// send quaternion
// -----------------------------
void sendRotationWithIndex() {
  auto& s = imu.sensorValue;

  // The first float is only a placeholder: the 4-byte header is written over it below,
  // which keeps the following four floats 4-byte aligned in the packet.
  // Axis remap (i, -k, j, real) converts the sensor frame into Unity's frame.
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
// Receive UDP messages
// -----------------------------
void onUdpMessage(byte* buffer, size_t size) {
  if (size < 4) return;

  // Vibration command: header 0xFA 0xFB 0x02, then intensity and duration as floats.
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
  vibrationMotor.pulse(300);  // short pulse confirms the board booted

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

  // Send on a fixed 15 ms grid (about 66 Hz) instead of every loop pass, so the
  // rate stays independent of how fast the loop happens to run.
  static unsigned long nextTSendQuat = 0;
  if (millis() >= nextTSendQuat) {
    nextTSendQuat += 15;
    
    // If the schedule fell behind by more than 100 ms (e.g. after a blocking
    // reconnect), resynchronise instead of firing a burst of catch-up packets.
    if (millis() > nextTSendQuat + 100) nextTSendQuat = millis() + 15;

    sendRotationWithIndex();

    if (!wifiConnection.connected) {
      Serial.print("Not connected to WiFi, status=");
      Serial.println((int)wifiConnection.status);
    }
  }
}

