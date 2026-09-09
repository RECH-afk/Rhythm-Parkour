using UnityEngine;

[CreateAssetMenu(menuName = "Hadal Zone/Audio/Sound Library")]
public class SoundLibrary : ScriptableObject
{
    [Tooltip("All sounds included in this library.")]
    public SoundData[] sounds;
}
