using System.Collections;
using UnityEngine;
using Lalim.Core;
using Lalim.UI;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Teleport scene: the first time the player arrives after a region unlocks the next one,
    /// a light burst plays at its teleporter (no banner: the portal itself coming alive says it).
    /// Shown once per save (tutorialFlags).
    /// </summary>
    public class TeleportUnlockReveal : MonoBehaviour
    {
        [SerializeField] private Region _region = Region.Visayas;
        [Tooltip("Where the burst plays (the region's teleporter). Defaults to this object.")]
        [SerializeField] private Transform _teleporter;
        [SerializeField, Min(0f)] private float _delay = 1.2f;

        private string Flag => $"seen_unlock_{_region}";

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(_delay);

            if (GameManager.Instance == null || !GameManager.Instance.IsRegionAccessible(_region)) yield break;
            SaveData save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
            if (save != null && save.tutorialFlags.Contains(Flag)) yield break;
            save?.tutorialFlags.Add(Flag);

            Burst(_teleporter != null ? _teleporter.position : transform.position);
            SaveManager.Instance?.AutoSave();
        }

        private static void Burst(Vector3 at)
        {
            var go = new GameObject("[UnlockBurst]");
            go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.45f, 1f, 0.95f);
            light.range = 12f;
            light.intensity = 6f;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.startLifetime = 1.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startColor = new Color(0.45f, 1f, 0.95f);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            ps.Emit(80);
            Object.Destroy(go, 3f);
        }
    }
}
