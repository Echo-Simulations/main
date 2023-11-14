using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RayTracingObject), typeof(AudioSource))]
public class AudioProcessor : MonoBehaviour
{
    public const int MAX_FREQ = 48000; // Maximum acceptible frequency in hertz.

    private int _soundSourceId = 0;
    private AudioSource _source; // The audio source for this listener.
                                 // This necessarily must be possessed by this
                                 // object.
    private float[] _audioData; // The audio buffer containing sample data.
    private float[] _modifiedAudioData; // Secondary buffer from which the
                                        // original sample data is modified.
    private bool _hasClip = false; // Whether the audio source is assigned a
                                   // valid audio clip.
    private float _volume = 1.0f; // The current volume of the audio clip.

    private void Start()
    {
        _source = GetComponent<AudioSource>();
        // Enforce presence of sound clip.
        if (_source.clip != null && _source.clip.frequency <= MAX_FREQ)
        {
            // Prepare sample buffers and retrieve data.
            _audioData = new float[_source.clip.samples*_source.clip.channels];
            _modifiedAudioData = new float[_source.clip.samples * _source.clip.channels];
            _source.clip.GetData(_audioData, 0);
            _source.loop = true;
            _hasClip = true;
        }
#if UNITY_EDITOR
        else
        {
            Debug.LogError("Sound sources must have valid audio clips with"
                + " properties acceptable to the processor.");
        }
#endif
        // Play audio (if valid).
        PlayAudio();
    }

    private void OnDestroy()
    {
        StopAudio(false);
    }

    private void OnEnable()
    {
        // Start playing audio on object enable.
        PlayAudio();
    }

    private void OnDisable()
    {
        // Stop all audio on object disable.
        StopAudio(false);
    }

    // Update the audio buffer.
    // NOTE: This can be called while the audio is playing to dynamically
    //       update the properties of the playing sound immediately. All
    //       audio manipulation should be followed by a call to UpdateAudio().
    private void UpdateAudio()
    {
        if (_hasClip)
        {
            // Pack sample data.
            _source.clip.SetData(_modifiedAudioData, 0);
        }
#if UNITY_EDITOR
        else
        {
            Debug.LogWarning("[" + GetType().ToString()
                + "] Warning: Failed to update audio data.");
        }
#endif
    }

    // Receive a texture containing audio information and modify the buffer
    // accordingly.
    // NOTE: This is the where the manipulation of the audio itself is
    //       implemented and is the primary outward-facing interface for
    //       communication with the master script.
    public bool SendTexture(float[] data, int texSize, int parameterCount, int layers)
    {
        bool error = false;
        float distance = 0.0f;
        int distanceCount = 0;

        // Traverse in row-major order.
        for (int i = 0; i < texSize; i++)
        {
            for (int j = 0; j < layers; j++)
            {
                int index = i + j * parameterCount * texSize;
                // Manipulate _audioData, breaking if there was an error.
                // .. if (error_condition) { error = true; break; }

                if (data[index] > (_soundSourceId - 0.5f) / 256 &&
                    data[index] < (_soundSourceId + 0.5f) / 256)
                {
                    //Distance for volume
                    //Taking the average (There are probably better alternatives)
                    if (data[index + texSize] > 0.0f)
                    {
                        distance = (distance + (1.0f - data[index + texSize]));
                        distanceCount++;
                    }
                    //Other attributes
                }
            }
        }
        //Translate into volume
        float prevVolume = _volume;
        if (distanceCount != 0 && distance / distanceCount >= 0.0f)
        {
            _volume = distance / distanceCount;
            //Scale the volume to a reasonable level (so it's not audible from 1000 m away)
            //This method introduces a lot of variability. An alternative should be found if possible
            //_volume = Mathf.Pow(_volume, 100);
        }
        else
        {
            _volume = 0.0f;
        }
#if UNITY_EDITOR
        Debug.Log("[" + GetType().ToString() + "] New volume is " + _volume
            + "\n(" + distance + ", " + distanceCount + ")");
#endif
        // Update the sample array iff characteristics have changed.
        if (prevVolume != _volume)
        {
            // Update sample array accounting for volume.
            for (int i = 0; i < _modifiedAudioData.Length; i++)
            {
                _modifiedAudioData[i] = _audioData[i] * _volume;
            }
            // Update the audio buffer.
            if (!error)
            {
                UpdateAudio();
            }
#if UNITY_EDITOR
            else
            {
                Debug.LogWarning("[" + GetType().ToString()
                    + "] Warning: Failed to process texture. Not updating"
                    + " buffer.");
            }
#endif
        }
        return !error;
    }

    // Play audio over Unity's own audio system.
    public void PlayAudio()
    {
        if (_hasClip && !_source.isPlaying)
        {
            // Ensure audio is up-to-date before playing.
            UpdateAudio();
            // Either unpause or play the source.
            if (_source.time == 0f)
            {
                _source.Play();
            }
            else
            {
                _source.UnPause();
            }
        }
    }

    // Stop any currently playing audio.
    public void StopAudio(bool pause)
    {
        if (_hasClip && _source.isPlaying)
        {
            // Either pause or stop the source.
            if (pause)
            {
                _source.Pause();
            }
            else
            {
                _source.Stop();
            }
        }
    }

    private void Update()
    {
        _soundSourceId = gameObject.GetComponent<RayTracingObject>().getId();
    }
}
