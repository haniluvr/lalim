using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// The diver floats when idle, like the Kataw do: a slow bob up and down with a gentle sway,
    /// while the kick animation eases to a lazy drift. It fades in after a moment without swimming
    /// and out as soon as the diver moves. Only the visible body moves (the model under the player),
    /// never the player's collider, so it can't push the diver into anything.
    /// </summary>
    public class DiverIdleFloat : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _bob = 0.18f;            // metres, as the Kataw
        [SerializeField, Min(0.1f)] private float _bobSpeed = 0.8f;
        [SerializeField, Range(0f, 10f)] private float _swayDegrees = 3f;
        [Tooltip("How far the body turns up from its swimming pose while floating (90 = straight up, like the Kataw).")]
        [SerializeField, Range(0f, 90f)] private float _uprightDegrees = 72f;
        [Tooltip("Animation speed while floating (the swim kick slows to a drift).")]
        [SerializeField, Range(0.1f, 1f)] private float _floatAnimSpeed = 0.45f;
        [SerializeField, Min(0f)] private float _settleDelay = 0.4f;
        [SerializeField, Min(0.05f)] private float _blendSeconds = 0.8f;

        private PlayerController _player;
        private Animator _animator;
        private readonly List<Transform> _visuals = new List<Transform>();
        // The pose to float from: the animator's pose this frame if it wrote one, else the last one.
        private readonly List<Vector3> _basePos = new List<Vector3>(), _written = new List<Vector3>();
        private readonly List<Quaternion> _baseRot = new List<Quaternion>(), _writtenRot = new List<Quaternion>();
        private float _weight;
        private float _idleFor;
        private float _phase;
        private Vector3 _pivot;   // the body's middle, in the player's space (it turns upright about this)

        private void Start()
        {
            _player = GetComponent<PlayerController>();
            _animator = GetComponentInChildren<Animator>();
            // The visible body: every direct child that draws something (the armature and the mesh).
            foreach (Transform c in transform)
            {
                if (c.GetComponentInChildren<Renderer>(true) == null && c.GetComponentInChildren<Animator>(true) == null && c.name != "Armature") continue;
                _visuals.Add(c);
                _basePos.Add(c.localPosition);
                _baseRot.Add(c.localRotation);
                _written.Add(c.localPosition);
                _writtenRot.Add(c.localRotation);
            }
            _phase = Random.value * 10f;
            Bounds b = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            _pivot = transform.InverseTransformPoint(b.center);
        }

        private void LateUpdate()
        {
            if (_player == null) return;
            float dt = Time.deltaTime;
            bool idle = _player.IsMovementEnabled && !_player.IsMoving && !_player.IsDashing && !_player.IsMounted;
            _idleFor = idle ? _idleFor + dt : 0f;
            float target = _idleFor >= _settleDelay ? 1f : 0f;
            _weight = Mathf.MoveTowards(_weight, target, dt / _blendSeconds);
            float w = Mathf.SmoothStep(0f, 1f, _weight);

            float t = Time.time * _bobSpeed + _phase;
            Vector3 offset = Vector3.up * Mathf.Sin(t) * _bob * w;
            Quaternion sway = Quaternion.AngleAxis(-_uprightDegrees * w, Vector3.right)
                            * Quaternion.Euler(Mathf.Sin(t * 0.5f) * _swayDegrees * 0.5f * w, 0f, Mathf.Sin(t * 0.7f + 1f) * _swayDegrees * w);
            for (int i = 0; i < _visuals.Count; i++)
            {
                Transform v = _visuals[i];
                if (v == null) continue;
                // Layer on top of the animation: take the animator's fresh pose when it changed it.
                if (v.localPosition != _written[i]) _basePos[i] = v.localPosition;
                if (v.localRotation != _writtenRot[i]) _baseRot[i] = v.localRotation;
                v.localPosition = _pivot + sway * (_basePos[i] - _pivot) + transform.InverseTransformVector(offset);
                v.localRotation = sway * _baseRot[i];
                _written[i] = v.localPosition;
                _writtenRot[i] = v.localRotation;
            }
            if (_animator != null) _animator.speed = Mathf.Lerp(1f, _floatAnimSpeed, w);
        }
    }
}
