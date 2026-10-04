using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lalim.Core;

namespace Lalim.Abilities
{
    /// <summary>
    /// Saves and restores the player's HP, stamina, the dugong's health and every skill's cooldown
    /// and charge, so resuming a saved game picks up exactly where the player left off.
    /// Respawning at a checkpoint still refills everything (CheckpointManager).
    /// </summary>
    public static class PlayerVitals
    {
        /// <summary>Called on every save. Outside a gameplay scene the last captured values are kept.</summary>
        public static void Capture(SaveData save)
        {
            if (save == null || PlayerHealth.Instance == null) return;
            PlayerHealth hp = PlayerHealth.Instance;

            var v = new SavedVitals
            {
                valid = true,
                scene = SceneManager.GetActiveScene().name,
                playerHp = hp.IsDead ? hp.Max : hp.Current,   // dying means a full respawn anyway
                stamina = PlayerStamina.Instance != null ? PlayerStamina.Instance.Current : -1f,
                dugongHp = DugongHealth.Instance != null ? DugongHealth.Instance.Current : -1f,
                dugongExhausted = DugongHealth.Instance != null && DugongHealth.Instance.IsExhausted,
            };
            if (AbilityController.Instance != null)
            {
                foreach (AbilityId id in Enum.GetValues(typeof(AbilityId)))
                {
                    AbilityBase a = AbilityController.Instance.Get(id);
                    if (a == null) continue;
                    v.abilities.Add(new SavedAbility { id = (int)id, cooldown = a.CooldownForSave, charge = a.ChargeForSave });
                }
            }
            save.vitals = v;
        }

        /// <summary>
        /// Puts the saved values back when a loaded save resumes in the scene they were captured in.
        /// Runs once per load (the values are then marked used), so later visits start normally.
        /// </summary>
        public static void TryRestore()
        {
            SaveManager sm = SaveManager.Instance;
            SaveData save = sm != null ? sm.CurrentSave : null;
            SavedVitals v = save != null ? save.vitals : null;
            if (v == null || !v.valid || sm.Origin != SaveManager.SessionOrigin.LoadedGame) return;
            if (v.scene != SceneManager.GetActiveScene().name) return;
            v.valid = false;

            if (PlayerHealth.Instance != null) PlayerHealth.Instance.RestoreCurrent(v.playerHp);
            if (PlayerStamina.Instance != null && v.stamina >= 0f) PlayerStamina.Instance.RestoreCurrent(v.stamina);
            if (DugongHealth.Instance != null && v.dugongHp >= 0f) DugongHealth.Instance.RestoreCurrent(v.dugongHp, v.dugongExhausted);
            if (AbilityController.Instance != null && v.abilities != null)
            {
                foreach (SavedAbility s in v.abilities)
                {
                    AbilityBase a = AbilityController.Instance.Get((AbilityId)s.id);
                    if (a != null) a.RestoreFromSave(s.cooldown, s.charge);
                }
            }
            Debug.Log($"[PlayerVitals] Restored HP {v.playerHp:F0}, stamina {v.stamina:F1}, {v.abilities.Count} skills.");
        }
    }
}
