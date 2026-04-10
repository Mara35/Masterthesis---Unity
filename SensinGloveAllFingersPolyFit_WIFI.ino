/*
 * Project:    SensinGlove – Full Hand readout for double Polynomial (ESP32)
 * File:       SensinGloveAllFingersPolyFit.ino
 * Version:    Arduino IDE 2.3.6
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
*/

#include <Arduino.h>
#if defined(ARDUINO_ARCH_ESP32)
#include "driver/adc.h"
#endif

#include <WiFi.h>
#include <WiFiUdp.h>

// ---------- WiFi / UDP ----------
const char *ssid = "LAPTOP-Mara";
const char *pwd  = "2Rc209@3";

WiFiUDP udp;
IPAddress serverIp(192,168,137,1);  
const int port = 9001;
const byte gloveId = 20;

// ---------- Configuration ----------
static const uint32_t BAUD = 115200;
const unsigned long PLOT_INTERVAL = 50; // ms -> ~20 Hz
const bool USE_LINEAR_CALIBRATION = true; // true = simple linear mapping instead of polynomial fit

// Polynomial degree (e.g. 4 -> 5 coefficients)
#define POLY_DEG 4
#define COEFFS (POLY_DEG + 1)

// Finger/Joints
enum Finger { THUMB=0, INDEX, MIDDLE, RING, PINKY, NUM_FINGERS };
enum Joint  { MCP_FLEX=0, MCP_ABAD, PIP_FLEX, NUM_JOINTS };

const char* FINGER_NAME[NUM_FINGERS] = { "Thumb","Index","Middle","Ring","Pinky" };
const char* JOINT_NAME [NUM_JOINTS ] = { "MCP","AbAd","PIP" };

// Split/Domain type
enum SplitKind  { SPLIT_ON_X=0, SPLIT_ON_ANGLE };
enum DomainKind { DOMAIN_X01=0, DOMAIN_POT_DEG };

// Channel configuration
struct ChannelCfg {
  int pin;          // ADC Pin
  int rawMin;       // ADC at min (e.g., extension end)
  int rawMax;       // ADC at max (e.g., flexion end)
  bool negate;      // Invert angle value?
  float tauMs;      // Filter time constant (ms), 0 = off
  // Piecewise polynomials (degree POLY_DEG)
  float poly1[COEFFS];
  float poly2[COEFFS];
  SplitKind  splitKind;
  float      splitVal;
  DomainKind domain;
  // If DOMAIN_POT_DEG: linear mapping raw -> potiDeg (corresponding to rawMin/rawMax)
  float potDegMin; 
  float potDegMax;
};

// ---------- Helper functions ----------
static inline float clampf(float v, float lo, float hi) {
  if (v < lo) return lo;
  if (v > hi) return hi;
  return v;
}

// Raw value -> x in [0..1], robust against swapped limits
float rawToX01(int raw, int rmin, int rmax) {
  if (rmax == rmin) return 0.0f;
  if (rmax < rmin) { int t=rmin; rmin=rmax; rmax=t; }
  float x = (float)(raw - rmin) / (float)(rmax - rmin);
  return clampf(x, 0.0f, 1.0f);
}

// Raw value -> potentiometer angle (degrees), robust against swapped raw limits
float rawToPotiDeg(int raw, int rmin, int rmax, float potMin, float potMax) {
  if (rmax == rmin) return potMin;
  if (rmax < rmin) {
    int t=rmin; rmin=rmax; rmax=t;
    float tp=potMin; potMin=potMax; potMax=tp;
  }
  float u = clampf((float)(raw - rmin) / (float)(rmax - rmin), 0.0f, 1.0f);
  return potMin + u * (potMax - potMin);
}

// Polynomial evaluation
float evaluatePoly(const float coeffs[COEFFS], float X) {
  float p = 0.0f;
  float xn = 1.0f;
  #pragma unroll
  for (int i=0; i<COEFFS; ++i) {
    p += coeffs[i] * xn;
    xn *= X;
  }
  return p;
}

static inline bool looksDisconnected(int rv) {
  return (rv < 10 || rv > 4085);
}

