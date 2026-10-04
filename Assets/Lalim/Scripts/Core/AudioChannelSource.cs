using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// Put on any AudioSource (level music, ambience loops, voice lines) so the settings
    /// volume for its channel applies to it. The Inspector volume becomes the base volume.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioChannelSource : MonoBehaviour
    {
        [SerializeField] private AudioChannel _channel = AudioChannel.Music;

        private AudioSource _source;
        private float _baseVolume;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _baseVolume = _source.volume;
        }

        private void OnEnable() => GameAudio.Bind(_source, _baseVolume, _channel);
        private void OnDisable() => GameAudio.Unbind(_source);
    }
}
