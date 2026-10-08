using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Plays a rigged sea creature's own clip (swim, or idle) on a loop, starting at a random point
    /// so a school doesn't beat its tails in lockstep. Sits on the model's root (the FBX instance);
    /// adds the Animator it needs. Movement stays with MarineSchool / WanderingMarineAnimal.
    /// </summary>
    [DisallowMultipleComponent]
    public class CreatureRig : MonoBehaviour
    {
        [SerializeField] private AnimationClip _clip;
        [SerializeField, Min(0f)] private float _speed = 1f;

        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;

        /// <summary>Plays <paramref name="modelName"/>'s clip (from <see cref="SeaCreatureClips"/>) on an instance of it.</summary>
        public static CreatureRig Play(GameObject instance, string modelName, bool idle, float speed = 1f)
        {
            if (instance == null) return null;
            AnimationClip clip = SeaCreatureClips.Find(modelName, idle);
            if (clip == null) return null;
            CreatureRig rig = instance.GetComponent<CreatureRig>();
            if (rig == null) rig = instance.AddComponent<CreatureRig>();
            rig.SetClip(clip, speed);
            return rig;
        }

        public void SetClip(AnimationClip clip, float speed)
        {
            if (clip == _clip && Mathf.Approximately(speed, _speed) && _graph.IsValid()) return;
            _clip = clip;
            _speed = speed;
            if (isActiveAndEnabled) Rebuild();
        }

        private void OnEnable() => Rebuild();

        private void OnDisable()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        private void Rebuild()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (_clip == null) return;
            Animator animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            _playable = AnimationPlayableUtilities.PlayClip(animator, _clip, out _graph);
            _playable.SetSpeed(_speed);
            _playable.SetTime(Random.value * _clip.length);
        }

        // The imported clips aren't flagged to loop: wrap them by hand.
        private void Update()
        {
            if (!_graph.IsValid() || _clip == null || _clip.length <= 0f) return;
            double t = _playable.GetTime();
            if (t >= _clip.length) _playable.SetTime(t % _clip.length);
        }

#if UNITY_EDITOR
        public void EditorSetup(AnimationClip clip, float speed)
        {
            _clip = clip;
            _speed = speed;
        }
#endif
    }
}
