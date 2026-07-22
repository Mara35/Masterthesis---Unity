using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads a full-body CSV via <see cref="CsvFullBodyLoader"/> and advances a playback cursor over
/// its frames, exposing the current one as <see cref="CurrentFrame"/>. Supports play/pause/stop,
/// looping and a playback speed. Because the CSV has no timestamps, a fixed frame interval
/// (<see cref="assumedDtSeconds"/>) is assumed.
/// </summary>

public class FullBodyPlaybackSource : MonoBehaviour
{
    [SerializeField] private TextAsset csvFile;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop = true;
    [SerializeField] private float playbackSpeed = 1f;
    [SerializeField] private float assumedDtSeconds = 0.02f;

    public FullBodyFrame CurrentFrame { get; private set; }
    public bool HasData => _frames != null && _frames.Count > 0;

    private List<FullBodyFrame> _frames = new();
    private float _playbackTime = 0f;
    private int _currentIndex = 0;
    private bool _isPlaying = false;

    private void Start()
    {
        if (csvFile != null)
            _frames = CsvFullBodyLoader.LoadFromText(csvFile.text, assumedDtSeconds);

        if (_frames.Count > 0)
            CurrentFrame = _frames[0];

        Debug.Log("FullBody frames loaded: " + _frames.Count);

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

        // Advance the cursor to the last frame whose timestamp has already passed.
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