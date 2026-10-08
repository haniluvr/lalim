using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lalim.Core;
using Lalim.DDA;
using Lalim.Mechanics;
using Lalim.Mechanics.Quest;
using Lalim.UI;

namespace Lalim.Challenges.GhostNet
{
    /// <summary>
    /// Zone 3 — ghost net untangling and removal at one area (first person, looking down).
    ///
    /// The scene's ghost net lies over the seabed with sea animals trapped under it. It is one
    /// square-mesh net: corded ropes between knots, finer cord across each square. Each animal lies
    /// under a torn hole sized to it (small, long or large), but the net's inner knots have been
    /// dragged out of place so ropes cross over the holes. DRAG the glowing knots; once nothing
    /// crosses the room an animal needs, it fits through and swims free into the "Freed Ocean
    /// Friends" tray. When every animal is free, SWIPE UP to haul the net away.
    ///
    /// DDA knobs: animals (3–7), scramble (how many knots start out of place), hints.
    /// DDA reporting: a knot move that removes crossings or frees an animal = correct action.
    /// Tasks: one per freed animal + one for hauling the net.
    /// </summary>
    public class GhostNetMinigame : MonoBehaviour, IAreaMinigame
    {
        [Serializable]
        public class Species
        {
            public string id;
            public string displayName;
            public GameObject model;
            [Tooltip("How big a hole the animal needs to slip through.")]
            public NetPuzzle.Fit fit;
        }

        [SerializeField] private MinigameDifficultyProfile _profile;
        [SerializeField] private List<Species> _species = new List<Species>();
        [Tooltip("The scene's ghost net and trapped-creature props at this area: hidden while the puzzle net is out, gone once it's hauled away.")]
        [SerializeField] private List<GameObject> _sceneProps = new List<GameObject>();

        [Header("Look")]
        [Tooltip("The net's cord (the scene net's own material).")]
        [SerializeField] private Material _ropeMaterial;
        [Tooltip("Ropes that cross another rope.")]
        [SerializeField] private Material _tangledMaterial;
        [Tooltip("The glowing knots the player drags.")]
        [SerializeField] private Material _knotMaterial;
        [SerializeField] private TMP_FontAsset _titleFont;
        [SerializeField] private TMP_FontAsset _bodyFont;

        [Header("Sound")]
        [Tooltip("Grabbing a knot to drag the rope.")]
        [SerializeField] private AudioClip _ropeDragClip;
        [Tooltip("The net hauled up and away.")]
        [SerializeField] private AudioClip _netLiftClip;
        [Tooltip("An animal slipping free (once, even if several get out together).")]
        [SerializeField] private AudioClip _freedClip;

        [Header("Layout (metres)")]
        [SerializeField, Min(0.4f)] private float _cellSize = 0.9f;
        [SerializeField, Range(45f, 90f)] private float _viewPitch = 72f;
        [SerializeField, Min(0.05f)] private float _knotSize = 0.17f;
        [SerializeField, Min(0.1f)] private float _grabRadius = 0.45f;
        [SerializeField, Min(0.005f)] private float _ropeRadius = 0.03f;
        [SerializeField, Min(0.002f)] private float _cordRadius = 0.012f;

        private static readonly Color KnotColor = new Color(0.45f, 1f, 0.93f);
        private static readonly Color CordColor = new Color(0.33f, 0.4f, 0.38f);
        private static readonly Color TangledColor = new Color(0.85f, 0.22f, 0.14f);

        private int _animals = -1;

        public int TaskCount => AnimalCount + 1;
        public float ExpectedSeconds => _profile != null ? _profile.expectedSeconds : 90f;
        public float ExpectedFirstActionSeconds => _profile != null ? _profile.expectedFirstActionSeconds : 8f;
        public int HintBudget => _profile != null && _profile.Has(Level1DifficultyKnobs.Hints) ? Mathf.Max(0, _profile.GetInt(Level1DifficultyKnobs.Hints)) : 2;

        private int AnimalCount
        {
            get
            {
                if (_animals < 0)
                {
                    _animals = _profile != null && _profile.Has(Level1DifficultyKnobs.Animals) ? _profile.GetInt(Level1DifficultyKnobs.Animals) : 5;
                    _animals = Mathf.Clamp(_animals, 3, 7);
                }
                return _animals;
            }
        }

        private MinigameStage _stage;
        private Transform _root, _topView;
        private NetPuzzle _puzzle;
        private Mesh _ropeMesh;
        private readonly List<Transform> _knots = new List<Transform>();
        private readonly List<GameObject> _animalObjects = new List<GameObject>();
        private readonly List<Species> _dealt = new List<Species>();
        private readonly List<RenderTexture> _portraits = new List<RenderTexture>();
        private float[] _wobble;
        private bool[] _freed;
        private int _grabbed = -1;
        private int _crossingsAtGrab, _initialCrossings;
        private bool _ready, _hauling, _hauled;
        private float _ground;
        private Vector3 _heading, _swipeStart;
        private Material _knotRuntime, _tangledRuntime, _ropeRuntime;
        private NetUI _ui;

