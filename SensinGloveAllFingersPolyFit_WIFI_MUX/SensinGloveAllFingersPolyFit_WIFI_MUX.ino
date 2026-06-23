/*
 * Project:    SensinGlove – Full Hand readout (ESP32 + CD74HC4067 MUX)
 * File:       SensinGloveAllFingersPolyFit_WIFI_MUX.ino
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2026
 *
 * MUX-Kanal-Belegung:
 *   Daumen:  ABAD=C0  PIP=C1  MCP=C2
 *   Index:   ABAD=C3  PIP=C4  MCP=C5
 *   Mittel:  ABAD=C6  PIP=C7  MCP=C8
 *   Ring:    ABAD=C9  PIP=C10 MCP=C11
 *   Klein:   ABAD=C12 PIP=C13 MCP=C14
 *
 * Hardware CD74HC4067:
 *   VCC -> ESP32 3.3V
 *   GND -> ESP32 GND
 *   EN  -> ESP32 GND  (fest LOW, kein ESP32-Pin)
 *   SIG -> ESP32 GPIO 36  (ADC1, WiFi-sicher)
 *   S0  -> ESP32 GPIO 18
 *   S1  -> ESP32 GPIO 19
 *   S2  -> ESP32 GPIO 21
 *   S3  -> ESP32 GPIO 22
 */

#include <Arduino.h>
#if defined(ARDUINO_ARCH_ESP32)
#include "driver/adc.h"
#endif
#include <WiFi.h>
#include <WiFiUdp.h>

// ---------- WiFi / UDP ----------
const char* ssid = "LAPTOP-Mara";
const char* pwd  = "2Rc20931";

WiFiUDP udp;
IPAddress serverIp(192, 168, 137, 165);
const int  port    = 9001;
const byte gloveId = 20;

// ---------- Konfiguration ----------
static const uint32_t BAUD          = 115200;
const unsigned long   PLOT_INTERVAL = 50;
const bool USE_LINEAR_CALIBRATION   = true;

#define POLY_DEG 4
#define COEFFS   (POLY_DEG + 1)

// ---------- CD74HC4067 ----------
#define MUX_SIG_PIN 36
#define MUX_S0      18
#define MUX_S1      19
#define MUX_S2      21
#define MUX_S3      22

// ---------- Enums ----------
enum Finger    { THUMB=0, INDEX, MIDDLE, RING, PINKY, NUM_FINGERS };
enum Joint     { MCP_FLEX=0, MCP_ABAD, PIP_FLEX, NUM_JOINTS };
enum SplitKind { SPLIT_ON_X=0, SPLIT_ON_ANGLE };
enum DomainKind{ DOMAIN_X01=0, DOMAIN_POT_DEG };

const char* FINGER_NAME[NUM_FINGERS] = { "Thumb","Index","Middle","Ring","Pinky" };
const char* JOINT_NAME [NUM_JOINTS ] = { "MCP","AbAd","PIP" };

// ---------- Struct ----------
struct ChannelCfg {
  int   pin;
  int   rawMin;
  int   rawMax;
  bool  negate;
  float tauMs;
  float poly1[COEFFS];
  float poly2[COEFFS];
  SplitKind  splitKind;
  float      splitVal;
  DomainKind domain;
  float potDegMin;
  float potDegMax;
};

// ---------- MUX-Funktionen ----------
static void selectMuxChannel(uint8_t ch) {
  digitalWrite(MUX_S0, (ch >> 0) & 1);
  digitalWrite(MUX_S1, (ch >> 1) & 1);
  digitalWrite(MUX_S2, (ch >> 2) & 1);
  digitalWrite(MUX_S3, (ch >> 3) & 1);
  delayMicroseconds(50);
}

static int muxRead(uint8_t ch) {
  selectMuxChannel(ch);
  return analogRead(MUX_SIG_PIN);
}

// ---------- Hilfsfunktionen ----------
static inline float clampf(float v, float lo, float hi) {
  if (v < lo) return lo;
  if (v > hi) return hi;
  return v;
}

float rawToX01(int raw, int rmin, int rmax) {
  if (rmax == rmin) return 0.0f;
  if (rmax < rmin) { int t = rmin; rmin = rmax; rmax = t; }
  return clampf((float)(raw - rmin) / (float)(rmax - rmin), 0.0f, 1.0f);
}

