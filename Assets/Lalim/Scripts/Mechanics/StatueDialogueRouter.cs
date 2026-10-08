using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Lalim.UI;

namespace Lalim.Mechanics
{
    /// <summary>
    /// A deity statue (Amansinaya, Haik, Neguno). Shows the chat icon when the player can talk,
    /// plays the quest dialogue once the level flow arms it, and can play the closing dialogue,
    /// short barks and an idle line afterwards.
    ///
    /// Implements IInteractable so PlayerController's raycast + HUD action button just work.
    /// </summary>
    [DisallowMultipleComponent]
    public class StatueDialogueRouter : MonoBehaviour, IInteractable, IInteractionIcon
    {
        [Serializable]
        public class NamedLines
        {
            public string key;
            public List<DialogueLine> lines = new List<DialogueLine>();
        }

        [Header("Identity")]
        [SerializeField] private string _deityName = "Amansinaya";

        [Header("Quest dialogue (gives the zone's quest)")]
        [SerializeField] private List<DialogueLine> _questLines = new List<DialogueLine>();

        [Header("Closing dialogue (after the zone's last minigame)")]
        [SerializeField] private List<DialogueLine> _closingLines = new List<DialogueLine>();

        [Header("Idle line (re-talking after the quest)")]
        [SerializeField] private List<DialogueLine> _idleLines = new List<DialogueLine>();

        [Header("Mid-quest barks, played by quest steps by key")]
        [SerializeField] private List<NamedLines> _barks = new List<NamedLines>();

        [Header("Conversation framing")]
        [Tooltip("Where the diver floats while talking, facing the statue's face (move it to adjust).")]
        [SerializeField] private Transform _talkPoint;
        [Tooltip("Where the dugong floats while talking, beside the diver (optional).")]
        [SerializeField] private Transform _dugongPoint;
        [Tooltip("The fixed camera shot while talking: a 3/4 view of the statue's face on the left, diver and dugong on the right.")]
        [SerializeField] private Transform _talkCamera;
        [Tooltip("Dialogues the statue starts on its own (closing, barks) are framed too when the diver is this close.")]
        [SerializeField, Min(0f)] private float _frameWithin = 30f;
        [Tooltip("Fade to black (and back) while the diver, dugong and camera are put in place.")]
        [SerializeField, Min(0.05f)] private float _fadeSeconds = 0.35f;

        private bool _fadingIn;
        private Coroutine _release;
        private bool _releaseFading;

        [Tooltip("Legacy hook: invoked after the quest dialogue ends.")]
        public UnityEvent onIntroComplete;

        private bool _questArmed;
        private bool _questDone;
        private bool _idleAllowed;
        private Action _onQuestComplete;
        private List<DialogueLine> _pendingTalk;
        private Action _onPendingTalkStarted;
        private Action _onPendingTalkDone;

        public string DeityName => _deityName;
        public bool HasPendingQuest => _questArmed && !_questDone;
        public bool QuestDone => _questDone;
        /// <summary>A one-off conversation (e.g. the relic gift) is waiting for the player.</summary>
        public bool HasPendingTalk => _pendingTalk != null;
        public InteractionIcon Icon => InteractionIcon.Chat;

        /// <summary>Called by the level flow when this statue's quest is the current step.</summary>
        public void ArmQuest(Action onQuestComplete)
        {
            _questArmed = true;
            _onQuestComplete = onQuestComplete;
        }

        /// <summary>Restoring a save: the quest was already given.</summary>
        public void MarkQuestDone(bool allowIdleTalk)
        {
            _questArmed = true;
            _questDone = true;
            _idleAllowed = allowIdleTalk;
            _onQuestComplete = null;
        }

        /// <summary>
        /// Waits for the player to swim over and talk, then plays <paramref name="lines"/>.
        /// <paramref name="onStarted"/> runs as the conversation opens, <paramref name="onDone"/> when it ends.
        /// </summary>
        public void ArmTalk(List<DialogueLine> lines, Action onStarted, Action onDone)
        {
            _pendingTalk = lines;
            _onPendingTalkStarted = onStarted;
            _onPendingTalkDone = onDone;
        }

        public void CancelTalk()
        {
            _pendingTalk = null;
            _onPendingTalkStarted = null;
            _onPendingTalkDone = null;
        }

        /// <summary>The lines stored under a bark key, or null.</summary>
        public List<DialogueLine> GetBark(string key)
        {
            foreach (NamedLines b in _barks) if (b.key == key) return b.lines;
            return null;
        }