        // ───────────────────────── IAreaMinigame ─────────────────────────

        public void Play(AreaStation station)
        {
            _stage ??= new MinigameStage(this);
            Build(station);
            _stage.Enter(_topView);
            _ui = NetUI.Create(transform, _dealt.Count, UseHint, _titleFont, _bodyFont);
            _initialCrossings = Mathf.Max(1, _puzzle.TotalCrossings());
            RefreshHints();
            UpdateStatus();
            StartCoroutine(EnableAfter(0.6f));
        }

        public void Stop()
        {
            StopAllCoroutines();
            _ready = false;
            _grabbed = -1;
            // Left before the net was hauled: the scene's net (and its animals) come back.
            if (!_hauled) SetSceneProps(true);
            if (_root != null) Destroy(_root.gameObject);
            if (_topView != null) Destroy(_topView.gameObject);
            if (_ui != null) Destroy(_ui.gameObject);
            if (_ropeMesh != null) Destroy(_ropeMesh);
            foreach (RenderTexture rt in _portraits) if (rt != null) { rt.Release(); Destroy(rt); }
            _portraits.Clear();
            _knots.Clear(); _animalObjects.Clear(); _dealt.Clear();
            _animals = -1;
            _hauling = _hauled = false;
            _stage?.Exit();
        }

        public void ShowCompletedState() => SetSceneProps(false);

        private IEnumerator EnableAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _ready = true;
        }

        // ───────────────────────── Build ─────────────────────────

        private void Build(AreaStation station)
        {
            DealSpecies(AnimalCount);
            var fits = new List<NetPuzzle.Fit>();
            foreach (Species s in _dealt) fits.Add(s.fit);
            float scramble = _profile != null && _profile.Has(Level1DifficultyKnobs.Scramble) ? _profile.Get(Level1DifficultyKnobs.Scramble) : 0.8f;
            _puzzle = NetPuzzle.Generate(fits, _cellSize, scramble, UnityEngine.Random.Range(0, int.MaxValue));
            _freed = new bool[_dealt.Count];
            _wobble = new float[_puzzle.Ropes.Count];
            for (int r = 0; r < _wobble.Length; r++) _wobble[r] = UnityEngine.Random.Range(-1f, 1f);

            // The puzzle net takes the scene net's place on the seabed, turned the way the diver faces.
            _heading = Heading(station);
            _ground = GroundUnder(transform.position, _puzzle.Bounds);
            float netHeight = 0.12f + TallestAnimal();
            _root = new GameObject("[GhostNet]").transform;
            _root.SetPositionAndRotation(new Vector3(transform.position.x, _ground + netHeight, transform.position.z),
                                         Quaternion.LookRotation(Vector3.down, _heading));
            SetSceneProps(false);

            _ropeRuntime = _ropeMaterial != null ? _ropeMaterial : LitMaterial(CordColor, 0f);
            _tangledRuntime = _tangledMaterial != null ? _tangledMaterial : LitMaterial(TangledColor, 0f);
            _knotRuntime = _knotMaterial != null ? _knotMaterial : new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = KnotColor };

            var ropes = new GameObject("Ropes", typeof(MeshFilter), typeof(MeshRenderer));
            ropes.transform.SetParent(_root, false);
            _ropeMesh = new Mesh { name = "GhostNetRopes" };
            _ropeMesh.MarkDynamic();
            ropes.GetComponent<MeshFilter>().sharedMesh = _ropeMesh;
            var mr = ropes.GetComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { _ropeRuntime, _tangledRuntime, _ropeRuntime };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            for (int k = 0; k < _puzzle.Positions.Count; k++)
            {
                var knot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                knot.name = $"Knot{k}";
                Destroy(knot.GetComponent<Collider>());
                knot.transform.SetParent(_root, false);
                bool movable = _puzzle.Movable[k];
                // Glowing knots can be dragged; the dark ones along the edge are weighed down.
                knot.transform.localScale = Vector3.one * (movable ? _knotSize : _ropeRadius * 3.2f);
                var rend = knot.GetComponent<Renderer>();
                rend.sharedMaterial = movable ? _knotRuntime : _ropeRuntime;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                knot.SetActive(_puzzle.Exists[k]);
                _knots.Add(knot.transform);
            }

            // A soft light over the work, so the net reads even in a rock's shadow.
            var lamp = new GameObject("WorkLight").AddComponent<Light>();
            lamp.transform.SetParent(_root, false);
            lamp.transform.localPosition = new Vector3(0f, 0f, -3f);
            lamp.type = LightType.Point;
            lamp.range = Mathf.Max(_puzzle.Bounds.width, _puzzle.Bounds.height) * 1.1f;
            lamp.intensity = 1.6f;
            lamp.color = new Color(0.85f, 1f, 0.95f);
            lamp.shadows = LightShadows.None;

