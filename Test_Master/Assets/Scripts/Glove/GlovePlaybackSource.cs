using System.Collections.Generic;
using UnityEngine;

public class GlovePlaybackSource : MonoBehaviour
{
    [SerializeField] private TextAsset csvFile;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop = true;
    [SerializeField] private float playbackSpeed = 1f;

    public float[] CurrentAngles { get; private set; } = new float[15];
    public bool HasData => _frames != null && _frames.Count > 0;

    private List<GloveFrame> _frames = new();
    private float _playbackTime = 0f;
    private int _currentIndex = 0;
    private bool _isPlaying = false;

    private void Start()
    {
        
        if (csvFile != null)
            _frames = CsvGloveLoader.LoadFromText(csvFile.text);

        Debug.Log("CSV loaded: " + (_frames != null ? _frames.Count : 0));

        if (playOnStart)
            Play();
    }

    private void Update()
    {
        if (!_isPlaying || !HasData)
            return;

        _playbackTime += Time.deltaTime * playbackSpeed;

        float maxTime = _frames[_frames.Count - 1].Time;
        if (_playbackTime > maxTime)
        {
            if (loop)
            {
                _playbackTime = 0f;
                _currentIndex = 0;
            }
            else
            {
                _playbackTime = maxTime;
                _isPlaying = false;
            }
        }

        while (_currentIndex < _frames.Count - 1 && _frames[_currentIndex + 1].Time <= _playbackTime)
        {
            _currentIndex++;
        }

        var frame = _frames[_currentIndex];
        for (int i = 0; i < 15; i++)
            CurrentAngles[i] = frame.Angles[i];
    }

    public void Play()
    {
        _playbackTime = 0f;
        _currentIndex = 0;
        _isPlaying = true;
    }

    public void Pause()
    {
        _isPlaying = false;
    }

    public void Stop()
    {
        _isPlaying = false;
        _playbackTime = 0f;
        _currentIndex = 0;
    }
}