float rawToPotiDeg(int raw, int rmin, int rmax, float potMin, float potMax) {
  if (rmax == rmin) return potMin;
  if (rmax < rmin) {
    int t = rmin; rmin = rmax; rmax = t;
    float tp = potMin; potMin = potMax; potMax = tp;
  }
  float u = clampf((float)(raw - rmin) / (float)(rmax - rmin), 0.0f, 1.0f);
  return potMin + u * (potMax - potMin);
}

float evaluatePoly(const float coeffs[COEFFS], float X) {
  float p = 0.0f, xn = 1.0f;
  for (int i = 0; i < COEFFS; ++i) { p += coeffs[i] * xn; xn *= X; }
  return p;
}

static inline bool looksDisconnected(int rv) {
  return (rv < 10 || rv > 4085);
}

// ---------- CFG-Array ----------
ChannelCfg CFG[NUM_FINGERS][NUM_JOINTS] = {

  // ===== Thumb =====  ABAD=C0  PIP=C1  MCP=C2
  {
    { 2, 1300, 1840, false, 60,
      { -23.70745f, 1.31059f, -0.004162259f, -2.648236e-05f, -1.453945e-08f },
      { -23.70745f, 1.31059f, -0.004162259f, -2.648236e-05f, -1.453945e-08f },
      SPLIT_ON_X, 0.0f, DOMAIN_X01, 0.0f, 1.0f },
    { 0, 480, 690, false, 60,
      { -10.0f, 20.0f, 0.0f, 0.0f, 0.0f },
      { -10.0f, 20.0f, 0.0f, 0.0f, 0.0f },
      SPLIT_ON_X, 0.0f, DOMAIN_X01, 0.0f, 1.0f },
    { 1, 2350, 1930, false, 60,
      { -9467.363f, -383.1077f, -5.821462f, -0.03925621f, -9.89494e-05f },
      {  965732.1f,  47681.06f,   882.7746f,  7.263641f,   0.0224098f },
      SPLIT_ON_X, 0.0f, DOMAIN_POT_DEG, -70.0f, 5.0f },
  },

  // ===== Index =====  ABAD=C3  PIP=C4  MCP=C5
  {
    { 5, 1267, 1638, false, 60,
      { -1842.486f, -81.24415f, -1.339152f, -0.009748406f, -2.640092e-05f },
      {  6342.47f,   315.6875f,  5.904446f,  0.04934528f,   0.0001543762f },
      SPLIT_ON_X, 0.0f, DOMAIN_POT_DEG, -104.7f, 8.4f },
    { 3, 480, 690, false, 60,
      { -10.0f, 20.0f, 0.0f, 0.0f, 0.0f },
      { -10.0f, 20.0f, 0.0f, 0.0f, 0.0f },
      SPLIT_ON_X, 0.0f, DOMAIN_X01, 0.0f, 1.0f },
    { 4, 2178, 1751, true, 60,
      { -12906.32f, -626.7503f, -11.41882f, -0.09235105f, -0.0002794454f },
      {  116650.2f,  6345.167f,  129.4287f,   1.173521f,   0.003988579f },
      SPLIT_ON_X, 0.0f, DOMAIN_POT_DEG, -70.0f, 5.0f },
  },

  // ===== Middle =====  ABAD=C6  PIP=C7  MCP=C8
  {
    { 8, 1096, 1669, false, 60,
      { -5.539121f, 0.4814345f, -0.004400292f, 8.672651e-05f, 6.23142e-07f },
      { -5.539121f, 0.4814345f, -0.004400292f, 8.672651e-05f, 6.23142e-07f },
      SPLIT_ON_X, 0.0f, DOMAIN_X01, 0.0f, 1.0f },
    { 6, 480, 690, false, 60,
      { -10.0f, 20.0f, 0.0f, 0.0f, 0.0f },
      { -10.0f, 20.0f, 0.0f, 0.0f, 0.0f },
      SPLIT_ON_X, 0.0f, DOMAIN_X01, 0.0f, 1.0f },
    { 7, 2254, 1685, true, 60,
      { -69813.3f,  -4587.288f, -113.0147f, -1.236931f,  -0.005073531f },
      {  9562.313f,   568.5159f,  12.68115f,  0.1261018f,  0.0004695562f },
      SPLIT_ON_X, 0.0f, DOMAIN_POT_DEG, -70.0f, 5.0f },
  },

  // ===== Ring =====  ABAD=C9  PIP=C10  MCP=C11
  {
    { 11, 1149, 1606, false, 60,
      { -6434.994f, -328.1925f, -6.266193f, -0.05303145f, -0.0001677482f },
      {  4236.355f,  209.3545f,  3.893254f,  0.03244614f,  0.0001011802f },
      SPLIT_ON_X, 0.0f, DOMAIN_POT_DEG, -104.7f, 8.4f },
    { 9, 480, 690, false, 60,
      { -10.0f, 20.0f, 0.0f, 0.0f, 0.0f },
      { -10.0f, 20.0f, 0.0f, 0.0f, 0.0f },
      SPLIT_ON_X, 0.0f, DOMAIN_X01, 0.0f, 1.0f },
    { 10, 2213, 1691, true, 60,
      { -34346.65f, -1871.815f, -38.25078f, -0.347165f,  -0.001180238f },
      {  40152.47f,  2211.613f,  45.68855f,  0.4197539f,  0.001445171f },
      SPLIT_ON_X, 0.0f, DOMAIN_POT_DEG, -70.0f, 5.0f },
  },

  // ===== Pinky =====  ABAD=C12  PIP=C13  MCP=C14
  {
    { 14, 2209, 2108, false, 60,
      { -13392.78f, -487.2567f, -6.65029f, -0.04028025f, -9.124583e-05f },
      {  700437.4f,  30032.26f,  482.8671f,  3.450415f,   0.009244489f },
      SPLIT_ON_X, 0.0f, DOMAIN_POT_DEG, -104.7f, 8.4f },
    { 12, 480, 690, false, 60,
      { -10.0f, 20.0f, 0.0f, 0.0f, 0.0f },
      { -10.0f, 20.0f, 0.0f, 0.0f, 0.0f },
      SPLIT_ON_X, 0.0f, DOMAIN_X01, 0.0f, 1.0f },
    { 13, 1291, 1500, true, 60,
      { -32657.31f, -1365.693f, -21.42684f, -0.1493048f,  -0.0003895511f },
      { 4257414.0f,  204979.7f,  3700.792f,  29.69485f,    0.08934576f },
      SPLIT_ON_X, 0.0f, DOMAIN_POT_DEG, -70.0f, 5.0f },
  },

};

