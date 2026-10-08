using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// What each scene sounds like, for the music that carries across scene loads (MusicDirector).
    /// Lives at Resources/Audio/MusicLibrary.
    ///
    ///  - Track: this scene's BGM. If it's already playing (carried over), it just continues.
    ///  - KeepPrevious: whatever was playing carries on (name input, cutscene).
    ///  - Loading: carries on from the scene before; arriving from the main menu (or from nothing),
    ///    it starts the BGM of the scene being loaded instead, so a new game opens on the intro
    ///    music and Continue opens on the saved level's music.
    ///  - OwnMusic: the scene plays its own music (main menu); the carried music fades out.
    ///  - Level: the level's own music system takes over seamlessly (ProximityMusic), picking up
    ///    the carried track where it is if it's the same one.
    /// </summary>
    [CreateAssetMenu(menuName = "Lalim/Audio/Music Library", fileName = "MusicLibrary")]
    public class MusicLibrary : ScriptableObject
    {
        public enum Mode { Track, KeepPrevious, Loading, OwnMusic, Level }

        [Serializable]
        public class SceneMusic
        {
            public string scene;
            public Mode mode = Mode.Track;
            [Tooltip("Track mode: the scene's BGM. Level mode: the level's opening BGM (used when a loading screen leads into it from the menu).")]
            public AudioClip bgm;
            [Tooltip("Ambience loop for the whole scene (levels set theirs per zone instead).")]
            public AudioClip ambience;
        }

        public List<SceneMusic> scenes = new List<SceneMusic>();

        public SceneMusic For(string sceneName)
        {
            foreach (SceneMusic s in scenes) if (s != null && s.scene == sceneName) return s;
            return null;
        }
    }
}
