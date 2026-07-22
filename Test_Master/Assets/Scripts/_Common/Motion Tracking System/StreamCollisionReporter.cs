using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Interface that defines a listener on collision events where you can subscribe to and get information about the type of collision and the id of the sensor
/// </summary>
public interface IStreamCollisionListener
{
    void OnStreamCollisionEnter(int sensorId, Collider c);
    void OnStreamCollisionExit(int sensorId, Collider c);
}

/// <summary>
/// Monobahviour that is related to a stream sensor collider box that can be used to subscribe to collision events related to the sensor
/// </summary>
public class StreamCollisionReporter : MonoBehaviour
{
    private readonly Dictionary<int, IStreamCollisionListener> _collisionListeners = new();

    public void AddCollisionListener(IStreamCollisionListener listener, int key)
    {
        _collisionListeners[key] = listener;
    }

    public void RemoveCollisionListener(IStreamCollisionListener listener)
    {
        foreach (var k in _collisionListeners.Where(x => x.Value == listener).Select(x => x.Key).ToList())
        {
            _collisionListeners.Remove(k);
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        foreach (var (key, listener) in _collisionListeners)
        {
            listener.OnStreamCollisionEnter(key, other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        foreach (var (key, listener) in _collisionListeners)
        {
            listener.OnStreamCollisionExit(key, other);
        }
    }
}