using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioProcessor : MonoBehaviour
{
    private AudioSource _source;
    private float[] _audioData;
    private bool _hasClip = false;

    void Start()
    {
        // Unpack sample data.
        _source = GetComponent<AudioSource>();
        if (_source.clip != null)
        {
            _audioData = new float[_source.clip.samples*_source.clip.channels];
            _source.clip.GetData(_audioData, 0);
            _hasClip = true;
        }
    }

    // NOTE: The data array could also be two-dimensional. Change the
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
        return true;
    }

    public void PlayAudio()
    {
        if (_hasClip)
        {
            // Pack sample data.
            _source.clip.SetData(_audioData, 0);
            //
            _source.Play();
        }
        // Tried to re-pack sample data without source.
    }
}
