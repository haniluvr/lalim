using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Lalim.Abilities;
using Lalim.Core;
using Lalim.DDA;
using Lalim.Mechanics;

namespace Lalim.DevTools
{
    /// <summary>
    /// Dev-only tuning aid (editor and development builds), spawned by DevBootstrapper.
    ///  F9  — toggle on-screen readout: HP, stamina, every ability's state, difficulty, tuning values, toxic severity.
    ///  F10 — toggle infinite stamina.
    ///  F11 — cycle difficulty 0 → 0.5 → 1.
    /// </summary>
    public class AbilityDebugOverlay : MonoBehaviour
    {
        private static AbilityDebugOverlay s_Instance;

        private bool _visible;
        private bool _infiniteStamina;
        private int _difficultyPreset = 1;
        private readonly float[] _presets = { 0f, 0.5f, 1f };
        private readonly StringBuilder _sb = new StringBuilder(1024);
        private GUIStyle _style;

        public static void Ensure()
        {
            if (!Debug.isDebugBuild || s_Instance != null) return;
            var go = new GameObject("[AbilityDebugOverlay]");
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<AbilityDebugOverlay>();
        }

        private void Update()
        {
            if (Pressed(Key.F9, KeyCode.F9)) _visible = !_visible;

            if (Pressed(Key.F10, KeyCode.F10))
            {
                _infiniteStamina = !_infiniteStamina;
                if (PlayerStamina.Instance != null)
                {
                    PlayerStamina.Instance.InfiniteStamina = _infiniteStamina;
                    if (_infiniteStamina) PlayerStamina.Instance.RefillToFull();
                }
                Debug.Log($"[Dev] Infinite stamina {(_infiniteStamina ? "ON" : "OFF")}");
            }

            if (Pressed(Key.F11, KeyCode.F11) && DDAManager.Instance != null)
            {
                _difficultyPreset = (_difficultyPreset + 1) % _presets.Length;
                DDAManager.Instance.SetDifficulty(_presets[_difficultyPreset]);
                Debug.Log($"[Dev] Difficulty set to {_presets[_difficultyPreset]:F1}");
            }

            // Keep the flag applied if the player (and its PlayerStamina) was recreated by a scene load.
            if (_infiniteStamina && PlayerStamina.Instance != null) PlayerStamina.Instance.InfiniteStamina = true;
        }

        private static bool Pressed(Key key, KeyCode legacy)
        {
            if (Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame) return true;
            return Input.GetKeyDown(legacy);
        }

        private void OnGUI()
        {
            if (!_visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true };
                _style.normal.textColor = Color.white;
            }

            _sb.Clear();
            _sb.AppendLine("<b>ABILITY DEBUG</b>  (F9 hide · F10 ∞ stamina · F11 difficulty)");

            if (PlayerHealth.Instance != null)
                _sb.AppendLine($"HP {PlayerHealth.Instance.Current:F1}/{PlayerHealth.Instance.Max:F0}{(PlayerHealth.Instance.IsDead ? "  DEAD" : "")}");
            if (PlayerStamina.Instance != null)
                _sb.AppendLine($"Stamina {PlayerStamina.Instance.Current:F2}/{PlayerStamina.Instance.MaxOrbs} (full {PlayerStamina.Instance.FullOrbs}){(_infiniteStamina ? "  ∞" : "")}");

            ToxicWaterTracker toxic = PlayerHealth.Instance != null ? PlayerHealth.Instance.GetComponent<ToxicWaterTracker>() : null;
            _sb.AppendLine($"Toxic {(toxic != null ? toxic.CurrentSeverity.ToString() : "n/a")}");
            if (PlayerController.Instance != null)
                _sb.AppendLine($"Speed {PlayerController.Instance.Velocity.magnitude:F2} m/s  mounted={PlayerController.Instance.IsMounted}  dash={PlayerController.Instance.IsDashing}");

            if (AbilityController.Instance != null)
            {
                _sb.AppendLine($"Hold charge {AbilityController.Instance.HoldChargeNormalized:P0}");
                foreach (AbilityBase a in AbilityController.Instance.All)
                {
                    if (a == null) continue;
                    a.CanActivate(out AbilityBlockReason reason);
                    string timer = a.State == AbilityState.Active ? $"{a.DurationRemaining:F1}s left"
                                 : a.State == AbilityState.Cooldown ? $"cd {a.CooldownRemaining:F1}s" : "";
                    _sb.AppendLine($"  {a.Id,-14} {a.State,-8} {timer,-12} cost {a.CostOrbs}  {(reason == AbilityBlockReason.None ? "" : reason.ToString())}");
                }
            }

            float d = AbilityDifficulty.Current;
            _sb.AppendLine($"Difficulty {d:F3}");
            if (DDAManager.Instance != null)
            {
                _sb.AppendLine($"  tasks ×{DDAManager.Instance.TaskQuantityMultiplier:F2}  hints {DDAManager.Instance.HintBudget}  tier {DDAManager.Instance.PuzzleComplexityTier}");
                if (DDAManager.Instance.Config != null)
                    _sb.AppendLine($"  timer ×{DDAManager.Instance.Config.GetTimerMultiplier(d):F2}");
                _sb.AppendLine($"  last: {DDAManager.Instance.LastSignal}");
            }

            AbilityConfig cfg = AbilityController.Instance != null ? AbilityController.Instance.Config : null;
            if (cfg != null)
            {
                _sb.AppendLine($"  orb regen {cfg.GetOrbRegenSeconds(d):F2}s  toxic ×{cfg.GetToxicDamageMultiplier(d):F2}  mend {cfg.GetSeagrassMendHeal(d):F0}HP");
                _sb.AppendLine($"  sense {cfg.GetSpiritSenseDuration(d):F1}s  glow ×{cfg.GetSpiritGlowIntensity(d):F2}");
            }

            ExplorationMetricsTracker ex = ExplorationMetricsTracker.Instance;
            if (ex != null)
                _sb.AppendLine($"Section '{ex.CurrentSectionId}' {ex.SectionSeconds:F0}s  deaths {ex.SectionDeaths}  near {ex.SectionNearDeaths}  avgHP {ex.AverageHpFraction:P0}  idle {ex.SecondsSinceObjectiveProgress:F0}s");

            GUI.Box(new Rect(10, 10, 560, 20 + 17 * CountLines()), _sb.ToString(), _style);
        }

        private int CountLines()
        {
            int n = 1;
            for (int i = 0; i < _sb.Length; i++) if (_sb[i] == '\n') n++;
            return n;
        }
    }
}
