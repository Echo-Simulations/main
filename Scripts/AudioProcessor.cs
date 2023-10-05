using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioProcessor : MonoBehaviour
{
    private AudioSource _source;
    private float[] _audioData;
    private bool _hasClip = false;

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
        }
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
        else
        {
            Debug.Log("[" + GetType().ToString() + "] Warning: Failed to update audio data.");
        }
    }

    // TODO: The data array could also be two-dimensional. Change the
    //       type from float[] to float[,] as necessary to match
    //       RayTracingMaster.
    public bool SendTexture(float[] data, int height, int width)
    {
        // Traverse in row-major order.
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                // Manipulate _audioData, returning false if there was an error.
                // .. if (error) return false;
            }
        }
        UpdateAudio();
        return true;
    }

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