// ---------- Zustandsvariablen ----------
float filtDeg[NUM_FINGERS][NUM_JOINTS]          = {0};
float zeroOff[NUM_FINGERS][NUM_JOINTS]          = {0};
int   zeroRawRef[NUM_FINGERS][NUM_JOINTS]       = {0};
int   startupZeroRawRef[NUM_FINGERS][NUM_JOINTS]= {0};
unsigned long lastMs   = 0;
unsigned long lastPlot = 0;

enum Mode { MODE_NORMAL = 0, MODE_CSV };
Mode mode             = MODE_NORMAL;
bool csvHeaderPrinted = false;

// ---------- Serial UI ----------
void printHelp() {
  Serial.println(F("\nCommands:"));
  Serial.println(F("  n -> Normal mode"));
  Serial.println(F("  c -> CSV mode"));
  Serial.println(F("  z -> Nullpunkt setzen"));
  Serial.println(F("  r -> Nullpunkt zuruecksetzen"));
  Serial.println(F("  h -> Hilfe"));
  Serial.println();
}

void printCsvHeader() {
  Serial.print(F("t_ms"));
  for (int f = 0; f < NUM_FINGERS; ++f)
    for (int j = 0; j < NUM_JOINTS; ++j) {
      Serial.print(F(","));
      Serial.print(FINGER_NAME[f]);
      Serial.print(F("_"));
      Serial.print(JOINT_NAME[j]);
    }
  Serial.println();
}

void handleCommands() {
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\n' || c == '\r') continue;
    switch (c) {
      case 'n': case 'N':
        mode = MODE_NORMAL;
        Serial.println(F("[MODE] NORMAL"));
        break;
      case 'c': case 'C':
        mode = MODE_CSV;
        csvHeaderPrinted = false;
        Serial.println(F("[MODE] CSV"));
        break;
      case 'z': case 'Z':
        for (int f = 0; f < NUM_FINGERS; ++f)
          for (int j = 0; j < NUM_JOINTS; ++j) {
            int acc = 0;
            for (int k = 0; k < 5; ++k) acc += muxRead(CFG[f][j].pin);
            zeroRawRef[f][j] = acc / 5;
            zeroOff[f][j]    = 0.0f;
            filtDeg[f][j]    = 0.0f;
          }
        Serial.println(F("[ZERO] Nullpunkt gesetzt."));
        break;
      case 'r': case 'R':
        for (int f = 0; f < NUM_FINGERS; ++f)
          for (int j = 0; j < NUM_JOINTS; ++j) {
            zeroRawRef[f][j] = startupZeroRawRef[f][j];
            zeroOff[f][j]    = 0.0f;
            filtDeg[f][j]    = 0.0f;
          }
        Serial.println(F("[ZERO] Nullpunkt zurueckgesetzt."));
        break;
      case 'h': case 'H':
        printHelp();
        break;
      default:
        Serial.print(F("[WARN] Unbekannter Befehl: "));
        Serial.println(c);
        break;
    }
  }
}