// ---------- Configuration: 15 Channels ----------
ChannelCfg CFG[NUM_FINGERS][NUM_JOINTS] = {
  // --- Thumb ---
  {
    // MCP_FLEX (x-based)
    { 32, 1300, 1840, false, 60,
      { -23.70745f, 1.31059f, -0.004162259f, -2.648236e-05f, -1.453945e-08f },
      { -23.70745f, 1.31059f, -0.004162259f, -2.648236e-05f, -1.453945e-08f },
      SPLIT_ON_X, 0.0f,
      DOMAIN_X01, 0.0f, 1.0f
    },
    // MCP_ABAD 
    { 36, 480, 690, false, 60,
      { -10.0f, 20.0f, 0, 0, 0 },
      { -10.0f, 20.0f, 0, 0, 0 },
      SPLIT_ON_X, 0.0f,
      DOMAIN_X01, 0.0f, 1.0f
    },
    // PIP/IP (Degree-Fit, relaxed range)
    { 39, 2350, 1930, false, 60,
      { -9467.363f, -383.1077f, -5.821462f, -0.03925621f, -9.89494e-05f },
      {  965732.1f,  47681.06f,   882.7746f,  7.263641f,   0.0224098f },
      SPLIT_ON_X, 0.0f,
      DOMAIN_POT_DEG, -70.0f, 5.0f
    },
  },
  // --- Index ---
  {
    // MCP_FLEX (Degree-Fit -> DOMAIN_POT_DEG) 
    { 35, 1267, 1638, false, 60,
      { -1842.486f, -81.24415f, -1.339152f, -0.009748406f, -2.640092e-05f },
      {  6342.47f,    315.6875f,  5.904446f,   0.04934528f,  0.0001543762f },
      SPLIT_ON_X, 0.0f,
      DOMAIN_POT_DEG, -104.7f, 8.4f
    },
    // MCP_ABAD (Dummy, x01)
    { 33, 480, 690, false, 60,
      { -10.0f, 20.0f, 0, 0, 0 },
      { -10.0f, 20.0f, 0, 0, 0 },
      SPLIT_ON_X, 0.0f,
      DOMAIN_X01, 0.0f, 1.0f
    },
    // PIP_FLEX (Degree-Fit, relaxed range)
    { 34, 2178, 1751, true, 60,
      { -12906.32f, -626.7503f, -11.41882f, -0.09235105f, -0.0002794454f },
      {  116650.2f,   6345.167f,   129.4287f,  1.173521f,   0.003988579f  },
      SPLIT_ON_X, 0.0f,
      DOMAIN_POT_DEG, -70.0f, 5.0f
    },
  },
  // --- Middle ---
  {
    // MCP_FLEX (x-based)
    { 25, 1096, 1669, false, 60,
      { -5.539121f, 0.4814345f, -0.004400292f, 8.672651e-05f, 6.23142e-07f },
      { -5.539121f, 0.4814345f, -0.004400292f, 8.672651e-05f, 6.23142e-07f },
      SPLIT_ON_X, 0.0f,
      DOMAIN_X01, 0.0f, 1.0f
    },
    // MCP_ABAD
    { 26, 480, 690, false, 60,
      { -10.0f, 20.0f, 0, 0, 0 },
      { -10.0f, 20.0f, 0, 0, 0 },
      SPLIT_ON_X, 0.0f,
      DOMAIN_X01, 0.0f, 1.0f
    },
    // PIP_FLEX (Degree-Fit, relaxed range)
    { 27, 2254, 1685, true, 60,
      { -69813.3f, -4587.288f, -113.0147f, -1.236931f, -0.005073531f },
      {   9562.313f,  568.5159f,   12.68115f,  0.1261018f,  0.0004695562f },
      SPLIT_ON_X, 0.0f,
      DOMAIN_POT_DEG, -70.0f, 5.0f
    },
  },
  // --- Ring ---
  {
    // MCP_FLEX (Degree-Fit -> DOMAIN_POT_DEG)
    { 14, 1149, 1606, false, 60,
      { -6434.994f, -328.1925f, -6.266193f, -0.05303145f, -0.0001677482f },
      {   4236.355f,  209.3545f,  3.893254f,  0.03244614f,  0.0001011802f },
      SPLIT_ON_X, 0.0f,
      DOMAIN_POT_DEG, -104.7f, 8.4f
    },
    // MCP_ABAD
    { 12, 480, 690, false, 60,
      { -10.0f, 20.0f, 0, 0, 0 },
      { -10.0f, 20.0f, 0, 0, 0 },
      SPLIT_ON_X, 0.0f,
      DOMAIN_X01, 0.0f, 1.0f
    },
    // PIP_FLEX (Degree-Fit, relaxed range)
    { 13, 2213, 1691, true, 60,
      { -34346.65f, -1871.815f, -38.25078f, -0.347165f, -0.001180238f },
      {   40152.47f,  2211.613f,  45.68855f,  0.4197539f,  0.001445171f },
      SPLIT_ON_X, 0.0f,
      DOMAIN_POT_DEG, -70.0f, 5.0f
    },
  },
  // --- Pinky ---
  {
    // MCP_FLEX (Degree-Fit -> DOMAIN_POT_DEG)
    {  4, 2209, 2108, false, 60,
      { -13392.78f, -487.2567f, -6.65029f, -0.04028025f, -9.124583e-05f },
      {  700437.4f,  30032.26f,  482.8671f,  3.450415f,   0.009244489f },
      SPLIT_ON_X, 0.0f,
      DOMAIN_POT_DEG, -104.7f, 8.4f
    },
    // MCP_ABAD
    {  2, 480, 690, false, 60,
      { -10.0f, 20.0f, 0, 0, 0 },
      { -10.0f, 20.0f, 0, 0, 0 },
      SPLIT_ON_X, 0.0f,
      DOMAIN_X01, 0.0f, 1.0f
    },
    // PIP_FLEX (Degree-Fit, relaxed range)
    { 15, 1291, 1500, true, 60,
      { -32657.31f, -1365.693f, -21.42684f, -0.1493048f, -0.0003895511f },
      { 4257414.0f,  204979.7f,   3700.792f,  29.69485f,   0.08934576f },
      SPLIT_ON_X, 0.0f,
      DOMAIN_POT_DEG, -70.0f, 5.0f
    },
  },
};

