using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioProcessor : MonoBehaviour
{
	private AudioSource _source;
	private float[] _audioData;

    void Start()
    {
        // Unpack sample data.
        _source = GetComponent<AudioSource>();
        _audioData = new float[_source.clip.samples*_source.clip.channels];
        _source.clip.GetData(_audioData, 0);
    }
}
