using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Globalization;

/// <summary>
/// Reads CSV data and controls the XBot.
/// FILES: Assets/Data/synthetic_glove.csv + synthetic_imu.csv
/// </summary>
public class CSVReplayController : MonoBehaviour
{
    [Header("--- Activation ---")]
    public bool useCSVReplay = true;
    [Tooltip("Keyboard Control Script – is disabled when CSV is active")]
    public HandTargetKeyboardControl keyboardControl;

    [Header("--- CSV files (Assets/Data/) ---")]
    public string gloveFileName = "synthetic_glove.csv";
    public string imuFileName = "synthetic_imu.csv";

    [Header("--- XBot Bones ---")]
    public Transform upperArmBone;   // mixamorig:RightArm
    public Transform foreArmBone;    // mixamorig:RightForeArm
    public Transform handBone;       // mixamorig:RightHand
    public Transform torsoBone;      // mixamorig:Spine2
    public Transform headBone;       // mixamorig:Head

    [Header("--- Finger Bones (each finger: [0]=MCP [1]=PIP) ---")]
    public Transform[] thumbBones;
    public Transform[] indexBones;
    public Transform[] middleBones;
    public Transform[] ringBones;
    public Transform[] pinkyBones;

    [Header("--- Finger Axis ---")]
    [Tooltip("Local axis around which the fingers are bent.")]
    public Vector3 fingerFlexAxis = Vector3.right;
    [Tooltip("Separate axis for the thumb.")]
    public Vector3 thumbFlexAxis = new Vector3(0, 0, 1);

    [Header("--- HandTarget (IK Target) ---")]
    public Transform handTarget;

    [Header("--- Automatic Gripping ---")]
    [Tooltip("GloveGrabber script on HandTarget – calculates finger angles")]
    public GloveGrabber gloveGrabber;

    [Header("--- Playback ---")]
    public float playbackSpeed = 1f;
    public bool loop = true;
    public float loopPauseSeconds = 1f;

    [Header("--- Debug ---")]
    public bool showDebugGUI = true;

    // ---- Internal data ----
    struct GloveFrame
    {
        public float t;
        public float[] angles; 
    }

    struct ImuFrame
    {
        public float t;
        public Quaternion upperArm, foreArm, torso, head;
    }

    List<GloveFrame> gloveFrames = new List<GloveFrame>();
    List<ImuFrame> imuFrames = new List<ImuFrame>();

    float playbackTime = 0f;
    float maxTime = 0f;
    bool isPlaying = false;
    bool isPaused = false;
    int currentGloveIdx = 0;
    int currentImuIdx = 0;

    // T-Pose Reference Rotations (saved in Start())
    Quaternion upperArmRest, foreArmRest, torsoRest, headRest;
    Dictionary<Transform, Quaternion> fingerRestPose = new Dictionary<Transform, Quaternion>();

    void Start()
    {
        if (!useCSVReplay) return;

        if (keyboardControl != null)
            keyboardControl.enabled = false;

        // Remember T-pose rotations
        if (upperArmBone) upperArmRest = upperArmBone.localRotation;
        if (foreArmBone) foreArmRest = foreArmBone.localRotation;
        if (torsoBone) torsoRest = torsoBone.localRotation;
        if (headBone) headRest = headBone.localRotation;

        // Remember finger-rest positions
        StoreFingerRest(thumbBones);
        StoreFingerRest(indexBones);
        StoreFingerRest(middleBones);
        StoreFingerRest(ringBones);
        StoreFingerRest(pinkyBones);

        LoadCSVFiles();

        if (gloveFrames.Count > 0 && imuFrames.Count > 0)
        {
            maxTime = Mathf.Max(
                gloveFrames[gloveFrames.Count - 1].t,
                imuFrames[imuFrames.Count - 1].t);
            isPlaying = true;
            Debug.Log($"[CSVReplay] Loaded: {gloveFrames.Count} Glove-Frames, " +
                      $"{imuFrames.Count} IMU-Frames, Duration={maxTime:F1}s");
        }
        else
        {
            Debug.LogError("[CSVReplay] CSV files not found! Assets/Data");
        }
    }