// ---------- Filter/Offsets/Status ----------
float filtDeg[NUM_FINGERS][NUM_JOINTS] = {0}; 
float zeroOff[NUM_FINGERS][NUM_JOINTS] = {0};  
int   zeroRawRef[NUM_FINGERS][NUM_JOINTS] = {0};
int   startupZeroRawRef[NUM_FINGERS][NUM_JOINTS] = {0};
unsigned long lastMs = 0;
unsigned long lastPlot = 0;

// Modes
enum Mode { MODE_NORMAL=0, MODE_CSV };
Mode mode = MODE_NORMAL;
bool csvHeaderPrinted = false;

// ---------- Serial UI ----------
void printHelp() {
  Serial.println(F("\nCommands:"));
  Serial.println(F("  n -> Normal mode (Raw + Angle)"));
  Serial.println(F("  c -> CSV mode (only Angle)"));
  Serial.println(F("  z -> Store current raw values as zero reference (0 deg)"));
  Serial.println(F("  r -> Reset zero reference to startup values"));
  Serial.println(F("  h -> Help"));
  Serial.println(F("\nNote: linear coarse calibration uses the current zero reference and CFG.rawMax as bend reference for flex joints."));
  Serial.println();
}

void printCsvHeader() {
  Serial.print(F("t_ms"));
  for (int f=0; f<NUM_FINGERS; ++f) {
    for (int j=0; j<NUM_JOINTS; ++j) {
      Serial.print(F(","));
      Serial.print(FINGER_NAME[f]); Serial.print(F("_")); Serial.print(JOINT_NAME[j]);
    }
  }
  Serial.println();
}

void handleCommands() {
  while (Serial.available()) {
    char c = Serial.read();
    if (c=='\n' || c=='\r') continue;
    switch (c) {
      case 'n': case 'N':
        mode = MODE_NORMAL; Serial.println(F("[MODE] NORMAL")); break;
      case 'c': case 'C':
        mode = MODE_CSV; csvHeaderPrinted = false; Serial.println(F("[MODE] CSV")); break;
      case 'z': case 'Z': {
        for (int f=0; f<NUM_FINGERS; ++f) {
          for (int j=0; j<NUM_JOINTS; ++j) {
            const ChannelCfg& c = CFG[f][j];
            int acc = 0; for (int k=0; k<5; ++k) acc += analogRead(c.pin);
            zeroRawRef[f][j] = acc / 5;
            zeroOff[f][j] = 0.0f;
            filtDeg[f][j] = 0.0f;
          }
        }
        Serial.println(F("[ZERO] Current raw values stored as 0 deg reference."));
        break;
      }
      case 'r': case 'R':
        for (int f=0; f<NUM_FINGERS; ++f)
          for (int j=0; j<NUM_JOINTS; ++j) {
            zeroRawRef[f][j] = startupZeroRawRef[f][j];
            zeroOff[f][j] = 0.0f;
            filtDeg[f][j] = 0.0f;
          }
        Serial.println(F("[ZERO] Reset zero reference to startup values."));
        break;
      case 'h': case 'H': printHelp(); break;
      default:
        Serial.print(F("[WARN] Unknown command: ")); Serial.println(c);
        break;
    }
  }
}

