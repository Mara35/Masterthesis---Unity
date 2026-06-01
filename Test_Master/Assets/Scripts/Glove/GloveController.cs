using UnityEngine;

public class GloveController : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private UDPCommunicationGlove udpCommunication;
    [SerializeField] private int gloveId = 20;

    [Header("Grabber")]
    [SerializeField] private GloveGrabber gloveGrabber;

    [Header("Thumb")]
    [SerializeField] private Transform thumbMCP;
    [SerializeField] private Transform thumbPIP;

    [Header("Index")]
    [SerializeField] private Transform indexMCP;
    [SerializeField] private Transform indexPIP;

    [Header("Middle")]
    [SerializeField] private Transform middleMCP;
    [SerializeField] private Transform middlePIP;

    [Header("Ring")]
    [SerializeField] private Transform ringMCP;
    [SerializeField] private Transform ringPIP;

    [Header("Pinky")]
    [SerializeField] private Transform pinkyMCP;
    [SerializeField] private Transform pinkyPIP;

    [Header("Rotation Axis")]
    [SerializeField] private FingerAxis thumbMcpAxis = FingerAxis.Z;
    [SerializeField] private FingerAxis thumbPipAxis = FingerAxis.Z;

    [SerializeField] private FingerAxis fingerMcpAxis = FingerAxis.X;
    [SerializeField] private FingerAxis fingerPipAxis = FingerAxis.X;

    [Header("Scaling / Offset")]
    [SerializeField] private float thumbMcpScale = 1f;
    [SerializeField] private float thumbPipScale = 1f;
    [SerializeField] private float indexMcpScale = 1f;
    [SerializeField] private float indexPipScale = 1f;
    [SerializeField] private float middleMcpScale = 1f;
    [SerializeField] private float middlePipScale = 1f;
    [SerializeField] private float ringMcpScale = 1f;
    [SerializeField] private float ringPipScale = 1f;
    [SerializeField] private float pinkyMcpScale = 1f;
    [SerializeField] private float pinkyPipScale = 1f;

    [SerializeField] private float thumbMcpOffset = 0f;
    [SerializeField] private float thumbPipOffset = 0f;
    [SerializeField] private float indexMcpOffset = 0f;
    [SerializeField] private float indexPipOffset = 0f;
    [SerializeField] private float middleMcpOffset = 0f;
    [SerializeField] private float middlePipOffset = 0f;
    [SerializeField] private float ringMcpOffset = 0f;
    [SerializeField] private float ringPipOffset = 0f;
    [SerializeField] private float pinkyMcpOffset = 0f;
    [SerializeField] private float pinkyPipOffset = 0f;

    [Header("Invert")]
    [SerializeField] private bool invertThumbMCP = false;
    [SerializeField] private bool invertThumbPIP = false;
    [SerializeField] private bool invertIndexMCP = false;
    [SerializeField] private bool invertIndexPIP = false;
    [SerializeField] private bool invertMiddleMCP = false;
    [SerializeField] private bool invertMiddlePIP = false;
    [SerializeField] private bool invertRingMCP = false;
    [SerializeField] private bool invertRingPIP = false;
    [SerializeField] private bool invertPinkyMCP = false;
    [SerializeField] private bool invertPinkyPIP = false;

    [Header("Debug")]
    [SerializeField] private bool logValues = false;

    private enum FingerAxis
    {
        X,
        Y,
        Z
    }

    private void Awake()
    {
        if (udpCommunication == null)
            udpCommunication = FindFirstObjectByType<UDPCommunicationGlove>();
    }

    private void Update()
    {
        if (udpCommunication == null)
            return;

        if (!udpCommunication.TryGetGloveData(gloveId, out var glove))
            return;

        ApplyFingerRotation(thumbMCP, glove.Thumb_MCP, thumbMcpScale, thumbMcpOffset, invertThumbMCP, thumbMcpAxis);
        ApplyFingerRotation(thumbPIP, glove.Thumb_PIP, thumbPipScale, thumbPipOffset, invertThumbPIP, thumbPipAxis);

        ApplyFingerRotation(indexMCP, glove.Index_MCP, indexMcpScale, indexMcpOffset, invertIndexMCP, fingerMcpAxis);
        ApplyFingerRotation(indexPIP, glove.Index_PIP, indexPipScale, indexPipOffset, invertIndexPIP, fingerPipAxis);

        ApplyFingerRotation(middleMCP, glove.Middle_MCP, middleMcpScale, middleMcpOffset, invertMiddleMCP, fingerMcpAxis);
        ApplyFingerRotation(middlePIP, glove.Middle_PIP, middlePipScale, middlePipOffset, invertMiddlePIP, fingerPipAxis);

        ApplyFingerRotation(ringMCP, glove.Ring_MCP, ringMcpScale, ringMcpOffset, invertRingMCP, fingerMcpAxis);
        ApplyFingerRotation(ringPIP, glove.Ring_PIP, ringPipScale, ringPipOffset, invertRingPIP, fingerPipAxis);

        ApplyFingerRotation(pinkyMCP, glove.Pinky_MCP, pinkyMcpScale, pinkyMcpOffset, invertPinkyMCP, fingerMcpAxis);
        ApplyFingerRotation(pinkyPIP, glove.Pinky_PIP, pinkyPipScale, pinkyPipOffset, invertPinkyPIP, fingerPipAxis);

     
        if (logValues)
        {
            Debug.Log(
                $"Glove {glove.Id} | " +
                $"Index MCP: {glove.Index_MCP:F1}, PIP: {glove.Index_PIP:F1} | " +
                $"Middle MCP: {glove.Middle_MCP:F1}, PIP: {glove.Middle_PIP:F1}"
            );
        }

        // Passes the finger angle to GloveGrabber
        if (gloveGrabber != null)
        {
            gloveGrabber.currentIndexMcp = glove.Index_MCP;
            gloveGrabber.currentIndexPip = glove.Index_PIP;
            gloveGrabber.currentMiddleMcp = glove.Middle_MCP;
            gloveGrabber.currentMiddlePip = glove.Middle_PIP;
            gloveGrabber.currentRingMcp = glove.Ring_MCP;
            gloveGrabber.currentRingPip = glove.Ring_PIP;
            gloveGrabber.currentPinkyMcp = glove.Pinky_MCP;
            gloveGrabber.currentPinkyPip = glove.Pinky_PIP;
        }
    }

    private void ApplyFingerRotation(Transform joint, float rawValue, float scale, float offset, bool invert, FingerAxis axis)
    {
        if (joint == null)
            return;

        float angle = rawValue * scale + offset;

        if (invert)
            angle = -angle;

        joint.localRotation = CreateRotation(angle, axis);
    }

    private Quaternion CreateRotation(float angle, FingerAxis axis)
    {
        return axis switch
        {
            FingerAxis.X => Quaternion.Euler(angle, 0f, 0f),
            FingerAxis.Y => Quaternion.Euler(0f, angle, 0f),
            FingerAxis.Z => Quaternion.Euler(0f, 0f, angle),
            _ => Quaternion.identity
        };
    }
};
    