// ---------- EMA-Filter ----------
float emaWithTau(float yPrev, float xNow, float tauMs, float dtMs) {
  if (tauMs <= 0.0f) return xNow;
  float alpha = clampf(dtMs / (tauMs + dtMs), 0.0f, 1.0f);
  return yPrev + alpha * (xNow - yPrev);
}

// ---------- Winkelberechnung ----------
float channelAngleDeg(const ChannelCfg& c, int raw, Joint j, int rawZeroRef) {
  if (looksDisconnected(raw)) return 0.0f;

  float deg = 0.0f;

  if (USE_LINEAR_CALIBRATION) {
    if (j == MCP_ABAD) {
      float spanPos = fabsf((float)c.rawMax - (float)rawZeroRef);
      float spanNeg = fabsf((float)c.rawMin - (float)rawZeroRef);
      if (spanPos < 1.0f && spanNeg < 1.0f) {
        deg = 0.0f;
      } else if (raw >= rawZeroRef) {
        deg = 10.0f * ((float)(raw - rawZeroRef) / max(1.0f, spanPos));
      } else {
        deg = -10.0f * ((float)(rawZeroRef - raw) / max(1.0f, spanNeg));
      }
    } else {
      float denom = (float)c.rawMax - (float)rawZeroRef;
      deg = (fabsf(denom) < 1.0f) ? 0.0f
          : -90.0f * ((float)(raw - rawZeroRef) / denom);
    }
  } else {
    float X = (c.domain == DOMAIN_X01)
              ? rawToX01(raw, c.rawMin, c.rawMax)
              : rawToPotiDeg(raw, c.rawMin, c.rawMax, c.potDegMin, c.potDegMax);
    if (c.splitKind == SPLIT_ON_X) {
      deg = (X <= c.splitVal) ? evaluatePoly(c.poly1, X)
                               : evaluatePoly(c.poly2, X);
    } else {
      float d1 = evaluatePoly(c.poly1, X);
      deg = (d1 <= c.splitVal) ? d1 : evaluatePoly(c.poly2, X);
    }
    if (c.negate) deg = -deg;
  }

  return (j == MCP_ABAD) ? clampf(deg, -10.0f, 10.0f)
                          : clampf(deg, -90.0f,  0.0f);
}

// ---------- WiFi ----------
void connectWiFi() {
  Serial.print(F("Verbinde mit WiFi: "));
  Serial.println(ssid);
  WiFi.mode(WIFI_STA);
  WiFi.disconnect(true);
  delay(300);
  WiFi.begin(ssid, pwd);
  for (int i = 0; i < 40 && WiFi.status() != WL_CONNECTED; ++i) {
    delay(500);
    Serial.print('.');
  }
  Serial.println();
  if (WiFi.status() == WL_CONNECTED) {
    udp.begin(port);
    serverIp = WiFi.broadcastIP(),
    Serial.print(F("WiFi verbunden. IP: "));
    Serial.println(WiFi.localIP());
  } else {
    Serial.println(F("WiFi-Verbindung fehlgeschlagen."));
  }
}

// ---------- UDP-Senden ----------
void sendGloveDataUDP(float tMcp, float tPip,
                      float iMcp, float iPip,
                      float mMcp, float mPip,
                      float rMcp, float rPip,
                      float lMcp, float lPip) {
  if (WiFi.status() != WL_CONNECTED) return;
  float values[11] = { 0, tMcp, tPip, iMcp, iPip,
                           mMcp, mPip, rMcp, rPip, lMcp, lPip };
  byte* buf = (byte*)values;
  buf[0] = 0xFF; buf[1] = 0x01; buf[2] = 0xFF; buf[3] = gloveId;
  udp.beginPacket(serverIp, port);
  udp.write(buf, sizeof(values));
  udp.endPacket();
}