// EMA with Tau (ms): alpha = dt/(tau+dt)
float emaWithTau(float yPrev, float xNow, float tauMs, float dtMs) {
  if (tauMs <= 0.0f) return xNow;
  float alpha = dtMs / (tauMs + dtMs);
  alpha = clampf(alpha, 0.0f, 1.0f);
  return yPrev + alpha * (xNow - yPrev);
}

// ---------- Evaluation per channel ----------
float channelAngleDeg(const ChannelCfg& c, int raw, Joint j, int rawZeroRef) {
  if (looksDisconnected(raw)) return 0.0f; // disconnected -> 0°

  float deg = 0.0f;

  if (USE_LINEAR_CALIBRATION) {
    // Linear coarse calibration with dynamic zero reference:
    // current zero reference -> 0 deg
    // configured bend reference (CFG.rawMax) -> -90 deg for flex joints
    // Ab/Ad uses zero reference as center and the farther configured endpoint as +/-10 deg
    if (j == MCP_ABAD) {
      float spanPos = fabsf((float)c.rawMax - (float)rawZeroRef);
      float spanNeg = fabsf((float)c.rawMin - (float)rawZeroRef);
      if (spanPos < 1.0f && spanNeg < 1.0f) {
        deg = 0.0f;
      } else if (raw >= rawZeroRef) {
        float span = max(1.0f, spanPos);
        deg = 10.0f * ((float)(raw - rawZeroRef) / span);
      } else {
        float span = max(1.0f, spanNeg);
        deg = -10.0f * ((float)(rawZeroRef - raw) / span);
      }
    } else {
      float denom = (float)c.rawMax - (float)rawZeroRef;
      if (fabsf(denom) < 1.0f) {
        deg = 0.0f;
      } else {
        deg = -90.0f * ((float)(raw - rawZeroRef) / denom);
      }
    }
  } else {
    // Domain: X = x01 or Poti degree
    float X = (c.domain == DOMAIN_X01)
                ? rawToX01(raw, c.rawMin, c.rawMax)
                : rawToPotiDeg(raw, c.rawMin, c.rawMax, c.potDegMin, c.potDegMax);

    // Choose piecewise polynomial
    if (c.splitKind == SPLIT_ON_X) {
      deg = (X <= c.splitVal) ? evaluatePoly(c.poly1, X) : evaluatePoly(c.poly2, X);
    } else { // SPLIT_ON_ANGLE (heuristic)
      float d1 = evaluatePoly(c.poly1, X);
      deg = (d1 <= c.splitVal) ? d1 : evaluatePoly(c.poly2, X);
    }

    if (c.negate) deg = -deg;
  }

  // Safety-clamp: Flex joints -> [-90, 0], AbAd -> [-10, 10]
  if (j == MCP_ABAD) {
    deg = clampf(deg, -10.0f, 10.0f);
  } else {
    deg = clampf(deg, -90.0f, 0.0f);
  }
  return deg;
}

void connectWiFi() {
  Serial.print("Connecting to WiFi: ");
  Serial.println(ssid);

  WiFi.mode(WIFI_STA);
  WiFi.disconnect(true);
  delay(300);
  WiFi.begin(ssid, pwd);

  int tries = 0;
  while (WiFi.status() != WL_CONNECTED && tries < 40) {
    delay(500);
    Serial.print(".");
    tries++;
  }
  Serial.println();

  if (WiFi.status() == WL_CONNECTED) {
    udp.begin(port);
    Serial.println("WiFi connected");
    Serial.print("Local IP: ");
    Serial.println(WiFi.localIP());
  } else {
    Serial.println("WiFi connection failed");
  }
}

void sendGloveDataUDP(
  float thumbMcp, float thumbPip,
  float indexMcp, float indexPip,
  float middleMcp, float middlePip,
  float ringMcp, float ringPip,
  float littleMcp, float littlePip
) {
  if (WiFi.status() != WL_CONNECTED) return;

  float values[11];
  values[0] = 0.0f; // wird durch Header überschrieben

  values[1]  = thumbMcp;
  values[2]  = thumbPip;
  values[3]  = indexMcp;
  values[4]  = indexPip;
  values[5]  = middleMcp;
  values[6]  = middlePip;
  values[7]  = ringMcp;
  values[8]  = ringPip;
  values[9]  = littleMcp;
  values[10] = littlePip;

  byte* buffer = (byte*)values;
  memcpy(buffer, (byte[]){0xFF, 0x01, 0xFF, gloveId}, 4);

  udp.beginPacket(serverIp, port);
  udp.write(buffer, sizeof(values));
  udp.endPacket();
}

