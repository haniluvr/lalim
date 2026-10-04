using UnityEngine;
using UnityEngine.Video;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Teleport scene: the swirling portal video inside the arches. One VideoPlayer (one decoder,
    /// cheap on phones) renders the looping clip into a RenderTexture that every portal surface
    /// shows. The clip is pre-baked to loop seamlessly (its end cross-fades into its start), so
    /// it simply loops forward — no ping-pong.
    ///
    /// Built by Tools ▸ Lalim ▸ Teleport ▸ Add Arch Portal Video.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class ArchPortalVideo : MonoBehaviour
    {
        [SerializeField] private VideoClip _clip;
        [SerializeField] private RenderTexture _target;
        [Tooltip("Surfaces that show the video (their material samples _target).")]
        [SerializeField] private Renderer[] _surfaces = new Renderer[0];
        [SerializeField, Range(0.25f, 2f)] private float _playbackSpeed = 1f;

        private VideoPlayer _player;

        private void Awake()
        {
            _player = GetComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.source = VideoSource.VideoClip;
            _player.clip = _clip;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _target;
            _player.isLooping = true;
            _player.skipOnDrop = true;
            _player.waitForFirstFrame = true;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.playbackSpeed = _playbackSpeed;

            // Until the first frame arrives the texture is undefined; start it on the clip's dark-blue tone.
            if (_target != null)
            {
                RenderTexture active = RenderTexture.active;
                RenderTexture.active = _target;
                GL.Clear(true, true, new Color(0.25f, 0.4f, 0.75f));
                RenderTexture.active = active;
            }
        }

        private void OnEnable()
        {
            if (_player != null && _clip != null) _player.Play();
        }

        private void OnDisable()
        {
            if (_player != null) _player.Pause();
        }

        // Saves battery while the app is in the background.
        private void OnApplicationPause(bool paused)
        {
            if (_player == null || !isActiveAndEnabled) return;
            if (paused) _player.Pause();
            else _player.Play();
        }

#if UNITY_EDITOR
        public void EditorSetup(VideoClip clip, RenderTexture target, Renderer[] surfaces)
        {
            _clip = clip;
            _target = target;
            _surfaces = surfaces;
        }
#endif
    }
}