        /// <summary>Lets the player re-talk for the idle line (after the zone's work is done).</summary>
        public void AllowIdleTalk(bool allowed) => _idleAllowed = allowed;

        public void Interact()
        {
            if (_fadingIn || DialogueManager.Instance == null || DialogueManager.Instance.IsOpen) return;

            if (HasPendingQuest)
            {
                Open(_questLines, () =>
                {
                    _questDone = true;
                    Action callback = _onQuestComplete;
                    _onQuestComplete = null;
                    onIntroComplete?.Invoke();
                    callback?.Invoke();
                }, null, true);
            }
            else if (HasPendingTalk)
            {
                List<DialogueLine> lines = _pendingTalk;
                Action started = _onPendingTalkStarted;
                Action done = _onPendingTalkDone;
                CancelTalk();
                Open(lines, done, started, true);
            }
            else if (_questDone && _idleAllowed && _idleLines.Count > 0)
            {
                Open(_idleLines, null, null, true);
            }
        }

        public string GetInteractionPrompt()
        {
            if (HasPendingQuest || HasPendingTalk) return "Talk";
            if (_questDone && _idleAllowed && _idleLines.Count > 0) return "Talk";
            return "";
        }

        /// <summary>The closing lines (the relic gift carries straight on into them).</summary>
        public List<DialogueLine> ClosingLines => _closingLines;

        /// <summary>The closing was already said as part of another conversation (the relic gift).</summary>
        public void MarkClosingSaid() => _closingSaid = true;

        private bool _closingSaid;

        /// <summary>Plays the closing dialogue (instantly calls back if there is none, or it was already said).</summary>
        public void PlayClosing(Action onDone)
        {
            if (_closingSaid) { _closingSaid = false; onDone?.Invoke(); return; }
            Play(_closingLines, onDone);
        }

        /// <summary>
        /// Plays a named bark (instantly calls back if the key is unknown). <paramref name="frame"/>
        /// false keeps the diver where they are (e.g. already placed at a minigame).
        /// </summary>
        public void PlayBark(string key, Action onDone, bool frame = true)
        {
            foreach (NamedLines b in _barks)
            {
                if (b.key == key) { Play(b.lines, onDone, frame); return; }
            }
            onDone?.Invoke();
        }

        private void Play(List<DialogueLine> lines, Action onDone, bool frame = true)
        {
            if (lines == null || lines.Count == 0 || DialogueManager.Instance == null)
            {
                onDone?.Invoke();
                return;
            }
            PlayerController pc = PlayerController.Instance;
            bool near = frame && pc != null && Vector3.Distance(pc.transform.position, transform.position) <= _frameWithin;
            Open(lines, onDone, null, near);
        }

        // ─────────────────────────────────────────────
        // Conversation framing
        // ─────────────────────────────────────────────
        //
        // Opening: fade to black, put the diver (and dugong) at the talk spots and cut the camera to
        // the fixed shot, then fade back in as the dialogue opens. The camera stays put (it doesn't
        // follow the diver) until the conversation ends, then fades out and back behind the diver.

        private void OnEnable() => Lalim.Core.EventBus.Subscribe<Lalim.Core.DialogueEndedEvent>(OnDialogueEnded);

        private void OnDisable()
        {
            Lalim.Core.EventBus.Unsubscribe<Lalim.Core.DialogueEndedEvent>(OnDialogueEnded);
            // Interrupted mid-fade (scene change): never leave the screen black or the diver frozen.
            if (_fadingIn || _releaseFading)
            {
                _fadingIn = false;
                _releaseFading = false;
                _release = null;
                if (DialogueManager.Instance == null || !DialogueManager.Instance.IsOpen)
                {
                    PlayerController.Instance?.SetMovementEnabled(true);
                    InputManager.Instance?.SetInputEnabled(true);
                }
                ScreenFader fader = ScreenFader.Instance;
                if (fader != null) fader.StartCoroutine(fader.FadeIn(0.2f));
            }
        }

        private bool CanFrame => _talkPoint != null && _talkCamera != null && PlayerController.Instance != null && CameraController.Instance != null;

        /// <summary>This statue's shot is already up (e.g. the relic gift flowing into the closing).</summary>
        private bool IsFramedHere => _talkCamera != null && CameraController.Instance != null && CameraController.Instance.Shot == _talkCamera;

        private static bool DialogueOpen => DialogueManager.Instance != null && DialogueManager.Instance.IsOpen;