// ---------- Setup / Loop ----------
void setup() {
  Serial.begin(BAUD);
  delay(300);

  connectWiFi();
  while (!Serial) {}
  if (USE_LINEAR_CALIBRATION) {
    Serial.println(F("ESP32 Hand – Linear coarse calibration"));
  } else {
    Serial.println(F("ESP32 Hand – Piecewise polynomials with domain input & stable split"));
  }
  printHelp();

  #if defined(ARDUINO_ARCH_ESP32)
    analogReadResolution(12);             
    analogSetWidth(12);
    analogSetAttenuation(ADC_11db);     
  #endif

  // Initialize starting values
  lastMs = millis();
  for (int f=0; f<NUM_FINGERS; ++f) {
    for (int j=0; j<NUM_JOINTS; ++j) {
      const ChannelCfg& c = CFG[f][j];
      int raw = analogRead(c.pin);
      zeroRawRef[f][j] = raw;
      startupZeroRawRef[f][j] = raw;
      float deg = channelAngleDeg(c, raw, (Joint)j, zeroRawRef[f][j]) - zeroOff[f][j];
      filtDeg[f][j] = deg;
    }
  }
}

void loop() {
  handleCommands();

  unsigned long now = millis();
  float dtMs = (float)(now - lastMs);
  if (dtMs < 1.0f) dtMs = 1.0f;   // numerical safety
  lastMs = now;

  int   raw[NUM_FINGERS][NUM_JOINTS];
  float deg[NUM_FINGERS][NUM_JOINTS];

  // Read and compute all channels
  for (int f=0; f<NUM_FINGERS; ++f) {
    for (int j=0; j<NUM_JOINTS; ++j) {
      const ChannelCfg& c = CFG[f][j];

      // Small averaging (3x)
      int acc=0; for (int k=0;k<3;k++) acc += analogRead(c.pin);
      int rv = acc/3;
      raw[f][j] = rv;

      float angle = channelAngleDeg(c, rv, (Joint)j, zeroRawRef[f][j]) - zeroOff[f][j];
      float y = emaWithTau(filtDeg[f][j], angle, c.tauMs, dtMs);
      filtDeg[f][j] = y;
      deg[f][j] = y;
    }
  }

  if (mode == MODE_NORMAL) {
    // Print every PLOT_INTERVAL ms (to reduce traffic)
    if (now - lastPlot >= PLOT_INTERVAL) {
      lastPlot = now;

      sendGloveDataUDP(
      deg[THUMB][MCP_FLEX],  deg[THUMB][PIP_FLEX],
      deg[INDEX][MCP_FLEX],  deg[INDEX][PIP_FLEX],
      deg[MIDDLE][MCP_FLEX], deg[MIDDLE][PIP_FLEX],
      deg[RING][MCP_FLEX],   deg[RING][PIP_FLEX],
      deg[PINKY][MCP_FLEX],  deg[PINKY][PIP_FLEX]
    );
    
      Serial.println(F("------------------------------------------------"));
      Serial.print(F("t=")); Serial.print(now); Serial.println(F(" ms"));
      for (int f=0; f<NUM_FINGERS; ++f) {
        Serial.print(FINGER_NAME[f]); Serial.print(F(": "));
        // MCP
        Serial.print(F("MCP raw=")); Serial.print(raw[f][MCP_FLEX]);
        Serial.print(F(" -> "));     Serial.print(deg[f][MCP_FLEX], 1); Serial.print(F("° | "));
        // AbAd
        Serial.print(F("AbAd raw=")); Serial.print(raw[f][MCP_ABAD]);
        Serial.print(F(" -> "));      Serial.print(deg[f][MCP_ABAD], 1); Serial.print(F("° | "));
        // PIP
        Serial.print(F("PIP raw="));  Serial.print(raw[f][PIP_FLEX]);
        Serial.print(F(" -> "));      Serial.print(deg[f][PIP_FLEX], 1); Serial.println(F("°"));
      }
    }
  } else {
    // CSV mode: one line per loop
    if (!csvHeaderPrinted) { printCsvHeader(); csvHeaderPrinted = true; }
    Serial.print(now);
    for (int f=0; f<NUM_FINGERS; ++f) {
      for (int j=0; j<NUM_JOINTS; ++j) {
        Serial.print(F(",")); Serial.print(deg[f][j], 3);
      }
    }
    Serial.println();
  }

  delay(5);
}
