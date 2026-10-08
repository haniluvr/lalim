using System;
using System.Collections;
using TMPro;
using UnityEngine;
using Lalim.DDA;
using Lalim.Mechanics;
using Lalim.Mechanics.Quest;

namespace Lalim.Challenges
{
    /// <summary>
    /// Zone 2, minigame 1 — water level readings at one area (first person), on the "Water
    /// Sampling Kit" panel (WaterSensorsUI).
    ///
    /// Three arc gauges (pH, Salinity, Temp) each have a needle sweeping back and forth: tap the
    /// gauge's STOP while its needle is in the green band to lock it. The needle stays exactly
    /// where it was stopped and the area's reading shows ("8.1 · Normal"). With all three locked,
    /// tap COMPLETE.
    /// DDA knobs: band width, sweep speed (bandSpeed), speed wobble (needleNoise), and how long a
    /// missed STOP stays locked out (lockDrain, in seconds).
    ///
    /// A hint (the bulb on the panel) glides the next unlocked needle into its green band and locks it.
    /// DDA knob "hints" sets how many (default 2).
    ///
    /// DDA reporting: every STOP tap is a player action; the first lock is the first correct action;
    /// a STOP outside the band is an error. One task per locked gauge (the last on COMPLETE).
    /// </summary>
    public class WaterCalibrationMinigame : MonoBehaviour, IAreaMinigame
    {
        public enum Param { PH, Salinity, Temperature }

        [Serializable]
        public struct Readings
        {
            public float ph;
            public float salinity;
            public float temperature;
        }

        [SerializeField] private string _areaLabel = "Area A";
        [SerializeField] private Readings _readings = new Readings { ph = 8.1f, salinity = 35f, temperature = 27f };
        [SerializeField] private MinigameDifficultyProfile _profile;
        [SerializeField] private GameObject _kitModel;

        [Header("Water Sampling Kit panel")]
        [SerializeField] private TMP_FontAsset _titleFont;
        [SerializeField] private TMP_FontAsset _bodyFont;
        [SerializeField] private Sprite _handIcon;

        // Healthy reef water (Haik's dialogue teaches these).
        public static readonly Vector2 PhRange = new Vector2(7.9f, 8.4f);
        public static readonly Vector2 SalinityRange = new Vector2(33f, 37f);
        public static readonly Vector2 TemperatureRange = new Vector2(24f, 29f);

        public int TaskCount => 3;
        public float ExpectedSeconds => _profile != null ? _profile.expectedSeconds : 60f;
        public float ExpectedFirstActionSeconds => _profile != null ? _profile.expectedFirstActionSeconds : 5f;
        public int HintBudget => _profile != null && _profile.Has(Level1DifficultyKnobs.Hints) ? Mathf.Max(0, _profile.GetInt(Level1DifficultyKnobs.Hints)) : 2;

        private MinigameStage _stage;
        private AreaStation _station;
        private WaterSensorsUI _ui;
        private GameObject _kit;
        private Coroutine _run;

        // ───────────────────────── IAreaMinigame ─────────────────────────

        public void Play(AreaStation station)
        {
            _station = station;
            _stage ??= new MinigameStage(this);
            _stage.Enter(station.ViewPoint);
            _ui = WaterSensorsUI.Create(transform, _titleFont, _bodyFont, _handIcon);
            SpawnKit();
            _run = StartCoroutine(Run());
        }

        public void Stop()
        {
            if (_run != null) StopCoroutine(_run);
            _run = null;
            if (_ui != null) Destroy(_ui.gameObject);
            if (_kit != null) Destroy(_kit);
            _stage?.Exit();
        }

        public void ShowCompletedState() { }

        // ───────────────────────── Flow ─────────────────────────

        // The gauge scales (the healthy ranges sit inside them).
        private static readonly Vector2 PhScale = new Vector2(6f, 9f);
        private static readonly Vector2 SalinityScale = new Vector2(28f, 42f);
        private static readonly Vector2 TemperatureScale = new Vector2(20f, 30f);

        private static Vector2 ScaleOf(Param p) => p == Param.PH ? PhScale : p == Param.Salinity ? SalinityScale : TemperatureScale;
        private static Vector2 HealthyOf(Param p) => p == Param.PH ? PhRange : p == Param.Salinity ? SalinityRange : TemperatureRange;

        private static string Tick(Param p, float v) => p == Param.PH ? v.ToString("0.0") : v.ToString("0");

        private float Knob(string key, float fallback) =>
            _profile != null && _profile.Has(key) ? _profile.Get(key) : fallback;

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(0.6f); // camera blend