            TopView();
            for (int a = 0; a < _dealt.Count; a++) SpawnAnimal(a);
            Redraw();
        }

        private Vector3 Heading(AreaStation station)
        {
            Vector3 heading = Vector3.zero;
            PlayerController player = PlayerController.Instance;
            if (player != null) heading = transform.position - player.transform.position;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.25f && station.ViewPoint != null) heading = station.ViewPoint.forward;
            heading.y = 0f;
            return heading.sqrMagnitude < 0.01f ? Vector3.forward : heading.normalized;
        }

        // The highest seabed under the net's footprint (so no sand pokes through).
        private float GroundUnder(Vector3 centre, Rect bounds)
        {
            float best = float.MinValue;
            float reach = Mathf.Max(bounds.width, bounds.height) * 0.5f;
            for (int i = 0; i < 9; i++)
            {
                Vector3 o = centre + (i == 0 ? Vector3.zero : Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * reach * (i % 2 == 0 ? 0.5f : 0.35f));
                o.y = centre.y + 2.5f;
                foreach (RaycastHit hit in Physics.RaycastAll(o, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (PlayerController.Instance != null && hit.collider.transform.IsChildOf(PlayerController.Instance.transform)) continue;
                    if (hit.collider.GetComponentInParent<Lalim.Abilities.DugongCompanion>() != null) continue;
                    if (hit.point.y < centre.y + 2.4f) best = Mathf.Max(best, hit.point.y);
                }
            }
            return best > float.MinValue ? best : centre.y - 1.5f;
        }

        private float TallestAnimal()
        {
            float tallest = 0.3f;
            foreach (Species s in _dealt)
            {
                if (s.model == null) continue;
                Vector3 size = ModelSize(s.model);
                float length = Mathf.Max(size.x, size.z);
                if (length < 0.001f) continue;
                tallest = Mathf.Max(tallest, size.y / length * AnimalLength(s.fit) * (Rolled(s, size) ? 0.8f : 1f));
            }
            return Mathf.Min(tallest, 0.9f);
        }

        /// <summary>A camera above the net looking (nearly) straight down, high enough to frame all of it between the card and the tray.</summary>
        private void TopView()
        {
            if (_topView == null) _topView = new GameObject("[GhostNetTopView]").transform;
            Camera cam = Camera.main;
            float fov = cam != null ? cam.fieldOfView : 60f;
            float aspect = cam != null ? cam.aspect : 16f / 9f;
            float lo = 1.5f, hi = 25f;
            for (int i = 0; i < 24; i++)
            {
                float mid = (lo + hi) * 0.5f;
                PlaceView(mid);
                if (NetFits(fov, aspect)) hi = mid; else lo = mid;
            }
            PlaceView(hi);
        }

        private void PlaceView(float height)
        {
            Vector3 centre = _root.position;
            float back = height / Mathf.Tan(_viewPitch * Mathf.Deg2Rad);
            _topView.position = centre + Vector3.up * height - _heading * back;
            _topView.rotation = Quaternion.LookRotation(centre - _topView.position, _heading);
        }

        private bool NetFits(float fov, float aspect)
        {
            Rect b = _puzzle.Bounds;
            float ty = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), tx = ty * aspect;
            foreach (Vector2 c in new[] { b.min, b.max, new Vector2(b.xMin, b.yMax), new Vector2(b.xMax, b.yMin) })
            {
                Vector3 v = Quaternion.Inverse(_topView.rotation) * (_root.TransformPoint(new Vector3(c.x, c.y, 0f)) - _topView.position);
                if (v.z <= 0.1f) return false;
                float x = 0.5f + 0.5f * v.x / (v.z * tx), y = 0.5f + 0.5f * v.y / (v.z * ty);
                // Clear of the glass card (top) and the friends tray (bottom).
                if (x < 0.04f || x > 0.96f || y < 0.21f || y > 0.83f) return false;
            }
            return true;
        }

        private void DealSpecies(int count)
        {
            var ids = new List<string>();
            foreach (Species s in _species) if (s != null && !string.IsNullOrEmpty(s.id)) ids.Add(s.id);
            SaveData save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
            List<string> picked = SpeciesBag.Deal(count, ids, save?.ghostNetBagState, new System.Random(), out string state);
            if (save != null) save.ghostNetBagState = state;

            _dealt.Clear();
            foreach (string id in picked) _dealt.Add(_species.Find(s => s.id == id));
            while (_dealt.Count < count) _dealt.Add(new Species { id = "fish", displayName = "Reef Fish" });
        }

        private float AnimalLength(NetPuzzle.Fit fit) =>
            _cellSize * (fit == NetPuzzle.Fit.Large ? 1.55f : fit == NetPuzzle.Fit.Long ? 1.75f : 0.9f);

        // Tall, flat-sided animals (most fish) lie on their side so they read from above.
        private static bool Rolled(Species s, Vector3 size) => s.fit != NetPuzzle.Fit.Large && size.y > Mathf.Min(size.x, size.z) * 1.15f && size.y > size.x;

        private void SpawnAnimal(int index)
        {
            Species s = _dealt[index];
            NetPuzzle.Hole hole = _puzzle.Holes[index];
            GameObject go = s.model != null ? Instantiate(s.model) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Trapped_{s.id}";
            if (s.model != null) CreatureRig.Play(go, s.model.name, true);   // struggling in the net: its idle
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            Vector3 size = ModelSize(go);
            float scale = AnimalLength(s.fit) / Mathf.Max(0.001f, Mathf.Max(size.x, size.z));
            go.transform.localScale *= scale;

            // Facing: along a long hole; sideways on screen for the rest (heads alternate).
            Vector3 along = _root.TransformDirection(hole.Fit == NetPuzzle.Fit.Long ? new Vector3(hole.Axis.x, hole.Axis.y, 0f) : Vector3.right);
            if (index % 2 == 1) along = -along;
            if (hole.Fit == NetPuzzle.Fit.Large) along = Quaternion.Euler(0f, UnityEngine.Random.Range(-35f, 35f), 0f) * along;
            Quaternion rot = Quaternion.LookRotation(along, Vector3.up);
            if (Rolled(s, size)) rot = Quaternion.AngleAxis(index % 2 == 1 ? -70f : 70f, along) * rot;
            go.transform.rotation = rot;
            go.transform.SetParent(_root, true);

            // Sit it on the sand, right where the camera sees the hole through the net above it.
            Vector3 holeWorld = _root.TransformPoint(new Vector3(hole.Center.x, hole.Center.y, 0f));
            Bounds b = RendererBounds(go);
            float rise = _ground + 0.02f - b.min.y;
            float centreY = b.center.y + rise;
            Vector3 eye = _topView.position;
            Vector3 onRay = eye + (holeWorld - eye) * ((eye.y - centreY) / Mathf.Max(0.01f, eye.y - holeWorld.y));
            go.transform.position += new Vector3(onRay.x - b.center.x, rise, onRay.z - b.center.z);

            go.AddComponent<Struggle>();
            _animalObjects.Add(go);
        }

        // ───────────────────────── Input ─────────────────────────

        private void Update()
        {
            if (!_ready || _puzzle == null) return;

            if (_hauling)
            {
                HandleHaulSwipe();
                return;
            }

            if (Input.GetMouseButtonDown(0) && !MinigameStage.PointerOverUI() && PointerOnNet(out Vector2 p))
            {
                _grabbed = NearestKnot(p);
                if (_grabbed >= 0)
                {
                    GameAudio.PlayFlat(_ropeDragClip);
                    _crossingsAtGrab = _puzzle.TotalCrossings();
                    ChallengeManager.Instance?.ReportPlayerAction();
                    Redraw();
                }
            }

            if (_grabbed >= 0 && Input.GetMouseButton(0) && PointerOnNet(out Vector2 drag))
            {
                _puzzle.MoveKnot(_grabbed, drag);
                Redraw();
            }

            if (_grabbed >= 0 && Input.GetMouseButtonUp(0))
            {
                _grabbed = -1;
                bool untangled = _puzzle.TotalCrossings() < _crossingsAtGrab;
                bool freed = CheckFreed();
                if (untangled || freed) ChallengeManager.Instance?.ReportCorrectAction();
                ChallengeManager.Instance?.ReportPlayerAction();
                Redraw();
                UpdateStatus();
            }
        }

        private bool PointerOnNet(out Vector2 local)
        {
            local = Vector2.zero;
            if (_stage.Camera == null) return false;
            Ray ray = _stage.PointerRay();
            var plane = new Plane(-_root.forward, _root.position);
            if (!plane.Raycast(ray, out float d)) return false;
            Vector3 l = _root.InverseTransformPoint(ray.GetPoint(d));
            local = new Vector2(l.x, l.y);
            return true;
        }

        private int NearestKnot(Vector2 p)
        {
            int best = -1;
            float bestSqr = _grabRadius * _grabRadius;
            for (int k = 0; k < _puzzle.Positions.Count; k++)
            {
                if (!_puzzle.Movable[k]) continue;
                float d = (_puzzle.Positions[k] - p).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = k; }
            }
            return best;
        }

        private void UseHint()
        {
            if (!_ready || _hauling || _grabbed >= 0 || ChallengeManager.Instance == null) return;
            int knot = _puzzle.HintKnot(_freed);
            if (knot < 0 || !ChallengeManager.Instance.UseHint()) { RefreshHints(); return; }
            StartCoroutine(GlideKnot(knot, _puzzle.Solved[knot]));
            RefreshHints();
        }

        private IEnumerator GlideKnot(int knot, Vector2 to)
        {
            Vector2 from = _puzzle.Positions[knot];
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                _puzzle.MoveKnot(knot, Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.6f)));
                Redraw();
                yield return null;
            }
            _puzzle.MoveKnot(knot, to);
            CheckFreed();
            Redraw();
            UpdateStatus();
        }

        private void RefreshHints()
        {
            _ui?.SetHints(HintBulb.Remaining(HintBudget));
        }

        private void UpdateStatus()
        {
            if (_ui == null) return;
            int freed = 0;
            foreach (bool f in _freed) if (f) freed++;
            float untangled = 1f - Mathf.Clamp01(_puzzle.TotalCrossings() / (float)_initialCrossings);
            float progress = _hauled ? 1f : 0.7f * freed / Mathf.Max(1, _freed.Length) + 0.3f * untangled;
            _ui.SetProgress(progress);
            _ui.SetStatus(_hauled ? "The net is gone. The sea is free." : _hauling ? "Swipe up to haul the net away" : "Drag the glowing knots so no ropes cross");
        }

        // ───────────────────────── Freeing & hauling ─────────────────────────

        private bool CheckFreed()
        {
            bool any = false;
            for (int a = 0; a < _freed.Length; a++)
            {
                if (_freed[a] || !_puzzle.IsFree(a)) continue;
                _freed[a] = true;
                any = true;
                StartCoroutine(FreeAnimal(a));
            }
            if (any) GameAudio.PlayFlat(_freedClip);
            if (any && Array.TrueForAll(_freed, f => f))
            {
                _hauling = true;
                _ui.ShowSwipeHint(true);
            }
            return any;
        }

        private IEnumerator FreeAnimal(int a)
        {
            Species s = _dealt[a];
            RenderTexture portrait = Portrait(s.model);
            if (portrait != null) _portraits.Add(portrait);
            _ui.Reveal(a, portrait, s.displayName);
            EventBus.Publish(new CodexEntryUnlockedEvent { SpeciesId = s.id, RegionDiscovered = "Luzon" });
            ChallengeManager.Instance?.ReportTaskCompleted();

            // Up through the hole, then away over the net's edge.
            GameObject animal = _animalObjects[a];
            if (animal == null) yield break;
            Destroy(animal.GetComponent<Struggle>());
            if (s.model != null) CreatureRig.Play(animal, s.model.name, false);   // and it swims off
            Transform tr = animal.transform;
            Vector3 start = tr.position;
            Vector3 away = start - _root.position;
            away.y = 0f;
            away = away.sqrMagnitude < 0.01f ? _heading : away.normalized;
            Vector3 up = start + Vector3.up * (_root.position.y - start.y + 0.5f);
            Vector3 end = up + away * 4f + Vector3.up * 0.8f;
            Quaternion r0 = tr.rotation, r1 = Quaternion.LookRotation(away, Vector3.up);
            Vector3 s0 = tr.localScale;
            for (float e = 0f; e < 2.4f; e += Time.deltaTime)
            {
                float k = e / 2.4f;
                tr.position = k < 0.35f ? Vector3.Lerp(start, up, Mathf.SmoothStep(0f, 1f, k / 0.35f))
                                        : Vector3.Lerp(up, end, Mathf.SmoothStep(0f, 1f, (k - 0.35f) / 0.65f));
                tr.rotation = Quaternion.Slerp(r0, r1, Mathf.Clamp01(k * 2.5f)) * Quaternion.Euler(0f, Mathf.Sin(e * 14f) * 12f * (1f - k), 0f);
                tr.localScale = s0 * Mathf.Lerp(1f, 0.001f, Mathf.Clamp01((k - 0.75f) / 0.25f));
                yield return null;
            }
            animal.SetActive(false);
        }

        private void HandleHaulSwipe()
        {
            if (_hauled) return;
            if (Input.GetMouseButtonDown(0)) _swipeStart = Input.mousePosition;
            if (Input.GetMouseButtonUp(0))
            {
                Vector3 delta = Input.mousePosition - _swipeStart;
                if (delta.y > Screen.height * 0.12f && delta.y > Mathf.Abs(delta.x))
                {
                    _hauled = true;
                    StartCoroutine(Haul());
                }
            }
        }

        private IEnumerator Haul()
        {
            GameAudio.PlayFlat(_netLiftClip);
            _ui.ShowSwipeHint(false);
            UpdateStatus();
            Vector3 start = _root.position;
            for (float e = 0f; e < 1.4f; e += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / 1.4f);
                _root.position = start + Vector3.up * (k * 2.5f) + _heading * (k * 7f);
                yield return null;
            }
            SetSceneProps(false);
            ChallengeManager.Instance?.ReportTaskCompleted(); // last task → challenge completes → Stop()
        }

        private void SetSceneProps(bool visible)
        {
            foreach (GameObject go in _sceneProps) if (go != null) go.SetActive(visible);
        }

        // ───────────────────────── Drawing ─────────────────────────

        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<int>[] _tris = { new List<int>(), new List<int>(), new List<int>() };

        private void Redraw()
        {
            for (int k = 0; k < _knots.Count; k++)
            {
                if (!_puzzle.Exists[k]) continue;
                Vector2 p = _puzzle.Positions[k];
                _knots[k].localPosition = new Vector3(p.x, p.y, -0.03f);
                if (_puzzle.Movable[k]) _knots[k].localScale = Vector3.one * _knotSize * (k == _grabbed ? 1.45f : 1f);
            }

            _verts.Clear();
            _normals.Clear();
            foreach (List<int> t in _tris) t.Clear();

            for (int r = 0; r < _puzzle.Ropes.Count; r++)
            {
                NetPuzzle.Rope rope = _puzzle.Ropes[r];
                bool crossed = _puzzle.RopeIsCrossed(r);
                // Crossing ropes ride over each other instead of cutting through.
                float lift = -0.015f * (r % 3);
                Tube(_puzzle.Positions[rope.A], _puzzle.Positions[rope.B], _ropeRadius, lift, 0.05f, _wobble[r] * 0.05f, 6, 6, crossed ? 1 : 0);
            }
            for (int m = 0; m < _puzzle.Meshes.Count; m++)
            for (int l = 0; l < NetPuzzle.LinesPerMesh; l++)
            {
                _puzzle.MeshLine(m, l, out Vector2 a, out Vector2 b);
                Tube(a, b, _cordRadius, 0.005f, 0.03f, 0f, 3, 4, 2);
            }

            _ropeMesh.Clear();
            _ropeMesh.indexFormat = _verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            _ropeMesh.SetVertices(_verts);
            _ropeMesh.SetNormals(_normals);
            _ropeMesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) _ropeMesh.SetTriangles(_tris[i], i, false);
            _ropeMesh.RecalculateBounds();
        }

        // A round cord between two knots, sagging down (local +z is toward the sand) and drifting a little sideways.
        private void Tube(Vector2 a, Vector2 b, float radius, float lift, float sag, float drift, int segments, int sides, int submesh)
        {
            Vector2 along2 = b - a;
            if (along2.sqrMagnitude < 1e-6f) return;
            Vector2 side2 = new Vector2(-along2.y, along2.x).normalized;
            int first = _verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float bulge = Mathf.Sin(t * Mathf.PI);
                Vector2 p2 = Vector2.Lerp(a, b, t) + side2 * (drift * bulge);
                Vector3 p = new Vector3(p2.x, p2.y, lift + sag * bulge);
                Vector3 tangent = new Vector3(along2.x, along2.y, sag * Mathf.PI * Mathf.Cos(t * Mathf.PI)).normalized;
                Vector3 n1 = Vector3.ProjectOnPlane(Vector3.forward, tangent).normalized;
                Vector3 n2 = Vector3.Cross(tangent, n1);
                for (int s = 0; s < sides; s++)
                {
                    float ang = s / (float)sides * Mathf.PI * 2f;
                    Vector3 n = n1 * Mathf.Cos(ang) + n2 * Mathf.Sin(ang);
                    _verts.Add(p + n * radius);
                    _normals.Add(n);
                }
            }
            List<int> tris = _tris[submesh];
            for (int i = 0; i < segments; i++)
            for (int s = 0; s < sides; s++)
            {
                int a0 = first + i * sides + s, a1 = first + i * sides + (s + 1) % sides;
                int b0 = a0 + sides, b1 = a1 + sides;
                tris.Add(a0); tris.Add(b0); tris.Add(a1);
                tris.Add(a1); tris.Add(b0); tris.Add(b1);
            }
        }

        private static Material LitMaterial(Color color, float emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (emission > 0f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
            }
            return mat;
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // Size of a model (or instance) as authored: unrotated, unscaled by its placement.
        private static Vector3 ModelSize(GameObject model)
        {
            bool asset = !model.scene.IsValid();
            GameObject probe = asset ? Instantiate(model) : model;
            Quaternion keep = probe.transform.rotation;
            probe.transform.rotation = Quaternion.identity;
            Vector3 size = RendererBounds(probe).size;
            probe.transform.rotation = keep;
            if (asset) DestroyImmediate(probe);
            return size;
        }

        // ───────────────────────── Tray portraits ─────────────────────────

        /// <summary>Renders the animal once, three-quarter view on a clear background, for its tray slot.</summary>
        private static RenderTexture Portrait(GameObject model)
        {
            if (model == null) return null;
            var stage = new GameObject("[NetPortrait]");
            stage.transform.position = new Vector3(0f, -5000f, 0f);
            GameObject m = Instantiate(model, stage.transform);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.Euler(0f, 125f, 0f);
            Bounds b = RendererBounds(m);
            // Fill the circle: frame the animal's width (and its height seen from a little above).
            float half = Mathf.Max(0.01f, Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z * 0.5f)) * 1.12f);
            float radius = b.extents.magnitude;

            var rt = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32) { name = "NetPortrait" };
            var cam = new GameObject("PortraitCamera").AddComponent<Camera>();
            cam.transform.SetParent(stage.transform, false);
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 28f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            float distance = half / Mathf.Tan(14f * Mathf.Deg2Rad);
            cam.transform.position = b.center + new Vector3(0f, 0.5f, -1f).normalized * distance;
            cam.transform.LookAt(b.center);
            cam.nearClipPlane = Mathf.Max(0.01f, distance - radius * 1.2f);
            cam.farClipPlane = distance + radius * 1.5f;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            DestroyImmediate(stage);
            return rt;
        }

