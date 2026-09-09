using UnityEngine;

[CreateAssetMenu(menuName = "Hadal Zone/Audio/Sound Data")]
public class SoundData : ScriptableObject
{
    [Header("General Settings")]
    public string soundName;

    [Tooltip("One or more clips to choose from when playing this sound.")]
    public AudioClip[] clips;

    [Range(0f, 1f)]
    [Tooltip("Default playback volume (will be multiplied by master volume).")]
    public float volume = 1f;

    [Range(0.1f, 3f)]
    [Tooltip("Pitch multiplier for playback.")]
    public float pitch = 1f;

    [Tooltip("If true, the sound will loop when played.")]
    public bool loop = false;

    [Tooltip("If true, the sound will be 3D (spatialized). Otherwise it will be 2D.")]
    public bool spatial = false;
}