    void StoreFingerRest(Transform[] bones)
    {
        if (bones == null) return;
        foreach (var b in bones)
            if (b != null && !fingerRestPose.ContainsKey(b))
                fingerRestPose[b] = b.localRotation;
    }

    void LoadCSVFiles()
    {
        string glovePath = Path.Combine(Application.dataPath, "Data", gloveFileName);
        string imuPath = Path.Combine(Application.dataPath, "Data", imuFileName);

        // Glove CSV
        if (File.Exists(glovePath))
        {
            bool firstLine = true;
            float t0 = -1f;
            foreach (var line in File.ReadLines(glovePath))
            {
                if (firstLine) { firstLine = false; continue; }
                var cols = line.Split(',');
                if (cols.Length < 16) continue;
                float t = float.Parse(cols[0], CultureInfo.InvariantCulture);
                if (t0 < 0) t0 = t;
                var frame = new GloveFrame
                {
                    t = (t - t0) / 1000f,
                    angles = new float[15]
                };
                for (int i = 0; i < 15; i++)
                    frame.angles[i] = float.Parse(cols[i + 1], CultureInfo.InvariantCulture);
                gloveFrames.Add(frame);
            }
        }
        else Debug.LogError("[CSVReplay] Glove CSV not found: " + glovePath);

        // IMU CSV
        if (File.Exists(imuPath))
        {
            bool firstLine = true;
            float t0 = -1f;
            foreach (var line in File.ReadLines(imuPath))
            {
                if (firstLine) { firstLine = false; continue; }
                var cols = line.Split(',');
                if (cols.Length < 17) continue;
                float t = float.Parse(cols[0], CultureInfo.InvariantCulture);
                if (t0 < 0) t0 = t;

                Quaternion ParseQ(int b)
                {
                    float x = float.Parse(cols[b], CultureInfo.InvariantCulture);
                    float y = float.Parse(cols[b + 1], CultureInfo.InvariantCulture);
                    float z = float.Parse(cols[b + 2], CultureInfo.InvariantCulture);
                    float w = float.Parse(cols[b + 3], CultureInfo.InvariantCulture);
                    var q = new Quaternion(x, y, z, w);
                    float mag = x * x + y * y + z * z + w * w;
                    return mag > 0.01f ? q.normalized : Quaternion.identity;
                }

                imuFrames.Add(new ImuFrame
                {
                    t = (t - t0) / 1000f,
                    upperArm = ParseQ(1),
                    foreArm = ParseQ(5),
                    torso = ParseQ(9),
                    head = ParseQ(13)
                });
            }
        }
        else Debug.LogError("[CSVReplay] IMU CSV not found: " + imuPath);
    }

    void Update()
    {
        if (!useCSVReplay || !isPlaying || isPaused) return;

        playbackTime += Time.deltaTime * playbackSpeed;

        if (playbackTime >= maxTime)
        {
            if (loop) StartCoroutine(LoopPause());
            else { playbackTime = maxTime; isPlaying = false; }
        }
    }

    // Does LateUpdate run AFTER the Animator? Bone rotations are not overwritten
    void LateUpdate()
    {
        if (!useCSVReplay || !isPlaying || isPaused) return;

        ApplyImuFrame();
        ApplyGloveFrame();
        UpdateHandTarget();
    }

    // ---- Arm / Torso / Head ----
    void ApplyImuFrame()
    {
        while (currentImuIdx < imuFrames.Count - 2 &&
               imuFrames[currentImuIdx + 1].t <= playbackTime)
            currentImuIdx++;

        int i0 = currentImuIdx;
        int i1 = Mathf.Min(i0 + 1, imuFrames.Count - 1);
        float span = imuFrames[i1].t - imuFrames[i0].t;
        float a = span > 0.0001f ? (playbackTime - imuFrames[i0].t) / span : 1f;

        if (upperArmBone)
            upperArmBone.localRotation =
                Quaternion.Slerp(imuFrames[i0].upperArm, imuFrames[i1].upperArm, a);

        if (foreArmBone)
            foreArmBone.localRotation =
                Quaternion.Slerp(imuFrames[i0].foreArm, imuFrames[i1].foreArm, a);

        if (torsoBone)
            torsoBone.localRotation =
                Quaternion.Slerp(imuFrames[i0].torso, imuFrames[i1].torso, a);

        if (headBone)
            headBone.localRotation =
                Quaternion.Slerp(imuFrames[i0].head, imuFrames[i1].head, a);
    }

