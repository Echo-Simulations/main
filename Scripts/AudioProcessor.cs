using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RayTracingObject), typeof(AudioSource))]
public class AudioProcessor : MonoBehaviour
{
    private AudioSource _source; // The audio source for this listener.
                                 // This necessarily must be possessed by this
                                 // object.
    private float[] _audioData; // The audio buffer containing sample data.
    private bool _hasClip = false; // Whether the audio source is assigned a
                                   // valid audio clip.
    private float _volume = 1.0f;
    private RayTracingObject _obj; // The source object.

    private void Start()
    {
        // Unpack sample data.
        _source = GetComponent<AudioSource>();
        if (_source.clip != null)
        {
            _audioData = new float[_source.clip.samples*_source.clip.channels];
            _source.clip.GetData(_audioData, 0);
            _source.loop = true;
            _hasClip = true;
            _obj = GetComponent<RayTracingObject>();
        }
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
            _source.clip.SetData(_audioData, 0);
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
    // TODO: The data array could also be two-dimensional. Change the
    //       type from float[] to float[,] as necessary to match
    //       RayTracingMaster.
    public bool SendTexture(float[] data, int height, int width)
    {
        bool error = false;
        float volume = -1.0f;
        // Traverse in row-major order.
        for (int i = 0; i < height; i++)
        {
            int idx1 = i * width;
            for (int j = 0; j < width; j++)
            {
                // Manipulate _audioData, breaking if there was an error.
                // .. if (error_condition) { error = true; break; }
                // (Breaking must occur in both loops on error.)

                // Try to retrieve only the first distance parameter for now.
                if (j == 1 && data[idx1+1] > 0.0f && data[idx1+1] < 1.0f)
                {
                    volume = 1.0f - data[idx1+1];
                    break;
                }
            }
        }
        if (volume < 0.0f || volume == _volume)
        {
            return !error;
        }
#if UNITY_EDITOR
        Debug.Log("[" + GetType().ToString() + "] New volume is " + volume);
#endif
        _volume = volume * (1.0f / _volume);
        // Update sample array accounting for volume.
        for (int i = 0; i < _audioData.Length; i++)
        {
            _audioData[i] *= _volume;
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
                + "] Warning: Failed to process texture. Not updating buffer.");
        }
#endif
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
}
