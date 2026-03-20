using System;
using UnityEngine;

public class GlovePlaybackHandController : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Serializable]
    public class JointBinding
    {
        public Transform target;
        public Axis axis = Axis.Z;
        public float multiplier = 1f;
        public float offset = 0f;
        public Vector3 baseEuler;
    }

    [Serializable]
    public class FingerBindings
    {
        public JointBinding mcpFlex;
        public JointBinding mcpAbAd;
        public JointBinding pipFlex;
    }

    [SerializeField] private GlovePlaybackSource playbackSource;
    [SerializeField] private FingerBindings thumb;
    [SerializeField] private FingerBindings index;
    [SerializeField] private FingerBindings middle;
    [SerializeField] private FingerBindings ring;
    [SerializeField] private FingerBindings pinky;

    private FingerBindings[] _fingers;

    private void Awake()
    {
        _fingers = new[] { thumb, index, middle, ring, pinky };
    }

    private void Start()
    {
        CacheBaseRotations();
    }

    [ContextMenu("Cache Base Rotations")]
    public void CacheBaseRotations()
    {
        foreach (var finger in _fingers)
        {
            CacheBinding(finger?.mcpFlex);
            CacheBinding(finger?.mcpAbAd);
            CacheBinding(finger?.pipFlex);
        }
    }

    private static void CacheBinding(JointBinding binding)
    {
        if (binding?.target == null) return;
        binding.baseEuler = binding.target.localEulerAngles;
    }

    private void Update()
    {
        if (playbackSource == null || !playbackSource.HasData)
            return;

        var angles = playbackSource.CurrentAngles;

        ApplyFinger(index, angles, 1);
    }

    private void ApplyFinger(FingerBindings finger, float[] angles, int fingerIndex)
    {
        if (finger == null) return;

        float mcpFlex = angles[fingerIndex * 3 + 0];
        float mcpAbAd = angles[fingerIndex * 3 + 1];
        float pipFlex = angles[fingerIndex * 3 + 2];

        ApplyCombinedMcp(finger.mcpFlex, finger.mcpAbAd, mcpFlex, mcpAbAd);
        ApplyBinding(finger.pipFlex, pipFlex);
    }

    private static void ApplyCombinedMcp(
        JointBinding flexBinding,
        JointBinding abadBinding,
        float flexDeg,
        float abadDeg)
    {
        Transform target = flexBinding?.target != null ? flexBinding.target : abadBinding?.target;
        if (target == null) return;

        Vector3 e = flexBinding != null ? flexBinding.baseEuler :
                    abadBinding != null ? abadBinding.baseEuler : Vector3.zero;

        if (flexBinding != null)
        {
            float v = flexBinding.offset + flexDeg * flexBinding.multiplier;
            switch (flexBinding.axis)
            {
                case Axis.X: e.x = flexBinding.baseEuler.x + v; break;
                case Axis.Y: e.y = flexBinding.baseEuler.y + v; break;
                case Axis.Z: e.z = flexBinding.baseEuler.z + v; break;
            }
        }

        if (abadBinding != null)
        {
            float v = abadBinding.offset + abadDeg * abadBinding.multiplier;
            switch (abadBinding.axis)
            {
                case Axis.X: e.x = abadBinding.baseEuler.x + v; break;
                case Axis.Y: e.y = abadBinding.baseEuler.y + v; break;
                case Axis.Z: e.z = abadBinding.baseEuler.z + v; break;
            }
        }

        target.localRotation = Quaternion.Euler(e);
    }

    private static void ApplyBinding(JointBinding binding, float sourceAngleDeg)
    {
        if (binding?.target == null) return;

        float value = binding.offset + sourceAngleDeg * binding.multiplier;
        Vector3 e = binding.baseEuler;

        switch (binding.axis)
        {
            case Axis.X: e.x = binding.baseEuler.x + value; break;
            case Axis.Y: e.y = binding.baseEuler.y + value; break;
            case Axis.Z: e.z = binding.baseEuler.z + value; break;
        }

        binding.target.localRotation = Quaternion.Euler(e);
    }
}