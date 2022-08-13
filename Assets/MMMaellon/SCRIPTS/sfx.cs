
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class sfx : UdonSharpBehaviour
{
    public AudioSource[] sources;
    public AudioSource paintSource;
    public AudioSource stirSound;
    public AudioClip paintSound;
    public AudioClip clearSound;
    public AudioClip completeSound;
    void Start()
    {
        stirSound.volume = 0;
        stirSound.Play();
    }

    public AudioSource GetAvailableSource()
    {
        foreach (AudioSource src in sources)
        {
            if (!src.isPlaying)
            {
                return src;
            }
        }
        return sources[0];
    }

    public void PlayPaint(Vector3 pos)
    {
        if (paintSource.isPlaying){
            return;
        }
        paintSource.clip = paintSound;
        paintSource.transform.position = pos;
        paintSource.Play();
    }
    public void PlayClear(Vector3 pos)
    {
        AudioSource src = GetAvailableSource();
        src.clip = clearSound;
        src.transform.position = pos;
        src.Play();
    }
    public void PlayComplete(Vector3 pos)
    {
        AudioSource src = GetAvailableSource();
        src.clip = completeSound;
        src.transform.position = pos;
        src.Play();
    }

    public void PlayStir(Vector3 pos, float speed)
    {
        stirSound.transform.position = pos;
        float maxVolume = Mathf.Min(1.0f, Mathf.Sqrt(speed) * 2);
        stirSound.volume = maxVolume;
    }

    public void Update()
    {
        stirSound.volume = Mathf.Lerp(stirSound.volume, 0, 0.02f);
    }
}