            float bandWidth = Knob(Level1DifficultyKnobs.BandWidth, 0.22f);
            float speed = 0.3f + Knob(Level1DifficultyKnobs.BandSpeed, 0.45f) * 0.8f;   // arc lengths per second
            float wobble = Knob(Level1DifficultyKnobs.NeedleNoise, 0.2f);
            float missLockout = Knob(Level1DifficultyKnobs.LockDrain, 0.6f);

            Param[] order = { Param.PH, Param.Salinity, Param.Temperature };
            var needle = new float[3];
            var dir = new float[3];
            var seed = new float[3];
            var locked = new bool[3];
            var lockoutUntil = new float[3];
            var band = new Vector2[3];

            _ui.SetSubtitle("Tap STOP when the needle is in the green to take each reading.");
            for (int i = 0; i < 3; i++)
            {
                Param p = order[i];
                Vector2 scale = ScaleOf(p), healthy = HealthyOf(p);
                float mid = Mathf.InverseLerp(scale.x, scale.y, (healthy.x + healthy.y) * 0.5f);
                float half = bandWidth * 0.5f;
                mid = Mathf.Clamp(mid, half + 0.04f, 1f - half - 0.04f);
                band[i] = new Vector2(mid - half, mid + half);
                string maxLabel = p == Param.Salinity ? $"ppt {Tick(p, scale.y)}" : p == Param.Temperature ? $"{Tick(p, scale.y)}°C" : Tick(p, scale.y);
                _ui.SetupGauge(i, Tick(p, scale.x), maxLabel, band[i],
                               Tick(p, Mathf.Lerp(scale.x, scale.y, band[i].x)), Tick(p, Mathf.Lerp(scale.x, scale.y, band[i].y)));
                needle[i] = UnityEngine.Random.Range(0.05f, 0.95f);
                dir[i] = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                seed[i] = UnityEngine.Random.value * 10f;
                _ui.SetNeedle(i, needle[i]);
            }

            int pressed = -1;
            _ui.StopPressed += i => pressed = i;
            bool complete = false;
            _ui.CompletePressed += () => complete = true;
            bool hint = false;
            _ui.HintPressed += () => hint = true;
            _ui.SetHints(Lalim.UI.HintBulb.Remaining(HintBudget));
            int gliding = -1;            // a hint is easing this needle into its band
            float glideFrom = 0f, glideT = 0f;
            const float GlideSeconds = 0.5f;

            int lockedCount = 0;
            bool firstLock = true;
            float t = 0f;
            while (lockedCount < 3)
            {
                float dt = Time.deltaTime;
                t += dt;
                // A hint: the next unlocked needle glides to the middle of its band and locks.
                if (hint)
                {
                    hint = false;
                    int next = -1;
                    for (int i = 0; i < 3 && next < 0; i++) if (!locked[i]) next = i;
                    if (gliding < 0 && next >= 0 && ChallengeManager.Instance != null && ChallengeManager.Instance.UseHint())
                    {
                        gliding = next;
                        glideFrom = needle[next];
                        glideT = 0f;
                    }
                    _ui.SetHints(Lalim.UI.HintBulb.Remaining(HintBudget));
                }
                if (gliding >= 0)
                {
                    int i = gliding;
                    glideT += dt / GlideSeconds;
                    needle[i] = Mathf.Lerp(glideFrom, (band[i].x + band[i].y) * 0.5f, Mathf.SmoothStep(0f, 1f, glideT));
                    _ui.SetNeedle(i, needle[i]);
                    if (glideT >= 1f)
                    {
                        gliding = -1;
                        locked[i] = true;
                        lockedCount++;
                        Lalim.Core.Sfx.Play(Lalim.Core.SfxCue.Correct);
                        Settle(i, needle[i], order[i]);
                        if (lockedCount < 3) ChallengeManager.Instance?.ReportTaskCompleted();
                    }
                }

                for (int i = 0; i < 3; i++)
                {
                    if (locked[i] || i == gliding) continue;
                    // Ping-pong sweep; the speed wobbles a little so the rhythm can't just be counted.
                    float v = speed * (1f + wobble * 1.5f * Mathf.Sin(t * 2.3f + seed[i]) + i * 0.08f);
                    needle[i] += dir[i] * v * dt;
                    if (needle[i] > 1f) { needle[i] = 2f - needle[i]; dir[i] = -1f; }
                    if (needle[i] < 0f) { needle[i] = -needle[i]; dir[i] = 1f; }
                    _ui.SetNeedle(i, needle[i]);
                }

                // Keyboard for the editor: 1/2/3 stop a gauge.
                for (int i = 0; i < 3; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) pressed = i;

                if (pressed >= 0)
                {
                    int i = pressed;
                    pressed = -1;
                    if (!locked[i] && i != gliding && Time.time >= lockoutUntil[i])
                    {
                        ChallengeManager.Instance?.ReportPlayerAction();
                        if (needle[i] >= band[i].x && needle[i] <= band[i].y)
                        {
                            locked[i] = true;
                            lockedCount++;
                            if (firstLock) { firstLock = false; ChallengeManager.Instance?.ReportCorrectAction(); }
                            Lalim.Core.Sfx.Play(Lalim.Core.SfxCue.Correct);
                            Settle(i, needle[i], order[i]);
                            if (lockedCount < 3) ChallengeManager.Instance?.ReportTaskCompleted();
                        }
                        else
                        {
                            ChallengeManager.Instance?.ReportError();
                            Lalim.Core.Sfx.Play(Lalim.Core.SfxCue.Wrong);
                            lockoutUntil[i] = Time.time + missLockout;
                            _ui.Miss(i, missLockout);
                        }
                    }
                }

                _ui.SetPulse(t);
                yield return null;
            }

