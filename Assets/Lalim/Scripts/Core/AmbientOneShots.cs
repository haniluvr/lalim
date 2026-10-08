using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.Core
{
    /// <summary>
    /// Now-and-then sounds from somewhere out in the water (a distant whale calling): every so often
    /// one of the clips plays from a random direction around the diver, far off, while the diver is
    /// inside <see cref="_area"/> (if set). Ambient volume.
    /// </summary>
    public class AmbientOneShots : MonoBehaviour
    {
        [SerializeField] private AudioClip[] _clips;
        [Tooltip("Only while the diver is inside this collider's bounds (optional).")]
        [SerializeField] private Collider _area;
        [SerializeField] private Vector2 _intervalSeconds = new Vector2(25f, 50f);
        [SerializeField] private Vector2 _distance = new Vector2(30f, 55f);
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;

        private Bounds _bounds;
        private bool _hasBounds;
        private float _next;

        private void Start()
        {
            // Cached: the area may be a zone's toxic volume, which is switched off once cleansed.
            if (_area != null) { _bounds = _area.bounds; _hasBounds = true; }
            _next = Time.time + Random.Range(_intervalSeconds.x * 0.3f, _intervalSeconds.y * 0.5f);
        }

        private void Update()
        {
            if (Time.time < _next || _clips == null || _clips.Length == 0) return;
            _next = Time.time + Random.Range(_intervalSeconds.x, _intervalSeconds.y);
            PlayerController pc = PlayerController.Instance;
            if (pc == null) return;
            Vector3 p = pc.transform.position;
            if (_hasBounds && !_bounds.Contains(p)) return;
            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector3 at = p + new Vector3(dir.x, Random.Range(-0.3f, 0.1f), dir.y) * Random.Range(_distance.x, _distance.y);
            AudioClip clip = _clips[Random.Range(0, _clips.Length)];
            // Far off but clearly heard: full volume out to most of that distance.
            GameAudio.PlayAt(clip, at, _volume, AudioChannel.Ambient, _distance.x);
        }

#if UNITY_EDITOR
        public void EditorSetup(AudioClip[] clips, Collider area, Vector2 interval, Vector2 distance, float volume)
        {
            _clips = clips;
            _area = area;
            _intervalSeconds = interval;
            _distance = distance;
            _volume = volume;
        }
#endif
    }
}
