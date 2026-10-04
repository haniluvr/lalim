using System;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Core;
using Lalim.DDA;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// One area of a multi-area minigame (zone 2 readings / replanting, zone 3 nets). Shows the
    /// interact icon ("Tap to use Water Sampling Kit", "Untangle the net"…) once its step is active;
    /// tapping it starts a first-person challenge with its own ID (so DDA re-scores after every area)
    /// and hands control to the IAreaMinigame on the same object.
    /// </summary>
    [DisallowMultipleComponent]
    public class AreaStation : MonoBehaviour, IInteractable, IInteractionIcon, IInteractionAnchor, ISpiritSenseRelevant
    {
        [SerializeField] private string _areaKey = "a";
        [SerializeField] private string _prompt = "Start";
        [SerializeField] private ChallengeType _challengeType = ChallengeType.WaterSampling;
        [Tooltip("Item the player must own (empty = none).")]
        [SerializeField] private string _requiredItemId;
        [Tooltip("Optional: where the player is placed for the first-person view.")]
        [SerializeField] private Transform _viewPoint;
        [Tooltip("Optional marker visuals shown while the station is available.")]
        [SerializeField] private GameObject _marker;
        [Tooltip("Height of the floating interact prompt above the station centre.")]
        [SerializeField] private float _promptHeight = 0.4f;

        private AreaMinigameStep _owner;
        private bool _available;
        private bool _running;
        private IAreaMinigame _minigame;

        public string AreaKey => _areaKey;
        public string ChallengeId => ChallengeIdFor(_owner);

        /// <summary>Challenge/save ID for this area under a given step: "{stepId}_{areaKey}".</summary>
        public string ChallengeIdFor(QuestStep owner) => owner != null ? $"{owner.StepId}_{_areaKey}" : _areaKey;
        public bool IsDone { get; private set; }
        public bool IsAvailable => _available && !IsDone;
        public InteractionIcon Icon => InteractionIcon.Interact;
        public Vector3 PromptAnchor => transform.position + Vector3.up * _promptHeight;
        public bool IsSpiritRelevant => IsAvailable && !_running;

        public event Action<AreaStation> Finished;

        private void Awake()
        {
            _minigame = GetComponent<IAreaMinigame>();
            SetMarker(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        public void Activate(AreaMinigameStep owner)
        {
            _owner = owner;
            _available = true;
            SetMarker(!IsDone);
        }

        public void MarkDone(AreaMinigameStep owner)
        {
            _owner = owner;
            bool wasDone = IsDone;
            IsDone = true;
            _available = false;
            SetMarker(false);
            if (!wasDone && !_running) _minigame?.ShowCompletedState();
        }

        /// <summary>Where the first-person camera frames this area from.</summary>
        public Transform ViewPoint => _viewPoint != null ? _viewPoint : transform;

        public void Interact()
        {
            if (!IsAvailable || _running) return;
            if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive) return;
            if (!string.IsNullOrEmpty(_requiredItemId) && Lalim.Inventory.InventoryManager.Instance != null &&
                !Lalim.Inventory.InventoryManager.Instance.HasItem(_requiredItemId))
            {
                Lalim.UI.ZoneBannerUI.Post("You need the right tool for this.");
                return;
            }

            _running = true;
            SetMarker(false);

            if (_viewPoint != null && PlayerController.Instance != null)
            {
                PlayerController.Instance.SetPosition(_viewPoint.position);
                // Yaw only: the view point is pitched down at the work, the diver's body shouldn't be.
                PlayerController.Instance.transform.rotation = Quaternion.Euler(0f, _viewPoint.eulerAngles.y, 0f);
                PlayerController.Instance.SyncFacingToTransform();
            }

            int tasks = _minigame != null ? Mathf.Max(1, _minigame.TaskCount) : 1;
            EventBus.Publish(new ChallengeStartedEvent
            {
                Type = _challengeType,
                ChallengeId = ChallengeId,
                TaskQuantity = tasks,
                TimeLimit = 0f,
                HintBudget = _minigame != null ? _minigame.HintBudget : 0,
                Perspective = ChallengePerspective.FirstPerson
            });
            if (_minigame != null)
            {
                ChallengeManager.Instance?.SetExpectedCompletionTime(_minigame.ExpectedSeconds);
                ChallengeManager.Instance?.SetExpectedFirstActionTime(_minigame.ExpectedFirstActionSeconds);
            }

            if (_minigame != null) _minigame.Play(this);
            else ChallengeManager.Instance?.CompleteChallenge(true);
        }

        public string GetInteractionPrompt() => IsAvailable && !_running ? _prompt : "";

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (!_running || evt.ChallengeId != ChallengeId) return;
            _running = false;
            _minigame?.Stop();

            if (evt.Success)
            {
                IsDone = true;
                _available = false;
                // However the minigame ended, leave the area in its finished look (e.g. the
                // scene's ghost net and trapped creatures are gone, not just the puzzle net).
                _minigame?.ShowCompletedState();
                Finished?.Invoke(this);
            }
            else SetMarker(true);
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            if (!_running || evt.ChallengeId != ChallengeId) return;
            _running = false;
            _minigame?.Stop();
            SetMarker(true); // try again
        }

        // Dying mid-minigame fails it; the player respawns at the area's checkpoint and the marker returns.
        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            if (_running) ChallengeManager.Instance?.FailChallenge("player_died");
        }

        private void SetMarker(bool on)
        {
            if (_marker != null) _marker.SetActive(on);
        }

#if UNITY_EDITOR
        public void EditorSetup(string areaKey, string prompt, ChallengeType type, string requiredItemId, Transform viewPoint, GameObject marker)
        {
            _areaKey = areaKey;
            _prompt = prompt;
            _challengeType = type;
            _requiredItemId = requiredItemId;
            _viewPoint = viewPoint;
            _marker = marker;
        }
#endif
    }
}