            // All three read: COMPLETE (or Enter/Space in the editor) reports the last task → Stop().
            _ui.SetCompleteReady();
            _ui.SetHints(0);   // nothing left to hint
            while (!complete && !Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.Space))
            {
                t += Time.deltaTime;
                _ui.SetPulse(t);
                yield return null;
            }
            ChallengeManager.Instance?.ReportTaskCompleted();
        }

        /// <summary>The stopped needle stays put; the area's reading is shown under the gauge.</summary>
        private void Settle(int i, float at, Param p)
        {
            float value = ValueOf(p);
            _ui.Lock(i, $"{Tick(p, value)}{(p == Param.Salinity ? " ppt" : p == Param.Temperature ? "°C" : "")} · {StatusOf(p, value)}", IsNormal(p, value));
            _ui.SetNeedle(i, at);
        }

        // ───────────────────────── Data ─────────────────────────

        private float ValueOf(Param p) => p == Param.PH ? _readings.ph : p == Param.Salinity ? _readings.salinity : _readings.temperature;

        /// <summary>"Normal" inside the healthy range, otherwise "Low" / "High".</summary>
        public static string StatusOf(Param p, float v)
        {
            if (IsNormal(p, v)) return "Normal";
            Vector2 r = p == Param.PH ? PhRange : p == Param.Salinity ? SalinityRange : TemperatureRange;
            return v < r.x ? "Low" : "High";
        }

        public static bool IsNormal(Param p, float v)
        {
            Vector2 r = p == Param.PH ? PhRange : p == Param.Salinity ? SalinityRange : TemperatureRange;
            return v >= r.x && v <= r.y;
        }

        public static string FormatValue(Param p, float v) =>
            p == Param.PH ? $"pH {v:0.0}" : p == Param.Salinity ? $"Salinity {v:0} ppt" : $"Temp {v:0}°C";

        private void SpawnKit()
        {
            if (_kitModel == null || _stage.Camera == null) return;
            _kit = Instantiate(_kitModel, _stage.Camera.transform);
            _kit.name = "[KitViewModel]";
            foreach (Collider c in _kit.GetComponentsInChildren<Collider>()) Destroy(c);
            // Normalise to ~0.3 m and park it in the lower right of the view.
            Bounds b = new Bounds(_kit.transform.position, Vector3.zero);
            foreach (Renderer r in _kit.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            float largest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (largest > 0.0001f) _kit.transform.localScale *= 0.3f / largest;
            _kit.transform.localPosition = new Vector3(0.32f, -0.22f, 0.75f);
            _kit.transform.localRotation = Quaternion.Euler(-10f, 205f, 5f);
        }

#if UNITY_EDITOR
        public void EditorSetupPanel(TMP_FontAsset titleFont, TMP_FontAsset bodyFont, Sprite handIcon)
        {
            _titleFont = titleFont;
            _bodyFont = bodyFont;
            _handIcon = handIcon;
        }

        public void EditorSetup(string areaLabel, Readings readings, MinigameDifficultyProfile profile, GameObject kitModel)
        {
            _areaLabel = areaLabel;
            _readings = readings;
            _profile = profile;
            _kitModel = kitModel;
        }
#endif
    }
}
