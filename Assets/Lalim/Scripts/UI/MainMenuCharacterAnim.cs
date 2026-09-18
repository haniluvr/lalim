using UnityEngine;
using System.Collections;

namespace Lalim.UI
{
    /// <summary>
    /// Animates the character swimming into the main menu scene from off-screen,
    /// stopping at the center, and transitioning to a floating animation loop.
    /// </summary>
    public class MainMenuCharacterAnim : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator _animator;
        
        [Header("Movement Settings")]
        [SerializeField] private Transform _startPoint;
        [SerializeField] private Transform _endPoint;
        [SerializeField] private float _swimDuration = 4.0f;
        
        [Header("Animator Parameters")]
        [SerializeField] private string _isSwimmingParam = "IsSwimming";
        [SerializeField] private string _isFloatingParam = "IsFloating";

        private void Start()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_startPoint != null && _endPoint != null)
            {
                transform.position = _startPoint.position;
                transform.rotation = _startPoint.rotation;
                StartCoroutine(SwimInRoutine());
            }
            else
            {
                Debug.LogWarning("[MainMenuCharacterAnim] Start or End point not assigned. Skipping movement.");
                if (_animator != null)
                {
                    _animator.SetBool(_isFloatingParam, true);
                }
            }
        }

        private IEnumerator SwimInRoutine()
        {
            float elapsed = 0f;

            if (_animator != null)
            {
                _animator.SetBool(_isSwimmingParam, true);
                _animator.SetBool(_isFloatingParam, false);
            }

            while (elapsed < _swimDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _swimDuration;

                // Ease out cubic
                float easeT = 1f - Mathf.Pow(1f - t, 3f);
                
                transform.position = Vector3.Lerp(_startPoint.position, _endPoint.position, easeT);
                transform.rotation = Quaternion.Slerp(_startPoint.rotation, _endPoint.rotation, easeT);

                yield return null;
            }

            transform.position = _endPoint.position;
            transform.rotation = _endPoint.rotation;

            // Stop swimming, start floating
            if (_animator != null)
            {
                _animator.SetBool(_isSwimmingParam, false);
                _animator.SetBool(_isFloatingParam, true);
            }
        }
    }
}
