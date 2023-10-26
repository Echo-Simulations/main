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
    private float[] _modifiedAudioData;
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
            _modifiedAudioData = new float[_source.clip.samples * _source.clip.channels];
            _source.clip.GetData(_audioData, 0);
            _source.loop = true;
            _hasClip = true;
            _obj = GetComponent<RayTracingObject>();
        }
#if UNITY_EDITOR
        else
        {
            Debug.LogError("Sound sources must have audio clips.");
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
    // TODO: The data array could also be two-dimensional. Change the
    //       type from float[] to float[,] as necessary to match
    //       RayTracingMaster.
    public bool SendTexture(float[] data, int texSize, int parameterCount, int layers)
    {
        bool error = false;
        float distance = 0.0f;
        int count = 0;
        
        // Traverse in row-major order.
        for (int i = 0; i < texSize; i++)
        {
            // Manipulate _audioData, breaking if there was an error.
            // .. if (error_condition) { error = true; break; }

            if (data[i] > 0.5f / 256)
            {
                //Distance for volume
                //Taking the average (There are probably better alternatives)
                if (data[i + texSize] > 0.0f) {
                    distance = (distance + (1.0f - data[i + texSize]));
                    count++;
                }
                //Other attributes
            }
        }
        //Translate into volume
        if (count != 0 && distance / count >= 0.0f)
        {
            _volume = distance / count;
            //Scale the volume to a reasonable level (so it's not audible from 1000 m away)
            //This method introduces a lot of variability. An alternative should be found if possible
            //_volume = Mathf.Pow(_volume, 100);
        }
        else
        {
            _volume = 0.0f;
        }
#if UNITY_EDITOR
        Debug.Log(distance + ", " + count);
        Debug.Log("[" + GetType().ToString() + "] New volume is " + _volume);
#endif
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
