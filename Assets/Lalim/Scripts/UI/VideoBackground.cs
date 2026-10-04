using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Lalim.UI
{
    /// <summary>
    /// Full-screen looping video behind a UI (the loading screen). The clip is pre-baked to loop
    /// seamlessly (Tools/make_seamless_loop.swift), so it simply loops forward.
    ///
    /// The RawImage shows a still poster (the clip's first frame) until the video's first frame
    /// is ready, so there's never a blank flash while the player prepares. Pair with an
    /// AspectRatioFitter (Envelope Parent) so the video covers the screen at any aspect ratio.
    /// </summary>
    [RequireComponent(typeof(RawImage), typeof(VideoPlayer))]
    public class VideoBackground : MonoBehaviour
    {
        [SerializeField] private VideoClip _clip;
        [Tooltip("Shown until the first video frame arrives (the clip's first frame).")]
        [SerializeField] private Texture _poster;

        private RawImage _image;
        private VideoPlayer _player;
        private RenderTexture _target;
        private bool _showingVideo;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            _player = GetComponent<VideoPlayer>();
            if (_poster != null) _image.texture = _poster;
            if (_clip == null) return;

            _target = new RenderTexture((int)_clip.width, (int)_clip.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "VideoBackground",
                useMipMap = false,
                wrapMode = TextureWrapMode.Clamp,
            };
            _player.playOnAwake = false;
            _player.source = VideoSource.VideoClip;
            _player.clip = _clip;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _target;
            _player.isLooping = true;
            _player.skipOnDrop = true;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        }

        private void OnEnable()
        {
            if (_player != null && _clip != null) _player.Play();
        }

        private void OnDisable()
        {
            if (_player != null) _player.Pause();
        }

        private void Update()
        {
            if (_showingVideo || _player == null || _target == null) return;
            if (_player.isPlaying && _player.frame > 0)
            {
                _image.texture = _target;
                _showingVideo = true;
            }
        }

        private void OnDestroy()
        {
            if (_target == null) return;
            if (_player != null) _player.targetTexture = null;
            _target.Release();
            Destroy(_target);
        }

#if UNITY_EDITOR
        public void EditorSetup(VideoClip clip, Texture poster)
        {
            _clip = clip;
            _poster = poster;
            GetComponent<RawImage>().texture = poster;
        }
#endif
    }
}
