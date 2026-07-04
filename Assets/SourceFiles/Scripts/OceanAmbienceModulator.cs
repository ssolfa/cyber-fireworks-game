using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class OceanAmbienceModulator : MonoBehaviour
{
    [Header("Base Volume")]
    [Range(0f, 1f)]
    public float baseVolume = 0.5f;

    [Header("LFO 1 Settings")]
    public float lfo1Frequency = 0.16f; // ~6.25 second cycle (main wave swell)
    public float lfo1Amplitude = 0.15f;

    [Header("LFO 2 Settings")]
    public float lfo2Frequency = 0.27f; // ~3.7 second cycle (secondary wave)
    public float lfo2Amplitude = 0.10f;

    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    private void Update()
    {
        if (audioSource == null) return;

        // Calculate independent sine wave offsets over time
        float lfo1 = Mathf.Sin(Time.time * lfo1Frequency * 2.0f * Mathf.PI) * lfo1Amplitude;
        float lfo2 = Mathf.Sin(Time.time * lfo2Frequency * 2.0f * Mathf.PI) * lfo2Amplitude;

        // Modulate volume and clamp to safe [0, 1] range
        float finalVolume = Mathf.Clamp01(baseVolume + lfo1 + lfo2);
        audioSource.volume = finalVolume;
    }
}
