using UnityEngine;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Plays one of a statue's conversations, then continues:
    ///  - Closing: the closing dialogue, right away.
    ///  - Bark: a named bark, right away.
    ///  - Talk: a named bark, but only once the player swims back to the statue and taps Talk —
    ///    with the same framing as the first conversation (e.g. Amansinaya after the trash).
    /// </summary>
    public class DialogueStep : QuestStep
    {
        public enum Kind { Closing, Bark, Talk }

        [SerializeField] private Kind _kind = Kind.Closing;
        [SerializeField] private string _barkKey;
        [Tooltip("Defaults to the zone's statue.")]
        [SerializeField] private StatueDialogueRouter _statue;

        private StatueDialogueRouter Statue => _statue != null ? _statue : (Zone != null ? Zone.Statue : null);

        protected override void OnBegin()
        {
            StatueDialogueRouter statue = Statue;
            if (statue == null) { Complete(); return; }

            switch (_kind)
            {
                case Kind.Closing: statue.PlayClosing(Complete); break;
                case Kind.Bark: statue.PlayBark(_barkKey, Complete); break;
                case Kind.Talk:
                    var lines = statue.GetBark(_barkKey);
                    if (lines == null || lines.Count == 0)
                    {
                        Debug.LogWarning($"[DialogueStep] {StepId}: no '{_barkKey}' lines on {statue.name}.");
                        Complete();
                        return;
                    }
                    ObjectiveTracker.Instance?.SetObjective(statue.transform);
                    statue.ArmTalk(lines, () => ObjectiveTracker.Instance?.Clear(), Complete);
                    break;
            }
        }

        private void DebugForceComplete()
        {
            if (_kind == Kind.Talk) Statue?.CancelTalk();
            Complete();
        }

#if UNITY_EDITOR
        public void EditorSetupDialogue(Kind kind, string barkKey)
        {
            _kind = kind;
            _barkKey = barkKey;
        }
#endif
    }
}