    // ---- Finger ----
    void ApplyGloveFrame()
    {
        while (currentGloveIdx < gloveFrames.Count - 2 &&
               gloveFrames[currentGloveIdx + 1].t <= playbackTime)
            currentGloveIdx++;

        int i0 = currentGloveIdx;
        int i1 = Mathf.Min(i0 + 1, gloveFrames.Count - 1);
        float span = gloveFrames[i1].t - gloveFrames[i0].t;
        float a = span > 0.0001f ? (playbackTime - gloveFrames[i0].t) / span : 1f;

        float[] angles = new float[15];
        for (int i = 0; i < 15; i++)
            angles[i] = Mathf.Lerp(gloveFrames[i0].angles[i], gloveFrames[i1].angles[i], a);

        // Layout: Thumb(0-2), Index(3-5), Middle(6-8), Ring(9-11), Pinky(12-14)
        // each Finger: [MCP_Flex, AbAd, PIP_Flex]
        ApplyFinger(thumbBones, angles[0], angles[2], thumbFlexAxis);
        ApplyFinger(indexBones, angles[3], angles[5], fingerFlexAxis);
        ApplyFinger(middleBones, angles[6], angles[8], fingerFlexAxis);
        ApplyFinger(ringBones, angles[9], angles[11], fingerFlexAxis);
        ApplyFinger(pinkyBones, angles[12], angles[14], fingerFlexAxis);

        // Pass the angle to GloveGrabber for automatic gripping
        if (gloveGrabber != null)
        {
            gloveGrabber.currentIndexMcp = angles[3];
            gloveGrabber.currentIndexPip = angles[5];
            gloveGrabber.currentMiddleMcp = angles[6];
            gloveGrabber.currentMiddlePip = angles[8];
            gloveGrabber.currentRingMcp = angles[9];
            gloveGrabber.currentRingPip = angles[11];
            gloveGrabber.currentPinkyMcp = angles[12];
            gloveGrabber.currentPinkyPip = angles[14];
        }
    }

    void ApplyFinger(Transform[] bones, float mcpDeg, float pipDeg, Vector3 flexAxis)
    {
        if (bones == null) return;

        // Resting pose + flexion around a configurable axis
        if (bones.Length > 0 && bones[0] != null)
        {
            Quaternion rest = fingerRestPose.ContainsKey(bones[0])
                ? fingerRestPose[bones[0]] : Quaternion.identity;
            bones[0].localRotation = rest * Quaternion.AngleAxis(-mcpDeg, flexAxis);
        }
        if (bones.Length > 1 && bones[1] != null)
        {
            Quaternion rest = fingerRestPose.ContainsKey(bones[1])
                ? fingerRestPose[bones[1]] : Quaternion.identity;
            bones[1].localRotation = rest * Quaternion.AngleAxis(-pipDeg, flexAxis);
        }
    }

    void UpdateHandTarget()
    {
        if (handTarget != null && handBone != null)
            handTarget.position = handBone.position;
    }

    IEnumerator LoopPause()
    {
        isPlaying = false;
        yield return new WaitForSeconds(loopPauseSeconds);
        playbackTime = 0f;
        currentGloveIdx = 0;
        currentImuIdx = 0;
        isPlaying = true;
    }

    void OnGUI()
    {
        if (!showDebugGUI) return;
        GUILayout.BeginArea(new Rect(10, 10, 300, 130));
        GUILayout.Label($"[CSVReplay] t={playbackTime:F2}s / {maxTime:F2}s");
        GUILayout.Label($"Playing: {isPlaying}  Speed: {playbackSpeed:F1}x");
        if (GUILayout.Button(isPaused ? "? Resume" : "? Pause"))
            isPaused = !isPaused;
        if (GUILayout.Button("? Restart"))
        {
            playbackTime = 0f; currentGloveIdx = 0; currentImuIdx = 0;
            isPlaying = true; isPaused = false;
        }
        GUILayout.EndArea();
    }
}