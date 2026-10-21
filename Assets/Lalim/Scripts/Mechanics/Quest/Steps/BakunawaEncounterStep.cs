using System.Collections;
using UnityEngine;
using Lalim.Challenges.Relocation;
using Lalim.Core;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Padre Burgos, the first meeting with Bakunawa. As the diver comes out of Trench B the sea
    /// starts to shake (Bakunawa is awake) and the diver is held in place; the serpent rises out of
    /// the abyss in a wide spiral and stops face to face with them. Only then does Talk appear, and
    /// the conversation is shot from behind the diver, up into its face. The step completes when
    /// that conversation ends (the relocation minigame's roar follows).
    /// </summary>
    public class BakunawaEncounterStep : QuestStep
    {
        // Getting to Padre Burgos is travel, not a quest: the box starts closed (as for any first talk).
        protected override bool DefaultBoxOpen => false;

        [SerializeField] private Bakunawa _bakunawa;
        [Tooltip("Defaults to the zone's statue (Bakunawa's conversation).")]
        [SerializeField] private StatueDialogueRouter _statue;
        [Tooltip("World X the diver must pass (the mouth of Trench B, just short of the drop into the abyss). 0 = Bakunawa's zone edge + 80 m.")]
        [SerializeField] private float _triggerX;

        [Header("The rise")]
        [Tooltip("Least distance from the diver to its face (further when the cliff edge is further: it rises over the open abyss).")]
        [SerializeField, Min(6f)] private float _faceDistance = 12f;
        [Tooltip("How far above the diver its head stops (looming over them).")]
        [SerializeField] private float _faceHeight = 6f;
        [SerializeField, Min(2f)] private float _riseSeconds = 7f;
        [SerializeField, Min(0f)] private float _shakeStrength = 0.6f;

        [Header("The sea shaking")]
        [Tooltip("A soft particle material (alpha blended) for the silt and grit shaken loose.")]
        [SerializeField] private Material _dustMaterial;
        [Tooltip("Rock models that break off the trench walls and tumble down while it rises.")]
        [SerializeField] private GameObject[] _rockModels = new GameObject[0];
        [SerializeField, Range(0, 16)] private int _fallingRocks = 7;
        [SerializeField] private AudioClip _rockClip;

        [Header("The talk shot (behind the diver, up into its face)")]
        [SerializeField, Min(1f)] private float _cameraBack = 3.6f;
        [Tooltip("About the diver's shoulders: the diver sits low in the shot, its face looms above.")]
        [SerializeField] private float _cameraUp = 0.4f;
        [Tooltip("Off to the diver's right, so they don't hide its face: the diver lower left, its face upper right.")]
        [SerializeField] private float _cameraSide = 1.5f;

        [SerializeField, TextArea(1, 2)] private string _talkText = "Bakunawa has awoken from his slumber. Talk to him.";

        private bool _started;
        private bool _holding;
        private Vector3 _hold;
        private Transform _shot;
        private Lalim.Challenges.Catching.NetBag _bag;

        private StatueDialogueRouter Statue => _statue != null ? _statue : (Zone != null ? Zone.Statue : null);
        private Bakunawa Serpent => _bakunawa != null ? _bakunawa : (_bakunawa = FindFirstObjectByType<Bakunawa>());
        private float TriggerX => _triggerX != 0f ? _triggerX : (Serpent != null ? Serpent.ZoneMinX + 80f : float.MaxValue);

        protected override void OnBegin()
        {
            _started = false;
            _holding = false;
            if (Statue == null || Serpent == null)
            {
                Debug.LogWarning($"[BakunawaEncounter] {StepId}: no Bakunawa or no statue; a plain talk instead.");
                Statue?.ArmQuest(Complete);
                if (Statue == null) Complete();
            }
        }

        private void Update()
        {
            if (!IsRunning) return;
            PlayerController pc = PlayerController.Instance;
            if (pc == null) return;

            if (!_started)
            {
                bool talking = DialogueManager.Instance != null && DialogueManager.Instance.IsOpen;
                if (!talking && pc.transform.position.x >= TriggerX) StartCoroutine(Encounter(pc));
                return;
            }
            // Held in place (but free to tap Talk) until the conversation starts.
            if (_holding && (pc.transform.position - _hold).sqrMagnitude > 0.04f) pc.SetPosition(_hold);
        }

        private IEnumerator Encounter(PlayerController pc)
        {
            _started = true;
            Bakunawa serpent = Serpent;
            StatueDialogueRouter statue = Statue;

            // The sea shakes and the diver stops.
            pc.SetMovementEnabled(false);
            _hold = pc.transform.position;
            Vector3 toward = serpent.transform.position - _hold;
            toward.y = 0f;
            toward = toward.sqrMagnitude > 0.01f ? toward.normalized : pc.transform.forward;
            pc.transform.rotation = Quaternion.LookRotation(toward);
            pc.SyncFacingToTransform();
            serpent.Rumble(0.9f);
            // The towed net-bag would hang between the camera and the serpent: out of sight for the meeting.
            _bag = Lalim.Challenges.Catching.NetBag.Instance;
            if (_bag != null) _bag.gameObject.SetActive(false);

            // Over the diver's shoulder, out over the abyss: it will come up there.
            Vector3 side = Vector3.Cross(Vector3.up, toward);
            Vector3 face = serpent.MeetingPoint(_hold, toward, _faceDistance, _faceHeight);
            _shot = new GameObject("[BakunawaMeetingShot]").transform;
            _shot.position = _hold - toward * _cameraBack + Vector3.up * _cameraUp + side * _cameraSide;
            _shot.rotation = Quaternion.LookRotation(face + Vector3.down * 6f - _shot.position);
            CameraController.Instance?.SetShot(_shot);

            _quaking = true;
            StartCoroutine(Quake(_hold, toward));
            yield return serpent.RiseToFace(_hold, toward, _faceDistance, _riseSeconds, _faceHeight);
            _quaking = false;   // it has come to rest in front of the diver: the sea settles

            // Face to face: the shot swings up to hold the diver and its face, and Talk appears.
            Vector3 head = serpent.HeadPosition;
            Vector3 toDiver = (_hold + Vector3.up * 0.5f - _shot.position).normalized;
            Vector3 toFace = (head + Vector3.up * 0.4f - _shot.position).normalized;
            Quaternion look = Quaternion.LookRotation(toDiver + toFace);   // halfway between them
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                _shot.rotation = Quaternion.Slerp(_shot.rotation, look, Time.deltaTime * 6f);
                yield return null;
            }
            statue.FrameTalkAt(_hold, Quaternion.LookRotation(toward), _shot.position, look,
                               _hold + toward * 0.3f - side * 2.2f - Vector3.up * 0.4f);   // the dugong at the diver's left
            // Its Talk trigger comes up with it, between the two of them.
            statue.MoveTriggerTo(Vector3.Lerp(_hold, head, 0.4f));

            _holding = true;
            pc.SetMovementEnabled(true);   // only to tap Talk: Update keeps the diver in place
            QuestObjectiveUI.Instance?.SetObjective(_talkText, true);
            ObjectiveTracker.Instance?.SetObjective(statue.transform);
            statue.ArmQuest(OnTalked);
        }

        private bool _quaking;

        /// <summary>
        /// The sea shakes while Bakunawa rises: the camera trembles (hard at first, easing as it
        /// nears), silt boils up off the trench floor, grit rains down from above and rocks break
        /// off the walls and tumble past. Settles once it has come to rest.
        /// </summary>
        private IEnumerator Quake(Vector3 at, Vector3 toward)
        {
            float floor = Lalim.Mechanics.SwimBody.SeabedY(at, at.y + 2f);
            var root = new GameObject("[TrenchQuake]").transform;
            root.position = at;
            ParticleSystem silt = Dust(root, new Vector3(at.x, floor + 0.5f, at.z) + toward * 6f, new Vector3(36f, 1f, 30f),
                                       new Color(0.2f, 0.23f, 0.2f, 0.3f), new Vector2(2f, 4.5f), new Vector2(4f, 7f), 30f, Vector3.up * 0.6f, 0f);
            ParticleSystem grit = Dust(root, at + Vector3.up * 16f + toward * 6f, new Vector3(40f, 1f, 34f),
                                       new Color(0.55f, 0.55f, 0.5f, 0.8f), new Vector2(0.05f, 0.14f), new Vector2(3f, 5f), 90f, Vector3.down * 3.5f, 0.4f);
            if (_rockClip != null) GameAudio.PlayFlat(_rockClip, 0.7f);
            StartCoroutine(Rockfall(root, at, toward));

            float start = Time.time, nextRumble = Time.time + 2.2f;
            while (_quaking)
            {
                // Hard at first, still clearly shaking as it comes up out of the dark.
                float k = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((Time.time - start) / Mathf.Max(1f, _riseSeconds)));
                CameraController.Instance?.Shake(_shakeStrength * k, 0.6f);
                if (Time.time >= nextRumble) { Serpent?.Rumble(0.6f); nextRumble = Time.time + 2.6f; }
                yield return new WaitForSeconds(0.2f);
            }
            // Settling: the shaking dies away, the silt hangs in the water and slowly clears.
            CameraController.Instance?.Shake(_shakeStrength * 0.5f, 1.4f);
            if (silt != null) { var e = silt.emission; e.rateOverTime = 0f; }
            if (grit != null) { var e = grit.emission; e.rateOverTime = 0f; }
            Destroy(root.gameObject, 9f);
        }

        private ParticleSystem Dust(Transform parent, Vector3 at, Vector3 box, Color colour, Vector2 size, Vector2 life, float rate, Vector3 drift, float stretch)
        {
            var go = new GameObject(stretch > 0f ? "Grit" : "Silt");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = colour;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(drift.x - 0.3f, drift.x + 0.3f);
            vel.y = new ParticleSystem.MinMaxCurve(drift.y * 0.6f, drift.y * 1.2f);
            vel.z = new ParticleSystem.MinMaxCurve(drift.z - 0.3f, drift.z + 0.3f);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (_dustMaterial != null) r.sharedMaterial = _dustMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (stretch > 0f) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = stretch; r.lengthScale = 1f; }
            ps.Play();
            return ps;
        }

        /// <summary>Rocks break off the trench walls ahead of the diver and tumble down into the dark.</summary>
        private IEnumerator Rockfall(Transform parent, Vector3 at, Vector3 toward)
        {
            if (_rockModels == null || _rockModels.Length == 0) yield break;
            Vector3 side = Vector3.Cross(Vector3.up, toward);
            var falling = new System.Collections.Generic.List<(Transform t, float floor, float v, Vector3 spin)>();
            for (int i = 0; i < _fallingRocks && _quaking; i++)
            {
                // Off one wall or the other, ahead of the diver (in view), from high up.
                float s = (i % 2 == 0 ? 1f : -1f);
                Vector3 from = at + toward * Random.Range(4f, 22f) + Vector3.up * Random.Range(10f, 22f);
                Vector3 start = from + side * s * 14f;
                if (Physics.Raycast(from, side * s, out RaycastHit wall, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    start = wall.point - side * s * 1.5f;
                GameObject model = _rockModels[Random.Range(0, _rockModels.Length)];
                if (model != null)
                {
                    GameObject rock = Instantiate(model, start, Random.rotation, parent);
                    rock.transform.localScale *= Random.Range(0.15f, 0.35f);
                    foreach (Collider c in rock.GetComponentsInChildren<Collider>()) Destroy(c);
                    falling.Add((rock.transform, Lalim.Mechanics.SwimBody.SeabedY(start, start.y), 0f, Random.insideUnitSphere * 90f));
                    if (_rockClip != null && i % 3 == 2) GameAudio.PlayAt(_rockClip, start, 0.6f, AudioChannel.Sfx, 40f);
                }
                float wait = Random.Range(0.4f, 1.1f);
                for (float t = 0f; t < wait; t += Time.deltaTime) { Tumble(falling); yield return null; }
            }
            for (float t = 0f; t < 10f && falling.Count > 0; t += Time.deltaTime) { Tumble(falling); yield return null; }
        }

        private static void Tumble(System.Collections.Generic.List<(Transform t, float floor, float v, Vector3 spin)> falling)
        {
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                var f = falling[i];
                if (f.t == null) { falling.RemoveAt(i); continue; }
                f.v = Mathf.Min(f.v + 7f * Time.deltaTime, 9f);   // slower than in air: it's water
                f.t.position += Vector3.down * f.v * Time.deltaTime;
                f.t.Rotate(f.spin * Time.deltaTime);
                if (f.t.position.y <= f.floor + 0.4f) { falling.RemoveAt(i); continue; }
                falling[i] = f;
            }
        }

        private void OnTalked()
        {
            _holding = false;
            if (_bag != null) _bag.gameObject.SetActive(true);
            if (_shot != null) Destroy(_shot.gameObject, 3f);   // the conversation's own shot has taken over
            ObjectiveTracker.Instance?.Clear();
            // Once the shot has faded back to the diver, Bakunawa's talk spot goes home to its lair,
            // where the relic gift is given at the end.
            StatueDialogueRouter statue = Statue;
            if (statue != null) statue.StartCoroutine(RestoreLater(statue));
            Complete();
        }

        private static IEnumerator RestoreLater(StatueDialogueRouter statue)
        {
            yield return new WaitForSecondsRealtime(3f);
            while (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) yield return null;
            statue.RestoreFraming();
        }

        public override void RestoreCompleted()
        {
            Statue?.MarkQuestDone(allowIdleTalk: Zone != null && Zone.IsCleansed);
        }

        private void DebugForceComplete()
        {
            Statue?.MarkQuestDone(false);
            _holding = false;
            Complete();
        }

#if UNITY_EDITOR
        public void EditorSetQuake(Material dust, GameObject[] rocks, AudioClip rockClip)
        {
            _dustMaterial = dust;
            _rockModels = rocks;
            _rockClip = rockClip;
        }

        public void EditorSetup(Bakunawa bakunawa, StatueDialogueRouter statue)
        {
            _bakunawa = bakunawa;
            _statue = statue;
        }
#endif
    }
}
