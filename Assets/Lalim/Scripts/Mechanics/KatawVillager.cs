using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// A Kataw of the root village: floats in place on its idle loop with a slow bob, and turns to
    /// watch the diver swim past. Quest Kataw (the elder, the pool teachers) carry a soft amber rim
    /// light so they read through the silt (design doc). Put it on the body, not on the object holding
    /// the talk framing (TalkPoint, TalkCamera), which must not turn.
    /// </summary>
    public class KatawVillager : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _watchWithin = 10f;
        [SerializeField, Min(0f)] private float _bob = 0.18f;
        [SerializeField] private bool _questGiver;
        [SerializeField] private Color _rimColor = new Color(1f, 0.72f, 0.38f);

        [Header("Hum")]
        [Tooltip("A soft loop heard only near this Kataw (silent beyond the range).")]
        [SerializeField] private AudioClip _hum;
        [SerializeField, Min(2f)] private float _humRange = 14f;
        [SerializeField, Range(0f, 1f)] private float _humVolume = 0.6f;

        private AudioSource _humSource;

        private Vector3 _home;
        private float _homeYaw;
        private float _phase;
        private Light _rim;

        private void Start()
        {
            _home = transform.position;
            _homeYaw = transform.eulerAngles.y;
            _phase = Random.value * 10f;
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null) anim.Play(anim.GetCurrentAnimatorStateInfo(0).shortNameHash, 0, Random.value);   // villagers don't bob in step
            if (_questGiver) AddRim();
            if (_hum != null) StartHum();
        }

        private void StartHum()
        {
            _humSource = gameObject.AddComponent<AudioSource>();
            _humSource.clip = _hum;
            _humSource.loop = true;
            _humSource.playOnAwake = false;
            _humSource.spatialBlend = 1f;
            _humSource.rolloffMode = AudioRolloffMode.Linear;   // fully silent past the range
            _humSource.minDistance = 2f;
            _humSource.maxDistance = _humRange;
            _humSource.dopplerLevel = 0f;
            _humSource.time = Random.value * Mathf.Max(0f, _hum.length - 0.1f);   // villagers out of step
            Lalim.Core.GameAudio.Bind(_humSource, _humVolume, Lalim.Core.AudioChannel.Ambient);
            _humSource.Play();
        }

        private void OnDestroy()
        {
            if (_humSource != null) Lalim.Core.GameAudio.Unbind(_humSource);
        }

        private void AddRim()
        {
            _rim = new GameObject("RimLight").AddComponent<Light>();
            _rim.transform.SetParent(transform, false);
            _rim.transform.localPosition = new Vector3(0.4f, 1.4f, 1.3f);   // in front, so the face reads
            _rim.type = LightType.Point;
            _rim.color = _rimColor;
            _rim.range = 4.5f;
            _rim.intensity = 2.6f;
            _rim.shadows = LightShadows.None;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            transform.position = _home + Vector3.up * Mathf.Sin(Time.time * 0.8f + _phase) * _bob;

            float yaw = _homeYaw;
            PlayerController pc = PlayerController.Instance;
            if (pc != null)
            {
                Vector3 to = pc.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < _watchWithin * _watchWithin && to.sqrMagnitude > 0.01f) yaw = Quaternion.LookRotation(to).eulerAngles.y;
            }
            Quaternion want = Quaternion.Euler(0f, yaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 70f * dt);
            if (_rim != null) _rim.intensity = 2.4f + 0.3f * Mathf.Sin(Time.time * 1.7f + _phase);
        }

#if UNITY_EDITOR
        public void EditorSetup(bool questGiver) => _questGiver = questGiver;
        public void EditorSetHum(AudioClip hum) => _hum = hum;
#endif
    }
}
