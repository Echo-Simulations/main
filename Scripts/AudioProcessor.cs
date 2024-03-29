using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[RequireComponent(typeof(RayTracingObject), typeof(AudioSource))]
public class AudioProcessor : MonoBehaviour
{
    public const int MAX_FREQ = 48000; // Maximum acceptible frequency in hertz.
    public const int MAX_LEN = 60*60; // Maximum acceptible length in seconds.

    private AudioSource _source; // The audio source for this listener.
                                 // This necessarily must be possessed by this
                                 // object.
    private AudioClip _clip; // The audio clip containing the initial state of
                             // the sample data.
    private AudioReverbFilter _reverb;

    private RayTracingObject _obj; // This ray tracing object.
    private float[] _audioData; // The audio buffer containing sample data.
    private float[] _modifiedAudioData; // Secondary buffer from which the
                                        // original sample data is modified.
    private float _volume = 1.0f; // The current volume of the audio clip.

    private void Start()
    {
        // NOTE: Source must include a sound clip component to be considered
        //       valid. It is recommended to use a unique clip for each audio
        //       source, though multiple sources referencing the same audio
        //       clip is supported.
        _source = GetComponent<AudioSource>();
        _obj = GetComponent<RayTracingObject>();
        _reverb = GetComponent<AudioReverbFilter>();
        if (_reverb != null)
        {
            _reverb.reverbPreset = AudioReverbPreset.Off;
            _reverb.reverbPreset = AudioReverbPreset.User;
            _reverb.room = 0.0f;
        }

        // Enforce presence of sound clip.
        if (_source.clip != null && _source.clip.frequency <= MAX_FREQ
            && _source.clip.length <= MAX_LEN)
        {
            PrepareAudio();
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

    private void Update()
    {
        // Gracefully handle the user changing the audio clip.
        if (_clip != _source.clip && _source.clip != null
            && _source.clip.frequency <= MAX_FREQ
            && _source.clip.length <= MAX_LEN)
        {
            PrepareAudio();
        }
    }

    private void OnDestroy()
    {
        // Stop all audio on object destroy.
        StopAudio(false);

        if (_clip != null)
        {
            _clip.SetData(_audioData, 0);
        }
    }

    private void OnEnable()
    {
        if (_source != null)
        {
            // Start playing audio on object enable.
            if (_clip != null)
            {
                _clip.SetData(_audioData, 0);
            }

            PlayAudio();
        }

    }

    private void OnDisable()
    {
        // Stop all audio on object disable.
        StopAudio(false);
        if (_clip != null)
        {
            _clip.SetData(_audioData, 0);
        }
    }

    private void PrepareAudio()
    {
        // Prepare sample buffers and retrieve data.
        _audioData = new float[_source.clip.samples*_source.clip.channels];
        _modifiedAudioData = new float[_source.clip.samples * _source.clip.channels];
        _source.clip.GetData(_audioData, 0);

        // Create distinct clip.
        AudioClip destructible_clip = AudioClip.Create(_source.clip.name,
            _source.clip.samples, _source.clip.channels,
            _source.clip.frequency, false);
        destructible_clip.SetData(_audioData, 0);
        _source.clip = _clip = destructible_clip;

        // Loop audio source.
        _source.loop = true;
    }

    // Update the audio buffer.
    // NOTE: This can be called while the audio is playing to dynamically
    //       update the properties of the playing sound immediately. All
    //       audio manipulation should be followed by a call to UpdateAudio().
    private void UpdateAudio()
    {
        if (_clip != null)
        {
            // Pack sample data.
            _clip.SetData(_modifiedAudioData, 0);
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
        float difference = 0.0f;
        int distanceCount = 0;
        int id = _obj.Id;

        // Traverse in row-major order.
        for (int i = 0; i < texSize; i++)
        {
            for (int j = 0; j < layers; j++)
            {
                int index = i + j * parameterCount * texSize;
                // Manipulate _audioData, breaking if there was an error.
                // .. if (error_condition) { error = true; break; }

                if (data[index] > (id - 0.5f) / 256 &&
                    data[index] < (id + 0.5f) / 256)
                {
                    // Distance for volume
                    // Taking the average (There are probably better alternatives)
                    if (data[index + texSize] > 0.0f)
                    {
                        distance = (distance + (1.0f - data[index + texSize]));
                        distanceCount++;
                    }
                }
            }
        }
        // Translate into volume
        if (distanceCount != 0 && distance / distanceCount >= 0.0f)
        {
            _volume = distance / distanceCount; //Doubles as the mean of the distance
        }
        else
        {
            _volume = 0.0f;
        }

#if UNITY_EDITOR
        Debug.Log("[" + GetType().ToString() + "] New volume is " + _volume
            + "\n(" + distance + ", " + distanceCount + ")");
#endif

        if (_reverb != null)
        {
            // Calculate standard deviation of distance.
            for (int i = 0; i < texSize; i++)
            {
                for (int j = 0; j < layers; j++)
                {
                    int index = i + j * parameterCount * texSize;
                    // Manipulate _audioData, breaking if there was an error.
                    // .. if (error_condition) { error = true; break; }

                    if (data[index] > (id - 0.5f) / 256 &&
                        data[index] < (id + 0.5f) / 256)
                    {
                        // Distance for volume
                        // Taking the average (There are probably better alternatives)
                        if (data[index + texSize] > 0.0f)
                        {
                            difference += Mathf.Abs((1.0f - data[index + texSize]) - _volume);
                        }
                    }
                }
            }
            difference = difference / distanceCount;
            if (difference > 0.0f) difference = Mathf.Sqrt(difference);
#if UNITY_EDITOR
            Debug.Log("[" + GetType().ToString() + "] New SD is " + difference
                + "\n(" + distance + ", " + distanceCount + ")");
#endif
            // Update the reverb.
            _reverb.roomHF = -10000 + 10000 * difference;
        }

        // Update the sample array iff characteristics have changed.
        if (_modifiedAudioData != null)
        {
            // Update sample array accounting for volume.
            for (int i = 0; i < _modifiedAudioData.Length; i++)
            {
                _modifiedAudioData[i] = _audioData[i] * _volume;
            }
            // Update the audio buffer.
            if (!error)
            {
                if (_source.isPlaying)
                {
                    UpdateAudio();
                }
                else
                {
                    // NOTE: SendTexture will never be invoked when disabled.
                    PlayAudio();
                }
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
        else
        {
            error = true;
        }

        return !error;
    }

    // Play audio over Unity's own audio system.
    public void PlayAudio()
    {
        if (_clip != null && !_source.isPlaying)
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
        if (_clip != null && _source.isPlaying)
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