#if UNITY_EDITOR
        public void EditorSetup(MinigameDifficultyProfile profile, List<Species> species, List<GameObject> sceneProps)
        {
            _profile = profile;
            _species = species;
            _sceneProps = sceneProps;
        }

        public void EditorSetupSounds(AudioClip ropeDrag, AudioClip netLift, AudioClip freed)
        {
            _ropeDragClip = ropeDrag;
            _netLiftClip = netLift;
            _freedClip = freed;
        }

        public void EditorSetupLook(Material rope, Material tangled, Material knot, TMP_FontAsset titleFont, TMP_FontAsset bodyFont, List<Species> species)
        {
            _ropeMaterial = rope;
            _tangledMaterial = tangled;
            _knotMaterial = knot;
            _titleFont = titleFont;
            _bodyFont = bodyFont;
            if (species != null) _species = species;
        }

        /// <summary>Test hook: the live puzzle (null when not playing).</summary>
        public NetPuzzle EditorPuzzle => _puzzle;
#endif

        /// <summary>The trapped animal pulls against the net until it's freed.</summary>
        private class Struggle : MonoBehaviour
        {
            private Quaternion _home;
            private float _phase;
            private void Start() { _home = transform.rotation; _phase = UnityEngine.Random.value * 10f; }
            private void Update() => transform.rotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 5f + _phase) * 9f, 0f) * _home * Quaternion.Euler(Mathf.Sin(Time.time * 3.1f + _phase) * 5f, 0f, 0f);
        }

        // ───────────────────────── UI (built at runtime) ─────────────────────────

        /// <summary>
        /// Glass card on top (hint bulb flush left, progress bar, one-line hint), the Freed Ocean
        /// Friends tray at the bottom, and an animated swipe-up arrow for hauling the net.
        /// </summary>
        private class NetUI : MonoBehaviour
        {
            private const float CardWidth = 860f, CardHeight = 118f, BulbZone = HintBulb.CardZone;

            private TMP_Text _status;
            private RectTransform _barFill, _swipe;
            private HintBulb _hints;
            private readonly List<RectTransform> _slots = new List<RectTransform>();
            private readonly List<Image> _slotFill = new List<Image>();
            private readonly List<TMP_Text> _slotMark = new List<TMP_Text>();
            private readonly List<RawImage> _slotPortrait = new List<RawImage>();
            private readonly List<float> _revealedAt = new List<float>();
            private readonly List<Image> _chevrons = new List<Image>();
            private TMP_FontAsset _title, _body;

            public static NetUI Create(Transform parent, int animals, Action onHint, TMP_FontAsset title, TMP_FontAsset body)
            {
                var go = new GameObject("[GhostNetUI]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(parent, false);
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 55;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;
                var ui = go.AddComponent<NetUI>();
                ui._title = title;
                ui._body = body;
                ui.Build(animals, onHint);
                return ui;
            }

            private void Build(int animals, Action onHint)
            {
                // ── Glass card ──
                RectTransform card = Rect("Card", transform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(CardWidth, CardHeight));
                card.pivot = new Vector2(0.5f, 1f);
                Lalim.UI.ZoneBannerUI.Avoid(card);   // warnings drop below the card
                Sliced(card, "Fill", GlassSprites.Rounded, new Color(0.06f, 0.22f, 0.32f, 0.62f), 0.75f);
                Sliced(card, "Sheen", GlassSprites.RoundedSheen, new Color(1f, 1f, 1f, 0.9f), 0.75f);
                Sliced(card, "Rim", GlassSprites.RoundedOutline, new Color(0.78f, 0.96f, 1f, 0.55f), 0.75f);

                // Hint bulb, flush with the card's left edge, split off by a hairline.
                _hints = HintBulb.AttachToCard(card, CardHeight, onHint, _title);

                // Progress bar + one hint line, centred in the rest of the card.
                float content = (CardWidth - BulbZone) * 0.5f + BulbZone - CardWidth * 0.5f;
                RectTransform track = Rect("Track", card, new Vector2(0.5f, 0.5f), new Vector2(content, 18f), new Vector2(620f, 22f));
                Sliced(track, "Back", GlassSprites.Rounded, new Color(1f, 1f, 1f, 0.16f), 0.22f);
                _barFill = Rect("Fill", track, new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
                _barFill.anchorMin = Vector2.zero;
                _barFill.anchorMax = new Vector2(0f, 1f);
                _barFill.offsetMin = _barFill.offsetMax = Vector2.zero;
                var fill = _barFill.gameObject.AddComponent<Image>();
                fill.sprite = GlassSprites.Rounded;
                fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = 1f / 0.22f;
                fill.color = new Color(0.55f, 1f, 0.75f, 0.95f);
                fill.raycastTarget = false;
                _status = Text(Rect("Hint", card, new Vector2(0.5f, 0.5f), new Vector2(content, -26f), new Vector2(700f, 40f)),
                               "", 28f, _body, new Color(1f, 1f, 1f, 0.88f), new Vector2(700f, 40f));

                // ── Freed animals tray (no label: the portraits speak for themselves) ──
                float spacing = 140f;
                for (int i = 0; i < animals; i++)
                {
                    float x = (i - (animals - 1) * 0.5f) * spacing;
                    RectTransform slot = Rect($"Slot{i}", transform, new Vector2(0.5f, 0f), new Vector2(x, 196f), new Vector2(116f, 116f));   // clear of the HP / swim bar / shells
                    var back = slot.gameObject.AddComponent<Image>();
                    back.sprite = GlassSprites.Circle;
                    back.color = new Color(0.06f, 0.22f, 0.32f, 0.55f);
                    back.raycastTarget = false;
                    var rim = Rect("Rim", slot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(116f, 116f)).gameObject.AddComponent<Image>();
                    rim.sprite = GlassSprites.CircleOutline;
                    rim.color = new Color(0.78f, 0.96f, 1f, 0.55f);
                    rim.raycastTarget = false;
                    var portrait = Rect("Portrait", slot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118f, 118f)).gameObject.AddComponent<RawImage>();
                    portrait.raycastTarget = false;
                    portrait.enabled = false;
                    _slots.Add(slot);
                    _slotFill.Add(back);
                    _slotPortrait.Add(portrait);
                    _slotMark.Add(Text(slot, "?", 44f, _title, new Color(1f, 1f, 1f, 0.6f), new Vector2(116f, 116f)));
                    _revealedAt.Add(-1f);
                }

                // ── Swipe-up arrow: three chevrons lighting up bottom to top ──
                _swipe = Rect("SwipeUp", transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 220f));
                for (int i = 0; i < 3; i++)
                {
                    var chev = Rect($"Chevron{i}", _swipe, new Vector2(0.5f, 0.5f), new Vector2(0f, -56f + i * 56f), new Vector2(150f, 150f)).gameObject.AddComponent<Image>();
                    chev.sprite = GlassSprites.Chevron;
                    chev.raycastTarget = false;
                    _chevrons.Add(chev);
                }
                _swipe.gameObject.SetActive(false);
            }

            public void SetStatus(string text) => _status.text = text;

            public void SetProgress(float value) => _barFill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);

            public void SetHints(int left) => _hints.SetHints(left);

            public void Reveal(int slot, Texture portrait, string name)
            {
                if (slot < 0 || slot >= _slots.Count) return;
                _slotFill[slot].color = new Color(0.35f, 1f, 0.9f, 0.4f);
                if (portrait != null)
                {
                    _slotPortrait[slot].texture = portrait;
                    _slotPortrait[slot].enabled = true;
                    _slotMark[slot].gameObject.SetActive(false);
                }
                else
                {
                    _slotMark[slot].text = name;
                    _slotMark[slot].fontSize = 18f;
                }
                _revealedAt[slot] = Lalim.Core.PauseClock.Time;
            }

            public void ShowSwipeHint(bool on) => _swipe.gameObject.SetActive(on);

            private void Update()
            {
                float now = Lalim.Core.PauseClock.Time;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_revealedAt[i] < 0f) continue;
                    float k = now - _revealedAt[i];
                    float s = k < 0.25f ? Mathf.Lerp(0.6f, 1.18f, k / 0.25f) : k < 0.45f ? Mathf.Lerp(1.18f, 1f, (k - 0.25f) / 0.2f) : 1f;
                    _slots[i].localScale = Vector3.one * s;
                }

                if (_swipe.gameObject.activeSelf)
                {
                    // A light runs up the chevrons while the whole arrow drifts upward and resets.
                    float wave = Mathf.Repeat(now * 1.2f, 1f);
                    for (int i = 0; i < _chevrons.Count; i++)
                    {
                        float d = Mathf.Abs(wave * 1.4f - 0.2f - i / 3f);
                        _chevrons[i].color = new Color(0.85f, 1f, 0.97f, Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(1f - d * 3f)));
                    }
                    _swipe.anchoredPosition = new Vector2(0f, Mathf.Repeat(now * 0.6f, 1f) * 50f - 25f);
                }
            }

            private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = anchor;
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
                return rt;
            }

            private static void Sliced(RectTransform parent, string name, Sprite sprite, Color color, float cornerScale)
            {
                RectTransform rt = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f / cornerScale;
                img.color = color;
                img.raycastTarget = false;
            }

            private static TMP_Text Text(RectTransform parent, string text, float size, TMP_FontAsset font, Color color, Vector2 box)
            {
                var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = box;
                var tmp = go.GetComponent<TextMeshProUGUI>();
                if (font != null) tmp.font = font;
                tmp.text = text;
                tmp.fontSize = size;
                tmp.color = color;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.raycastTarget = false;
                return tmp;
            }
        }
    }
}