// ---------- Setup ----------
void setup() {
  Serial.begin(BAUD);
  delay(300);

  pinMode(MUX_S0, OUTPUT);
  pinMode(MUX_S1, OUTPUT);
  pinMode(MUX_S2, OUTPUT);
  pinMode(MUX_S3, OUTPUT);
  selectMuxChannel(0);

  connectWiFi();

  Serial.println(F("SensinGlove – MUX-Mode"));
  Serial.print(F("ADC-Pin: GPIO ")); Serial.println(MUX_SIG_PIN);
  printHelp();

#if defined(ARDUINO_ARCH_ESP32)
  analogReadResolution(12);
  analogSetPinAttenuation(MUX_SIG_PIN, ADC_11db);
#endif

  lastMs = millis();
  for (int f = 0; f < NUM_FINGERS; ++f)
    for (int j = 0; j < NUM_JOINTS; ++j) {
      int raw = muxRead(CFG[f][j].pin);
      zeroRawRef[f][j]        = raw;
      startupZeroRawRef[f][j] = raw;
      filtDeg[f][j] = channelAngleDeg(CFG[f][j], raw, (Joint)j, raw);
    }
}

// ---------- Loop ----------
void loop() {
  handleCommands();

  unsigned long now = millis();
  float dtMs = (float)(now - lastMs);
  if (dtMs < 1.0f) dtMs = 1.0f;
  lastMs = now;

  int   raw[NUM_FINGERS][NUM_JOINTS];
  float deg[NUM_FINGERS][NUM_JOINTS];

  for (int f = 0; f < NUM_FINGERS; ++f) {
    for (int j = 0; j < NUM_JOINTS; ++j) {
      const ChannelCfg& c = CFG[f][j];
      int acc = 0;
      for (int k = 0; k < 3; ++k) acc += muxRead(c.pin);
      int rv    = acc / 3;
      raw[f][j] = rv;
      float angle = channelAngleDeg(c, rv, (Joint)j, zeroRawRef[f][j])
                    - zeroOff[f][j];
      filtDeg[f][j] = emaWithTau(filtDeg[f][j], angle, c.tauMs, dtMs);
      deg[f][j]     = filtDeg[f][j];
    }
  }

  if (mode == MODE_NORMAL) {
    if (now - lastPlot >= PLOT_INTERVAL) {
      lastPlot = now;
      sendGloveDataUDP(
        deg[THUMB][MCP_FLEX],  deg[THUMB][PIP_FLEX],
        deg[INDEX][MCP_FLEX],  deg[INDEX][PIP_FLEX],
        deg[MIDDLE][MCP_FLEX], deg[MIDDLE][PIP_FLEX],
        deg[RING][MCP_FLEX],   deg[RING][PIP_FLEX],
        deg[PINKY][MCP_FLEX],  deg[PINKY][PIP_FLEX]);
      Serial.println(F("------------------------------------------------"));
      Serial.print(F("t=")); Serial.print(now); Serial.println(F(" ms"));
      for (int f = 0; f < NUM_FINGERS; ++f) {
        Serial.print(FINGER_NAME[f]); Serial.print(F(": "));
        Serial.print(F("MCP raw="));  Serial.print(raw[f][MCP_FLEX]);
        Serial.print(F(" -> "));      Serial.print(deg[f][MCP_FLEX], 1);
        Serial.print(F("deg | AbAd raw=")); Serial.print(raw[f][MCP_ABAD]);
        Serial.print(F(" -> "));           Serial.print(deg[f][MCP_ABAD], 1);
        Serial.print(F("deg | PIP raw=")); Serial.print(raw[f][PIP_FLEX]);
        Serial.print(F(" -> "));           Serial.print(deg[f][PIP_FLEX], 1);
        Serial.println(F("deg"));
      }
    }
  } else {
    if (!csvHeaderPrinted) { printCsvHeader(); csvHeaderPrinted = true; }
    Serial.print(now);
    for (int f = 0; f < NUM_FINGERS; ++f)
      for (int j = 0; j < NUM_JOINTS; ++j) {
        Serial.print(',');
        Serial.print(deg[f][j], 3);
      }
    Serial.println();
  }

  delay(5);
}

// ---------- RTT-Ping-Echo ----------
// PC sendet 8-Byte-Ping: FE EE FE EE + uint32 id. Wir spiegeln ihn unveraendert
// an den Absender zurueck, damit der PC die Round-Trip-Zeit messen kann.
void handlePingEcho() {
  int plen = udp.parsePacket();
  if (plen >= 8) {
    byte pbuf[64];
    int n = udp.read(pbuf, sizeof(pbuf));
    if (n >= 8 && pbuf[0]==0xFE && pbuf[1]==0xEE && pbuf[2]==0xFE && pbuf[3]==0xEE) {
      IPAddress rip = udp.remoteIP();
      uint16_t rport = udp.remotePort();
      udp.beginPacket(rip, rport);
      udp.write(pbuf, n);
      udp.endPacket();
    }
  }
}
