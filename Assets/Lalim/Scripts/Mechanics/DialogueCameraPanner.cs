using System.Collections;
using UnityEngine;

namespace Lalim.Mechanics
{
    public class DialogueCameraPanner : MonoBehaviour
    {
        public static DialogueCameraPanner Instance { get; private set; }

        [SerializeField] private Transform _playerCameraTarget;
        [SerializeField] private Transform _npcCameraTarget;
        [SerializeField] private float _panSpeed = 2f;

        private Camera _mainCamera;
        private Vector3 _originalCameraPos;
        private Quaternion _originalCameraRot;
        private Coroutine _panCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _mainCamera = Camera.main;
        }

        public void SetTargets(Transform playerTarget, Transform npcTarget)
        {
            _playerCameraTarget = playerTarget;
            _npcCameraTarget = npcTarget;
        }

        public void StartDialogueSequence()
        {
            if (_mainCamera != null)
            {
                _originalCameraPos = _mainCamera.transform.position;
                _originalCameraRot = _mainCamera.transform.rotation;
            }
        }

        public void PanToSpeaker(bool isPlayer)
        {
            if (_mainCamera == null) return;
            Transform target = isPlayer ? _playerCameraTarget : _npcCameraTarget;

            if (target != null)
            {
                if (_panCoroutine != null) StopCoroutine(_panCoroutine);
                _panCoroutine = StartCoroutine(PanRoutine(target.position, target.rotation));
            }
        }

        public void EndDialogueSequence()
        {
            if (_mainCamera != null && _originalCameraPos != Vector3.zero)
            {
                if (_panCoroutine != null) StopCoroutine(_panCoroutine);
                _panCoroutine = StartCoroutine(PanRoutine(_originalCameraPos, _originalCameraRot));
            }
        }

        private IEnumerator PanRoutine(Vector3 targetPos, Quaternion targetRot)
        {
            while (Vector3.Distance(_mainCamera.transform.position, targetPos) > 0.01f || 
                   Quaternion.Angle(_mainCamera.transform.rotation, targetRot) > 0.1f)
            {
                _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, targetPos, Time.deltaTime * _panSpeed);
                _mainCamera.transform.rotation = Quaternion.Lerp(_mainCamera.transform.rotation, targetRot, Time.deltaTime * _panSpeed);
                yield return null;
            }
            
            _mainCamera.transform.position = targetPos;
            _mainCamera.transform.rotation = targetRot;
        }
    }
}