        private void Open(List<DialogueLine> lines, Action onDone, Action started, bool frame)
        {
            if (frame && !_fadingIn && CanFrame && !IsFramedHere)
            {
                StartCoroutine(FadeIntoTalk(lines, onDone, started));
                return;
            }
            // A conversation that follows right on from the last one keeps the shot.
            if (_release != null && !_releaseFading) { StopCoroutine(_release); _release = null; }
            DialogueManager.Instance.StartDialogue(lines, onDone);
            started?.Invoke();
        }

        private System.Collections.IEnumerator FadeIntoTalk(List<DialogueLine> lines, Action onDone, Action started)
        {
            _fadingIn = true;
            PlayerController.Instance.SetMovementEnabled(false);   // the dialogue re-enables both when it ends
            InputManager.Instance?.SetInputEnabled(false);

            ScreenFader fader = ScreenFader.Instance;
            yield return fader.FadeOut(_fadeSeconds);

            PlaceForTalk();
            CameraController.Instance.SetShot(_talkCamera, snap: true);
            _fadingIn = false;
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(lines, onDone);
                started?.Invoke();
            }
            else onDone?.Invoke();

            yield return fader.FadeIn(_fadeSeconds);
        }

        private void PlaceForTalk()
        {
            PlayerController pc = PlayerController.Instance;
            Transform t = pc.transform;
            var cc = t.GetComponent<CharacterController>();
            bool ccOn = cc != null && cc.enabled;
            if (ccOn) cc.enabled = false;
            t.SetPositionAndRotation(_talkPoint.position, Quaternion.Euler(0f, _talkPoint.eulerAngles.y, 0f));
            if (ccOn) cc.enabled = true;
            pc.SyncFacingToTransform();

            if (_dugongPoint != null && Lalim.Abilities.DugongCompanion.Instance != null)
                Lalim.Abilities.DugongCompanion.Instance.HoldAt(_dugongPoint);
        }

        private void OnDialogueEnded(Lalim.Core.DialogueEndedEvent evt)
        {
            if (!IsFramedHere || _release != null) return;
            _release = StartCoroutine(ReleaseShot());
        }

        private System.Collections.IEnumerator ReleaseShot()
        {
            // The next step can open another conversation straight away (relic gift → closing).
            yield return new WaitForSecondsRealtime(0.1f);
            if (DialogueOpen) { _release = null; yield break; }

            _releaseFading = true;
            ScreenFader fader = ScreenFader.Instance;
            yield return fader.FadeOut(_fadeSeconds);
            if (!DialogueOpen)
            {
                if (CameraController.Instance != null) CameraController.Instance.ClearShot(snap: true);
                if (Lalim.Abilities.DugongCompanion.Instance != null) Lalim.Abilities.DugongCompanion.Instance.ReleaseHold();
            }
            _releaseFading = false;
            _release = null;
            yield return fader.FadeIn(_fadeSeconds);
        }

#if UNITY_EDITOR
        public void EditorSetTalkFraming(Transform talkPoint, Transform dugongPoint, Transform talkCamera)
        {
            _talkPoint = talkPoint;
            _dugongPoint = dugongPoint;
            _talkCamera = talkCamera;
        }
#endif

#if UNITY_EDITOR
        /// <summary>Editor builder: fills the scripts (only when empty, so hand edits are kept).</summary>
        public void EditorFillIfEmpty(string deityName, List<DialogueLine> quest, List<DialogueLine> closing,
                                      List<DialogueLine> idle, List<NamedLines> barks)
        {
            if (string.IsNullOrEmpty(_deityName) || _deityName == "Amansinaya") _deityName = deityName;
            if (_questLines.Count == 0) _questLines = quest;
            if (_closingLines.Count == 0) _closingLines = closing;
            if (_idleLines.Count == 0) _idleLines = idle;
            if (_barks.Count == 0) _barks = barks;
        }

        /// <summary>Editor builder: adds or replaces one named bark.</summary>
        public void EditorSetBark(string key, List<DialogueLine> lines)
        {
            _barks.RemoveAll(b => b.key == key);
            _barks.Add(new NamedLines { key = key, lines = lines });
        }

        /// <summary>Editor builder: replaces the scripts (used when migrating the old placeholder lines).</summary>
        public void EditorReplaceScripts(string deityName, List<DialogueLine> quest, List<DialogueLine> closing,
                                         List<DialogueLine> idle, List<NamedLines> barks)
        {
            _deityName = deityName;
            _questLines = quest;
            _closingLines = closing;
            _idleLines = idle;
            _barks = barks;
        }
#endif
    }
}
