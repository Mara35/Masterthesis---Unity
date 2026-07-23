/*
 * Project:    Arm motion tracking node (game firmware)
 * File:       ArmIMU_Game
 * Board:      Seeed XIAO ESP32C6 + BNO086 IMU
 *
 *
 * Summary:
 *   Instrumented variant of the arm-tracking firmware, used only to characterise the
 *   wireless transport. It behaves like the game firmware but adds three things:
 *
 *     1. A sequence counter in every packet, so the PC can detect lost packets.
 *     2. A ping echo: 8-byte pings are mirrored back to the sender unchanged,
 *        which lets the PC measure the round-trip time.
 *     3. An on-device timer that reports how long assembling and sending one
 *        packet takes, to separate firmware cost from network cost.
 *
 *   WiFi power save is disabled here so the channel is characterised without sleep
 *   cycles. The game firmware keeps power save enabled to preserve battery life, so
 *   the RTT values measured with this firmware are a lower bound for gameplay.
 *
 *   Serial output is muted during a measurement run (QUIET_MODE) so that printing
 *   does not distort the timing.
 *
 * Packet format (sent, 24 bytes):
 *   [0..2] header 0xFF 0x00 0xFF, [3] strapId,
 *   [4..19] quaternion x, y, z, w as floats, [20..23] uint32 sequence counter
 *
 * Ping format (received and mirrored, 8 bytes):
 *   [0..3] 0xFE 0xEE 0xFE 0xEE, [4..7] uint32 ping id
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
#define BNO08X_RESET -1

// === For Upper Arm: ===
//const byte strapId = 3;
//const int localPort = 9001;

// === For Forearm: ===
const byte strapId = 4;
const int localPort = 9001;

const char *ssid = "LAPTOP-Mara";
const char *pwd  = "2Rc20931";

// XIAO ESP32C6 I2C pins:
const int SDA_PIN = D4;
const int SCL_PIN = D5;


// -----------------------------
// Measurement: sequence counter and on-device timing
// -----------------------------
uint32_t      imuSeq        = 0;      // incremented per packet, lets the PC spot gaps
const bool    QUIET_MODE    = true;   // mute serial output during a run
unsigned long espProcAccum  = 0;      // accumulated send duration in microseconds
uint32_t      espProcCount  = 0;      // number of samples in the accumulator
unsigned long lastEspReport = 0;      // last time the average was printed


// -----------------------------
// Vibration motor
// -----------------------------
struct VibrationMotor {
  uint8_t pin = D3;
  float value = 0.0f;
  unsigned long stopTime = 0;
  bool enabled = true;

  void begin() { pinMode(pin, OUTPUT); analogWrite(pin, 0); }

// intensity 0..1; durationSeconds <= 0 means "no timeout".
  void setVibration(float newValue, float durationSeconds = -1) {
    value = constrain(newValue, 0.0f, 1.0f);
    if (!enabled) value = 0.0f;
    analogWrite(pin, (int)(value * 255));
    if (durationSeconds > 0) stopTime = millis() + (unsigned long)(durationSeconds * 1000);
    else stopTime = 0;
  }

  // Switches the motor off once its timeout has elapsed.
  void poll() {
    if (stopTime > 0 && millis() > stopTime) { setVibration(0.0f); stopTime = 0; }
  }

  // Blocking single pulse, used as boot and WiFi-connected indicator.
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
    // Block until the sensor answers; without it there is nothing to send.
    while (!bno.begin_I2C(0x4B, &Wire)) { Serial.println("Failed to find BNO08x chip"); delay(1000); }
    Serial.println("BNO08x found");
    setReport();
    // Keep calibrating accelerometer and gyroscope at runtime.
    sh2_setCalConfig(SH2_CAL_ACCEL | SH2_CAL_GYRO);
    delay(50);
  }

  bool poll() {
    delay(10);
    // The BNO can reset itself and then forgets which report was enabled.
    if (bno.wasReset()) { Serial.println("Sensor was reset"); setReport(); }
    return bno.getSensorEvent(&sensorValue);
  }

  void setReport() {
    // Rotation vector = the fused 9-axis absolute orientation quaternion.
    if (!bno.enableReport(SH2_ROTATION_VECTOR)) Serial.println("Could not enable rotation vector");
    delay(2);
  }
} imu;

// -----------------------------
// WiFi / UDP
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
    esp_wifi_set_max_tx_power(15);        // limit TX power to reduce current draw
    WiFi.setSleep(false);                 // measurement: no WiFi power save -> low RTT
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
      // Broadcast, so the PC's IP may change without reflashing. No ACK, no retransmission.
      serverIp = WiFi.broadcastIP();
      Serial.print("Connected to WiFi. Broadcast IP: ");
      Serial.println(serverIp);
      if (!wifiVibrationDone) { vibrationMotor.pulse(300); wifiVibrationDone = true; }
    }
    if (!connectedNow) wifiVibrationDone = false;  // arm the pulse for the next reconnect

    int len = udp.parsePacket();
    if (len > 0) {
      IPAddress rip   = udp.remoteIP();    // remember the sender for the ping echo
      uint16_t  rport = udp.remotePort();
      len = udp.read(readBuffer, 512);
      
      // RTT ping (FE EE FE EE + uint32 id): mirror it back to the sender unchanged,
      // as a unicast reply, so the PC can measure the round-trip time.
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
    udp.beginPacket(serverIp, localPort);  // broadcast
    udp.write(buffer, size);
    udp.endPacket();
  }
} wifiConnection;

// -----------------------------
// Send quaternion
// Packet: 24 bytes = header(4) + quaternion floats(16) + sequence(4)
// -----------------------------
void sendRotationWithIndex() {
  auto& s = imu.sensorValue;

  byte buffer[24];
  buffer[0]=0xFF; buffer[1]=0x00; buffer[2]=0xFF; buffer[3]=strapId;   // Header

  // Axis remap (i, -k, j, real) converts the sensor frame into Unity's frame.
  float quat[4] = {
    s.un.rotationVector.i,
    -s.un.rotationVector.k,
    s.un.rotationVector.j,
    s.un.rotationVector.real
  };
   memcpy(buffer + 4,  quat, sizeof(quat));   // bytes 4..19, same offsets as the game firmware
  memcpy(buffer + 20, &imuSeq, 4);           // bytes 20..23: sequence counter

  if (!QUIET_MODE) {
    Serial.printf("%5.2f %5.2f %5.2f %5.2f seq=%lu\n",
                  quat[0], quat[1], quat[2], quat[3], (unsigned long)imuSeq);
  }

  wifiConnection.write(buffer, sizeof(buffer));
  imuSeq++;
}

// -----------------------------
// Receive UDP messages (pings are already handled in poll())
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

  // Send on a fixed 15 ms grid (about 66 Hz), independent of the loop rate.
  static unsigned long nextTSendQuat = 0;
  if (millis() >= nextTSendQuat) {
    nextTSendQuat += 15;
    // If the schedule fell behind by more than 100 ms, resynchronise instead of
    // firing a burst of catch-up packets.
    if (millis() > nextTSendQuat + 100) nextTSendQuat = millis() + 15;

    // Measure how long building and handing off one packet takes on the device.
    // This isolates firmware cost from the network latency measured on the PC.
    unsigned long tProc0 = micros();
    sendRotationWithIndex();
    unsigned long tProc1 = micros();
    espProcAccum += (tProc1 - tProc0);
    espProcCount++;

    // Report the average once per second.
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