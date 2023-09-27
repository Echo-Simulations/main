using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

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

    public bool SendTexture(NativeArray<float> texture)
    {
        if (!texture.IsCreated)
            return false;
        float[] textureData = new float[texture.Length];
        texture.CopyTo(textureData);
        for (int i = 0; i < textureData.Length; i++)
        {
            // Manipulate _audioData.
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
