using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// <see cref="MonoBehaviour"/> to hold the information for all the sensors and the mappings to their data in the unity scene.
/// This is stored as a List of <see cref="StreamSensorObject"/>s
/// </summary>
public class StreamSensorModel : MonoBehaviour
{

    /// <summary>
    /// representation of the data of a streamsensor used by the unity scene to set it up.
    /// Includes the sensor's <see cref="index"/>, the <see cref="targetTransform"/> transform for its IK target, the
    /// <see cref="deviceRotation"/> that indicates how the sensor is supposed to be placed onto the person,
    /// the <see cref="collisionReporter"/> that can be used to map colliders to the sensor and <see cref="calibrationEuler"/> that are used for calibration.
    /// </summary>
    [Serializable]
    public class StreamSensorObject {
        [SerializeField] public int index;
        [SerializeField] public Transform targetTransform;
        [SerializeField] public Transform deviceRotation;
        [SerializeField] public StreamCollisionReporter collisionReporter;
        [SerializeField] public Quaternion calibrationRot = Quaternion.identity;
    }

    [SerializeField] public List<StreamSensorObject> targets;
}