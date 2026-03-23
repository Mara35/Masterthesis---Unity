using System.Collections.Generic;
using UnityEngine;

public class ArmPlaybackSource : MonoBehaviour
{
    [SerializeField] private TextAsset csvFile;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop = true;
    [SerializeField] private float playbackSpeed = 1f;

    public ArmFrame CurrentFrame { get; private set; }
    public bool HasData => _frames != null && _frames.Count > 0;

    private List<ArmFrame> _frames = new();
    private float _playbackTime = 0f;
    private int _currentIndex = 0;
    private bool _isPlaying = false;

    private void Start()
    {
        if (csvFile != null)
            _frames = CsvArmLoader.LoadFromText(csvFile.text);

        if (_frames.Count > 0)
            CurrentFrame = _frames[0];

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

        CurrentFrame = _frames[_currentIndex];
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
        if (_frames.Count > 0)
            CurrentFrame = _frames[0];
    }
}