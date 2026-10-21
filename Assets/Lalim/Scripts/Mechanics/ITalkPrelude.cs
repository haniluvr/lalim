using System.Collections;
using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Something that plays before a StatueDialogueRouter's conversation opens (before its fade into
    /// the talk shot): e.g. Bakunawa uncoiling and swimming up to the diver for the first talk.
    /// </summary>
    public interface ITalkPrelude
    {
        /// <summary>True when the prelude should play before this conversation (e.g. only the first time).</summary>
        bool PlayBeforeTalk { get; }

        /// <summary>Runs the prelude; the conversation opens when it ends. <paramref name="talkPoint"/> is where the diver will stand to talk.</summary>
        IEnumerator Prelude(Transform talkPoint);

        /// <summary>
        /// Where the conversation's camera should look after the prelude (e.g. a huge speaker's
        /// face high above the diver); false keeps the statue's usual shot.
        /// </summary>
        bool TryGetTalkFocus(out Vector3 focus);
    }
}
