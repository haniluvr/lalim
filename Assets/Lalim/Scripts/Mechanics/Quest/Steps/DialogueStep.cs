using UnityEngine;

namespace Lalim.Mechanics.Quest
{
    /// <summary>Plays a statue's closing dialogue or a named bark, then continues.</summary>
    public class DialogueStep : QuestStep
    {
        public enum Kind { Closing, Bark }

        [SerializeField] private Kind _kind = Kind.Closing;
        [SerializeField] private string _barkKey;
        [Tooltip("Defaults to the zone's statue.")]
        [SerializeField] private StatueDialogueRouter _statue;

        protected override void OnBegin()
        {
            StatueDialogueRouter statue = _statue != null ? _statue : (Zone != null ? Zone.Statue : null);
            if (statue == null) { Complete(); return; }

            if (_kind == Kind.Closing) statue.PlayClosing(Complete);
            else statue.PlayBark(_barkKey, Complete);
        }

        private void DebugForceComplete() => Complete();

#if UNITY_EDITOR
        public void EditorSetupDialogue(Kind kind, string barkKey)
        {
            _kind = kind;
            _barkKey = barkKey;
        }
#endif
    }
}
