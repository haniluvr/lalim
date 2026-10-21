using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// The relic handover, in the world: the relic fades into view between the statue and the diver
    /// — white-hot at first, settling into a soft glow with a halo, a light and drifting sparkles —
    /// hovers and turns while the deity speaks, then sweeps into the diver and is gone. Spawned by
    /// RelicRewardStep. Uses scaled time, so pausing the dialogue freezes it too.
    /// </summary>
    public class RelicPresentation : MonoBehaviour
    {
        /// <summary>Material assets for the glow (assigned in the scene so their shader variants ship in builds).</summary>
        [Serializable]
        public struct Look
        {
            [Tooltip("Soft additive disc behind the relic.")]
            public Material halo;
            [Tooltip("Additive particle material for the sparkles.")]
            public Material sparkle;
            [Tooltip("Transparent Lit material the relic fades in with (its textures are copied over).")]
            public Material fade;
        }

        [SerializeField] private float _displaySize = 1.2f;          // metres across the relic's longest side
        [SerializeField] private float _appearSeconds = 1.8f;
        [SerializeField] private float _receiveSeconds = 0.7f;
        [SerializeField] private float _spinDegreesPerSecond = 40f;
        [SerializeField] private float _bobHeight = 0.08f;
        [SerializeField] private Color _glowColor = new Color(1f, 0.86f, 0.55f);
        [SerializeField] private float _lightIntensity = 3f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private Transform _diver;
        private Transform _model;
        private Light _light;
        private Renderer _halo;
        private Material _haloMat;
        private ParticleSystem _sparkles;
        private Vector3 _hover;
        private bool _receiving;
        private float _age;

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Material[]> _solid = new List<Material[]>();
        private readonly List<Material[]> _fading = new List<Material[]>();
        private readonly List<Material> _owned = new List<Material>();

        /// <summary>Fades <paramref name="relicPrefab"/> into view between the statue and the diver.</summary>
        public static RelicPresentation Spawn(GameObject relicPrefab, Transform statue, Transform diver, Look look = default)
        {
            var root = new GameObject("RelicPresentation");
            var p = root.AddComponent<RelicPresentation>();
            p.Setup(relicPrefab, statue, diver, look);
            return p;
        }

        private void Setup(GameObject relicPrefab, Transform statue, Transform diver, Look look)
        {
            _diver = diver;
            _hover = HoverPoint(StatueChest(statue, out float height), height);
            if (height > 0f && height < 3f) _displaySize = Mathf.Min(_displaySize, 0.5f);   // sized for people, not giant statues
            transform.position = _hover;

            if (relicPrefab != null)
            {
                GameObject model = Instantiate(relicPrefab, transform);
                model.name = relicPrefab.name;
                _model = model.transform;
                FitSize(model);
                PrepareMaterials(model, look.fade);
            }

            BuildHalo(look.halo);
            BuildSparkles(look.sparkle);

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(transform, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = _glowColor;
            _light.range = 4f;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;

            StartCoroutine(Appear());
        }

        /// <summary>The relic sweeps into the diver; <paramref name="onReceived"/> runs as it vanishes.</summary>
        public void Receive(Action onReceived)
        {
            if (_receiving) return;
            _receiving = true;
            StopAllCoroutines();
            ShowSolid();
            if (_sparkles != null) _sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            StartCoroutine(ReceiveRoutine(onReceived));
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_model != null) _model.Rotate(Vector3.up, _spinDegreesPerSecond * Time.deltaTime, Space.World);
        }

        private IEnumerator Appear()
        {
            if (_sparkles != null) _sparkles.Emit(28);   // a shimmer as it materialises
            for (float t = 0f; t < 1f; t += Time.deltaTime / _appearSeconds)
            {
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.85f, t));
                // Starts as pure light, then cools to its own colours with a soft glow.
                float heat = Mathf.Lerp(3f, 0.55f, Mathf.SmoothStep(0f, 1f, t));
                SetFade(alpha, heat);
                float bloom = t < 0.35f ? Mathf.SmoothStep(0f, 1.5f, t / 0.35f) : Mathf.Lerp(1.5f, 1f, (t - 0.35f) / 0.65f);
                SetHalo(bloom);
                _light.intensity = _lightIntensity * bloom * 1.4f;
                transform.position = _hover + Vector3.down * (0.15f * (1f - Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            ShowSolid();

            // Hover, gently bobbing and breathing, until the conversation ends.
            float phase = _age;
            while (true)
            {
                float b = Mathf.Sin((_age - phase) * 1.6f) * _bobHeight;
                float breathe = 0.85f + 0.15f * Mathf.Sin(_age * 2.2f);
                transform.position = _hover + Vector3.up * b;
                SetHalo(breathe);
                _light.intensity = _lightIntensity * breathe;
                yield return null;
            }
        }

        private IEnumerator ReceiveRoutine(Action onReceived)
        {
            Vector3 from = transform.position;
            Vector3 fromScale = transform.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / _receiveSeconds)
            {
                float e = t * t;
                transform.position = Vector3.Lerp(from, DiverChest(), e);
                transform.localScale = Vector3.Lerp(fromScale, Vector3.one * 0.05f, e);
                _light.intensity = _lightIntensity * (1f + 2f * t);   // bright flash as it reaches the diver
                SetHalo(1f + t);
                yield return null;
            }
            onReceived?.Invoke();
            Destroy(gameObject);
        }

        // ───────────────────────── Placement ─────────────────────────

        // Roughly the giver's chest: two thirds up its visible height. A Kataw's own body (its
        // "Body" model) counts, not the stand-in shapes beside it.
        private Vector3 StatueChest(Transform statue, out float height)
        {
            height = 0f;
            if (statue == null) return _diver != null ? _diver.position + Vector3.up : Vector3.zero;
            Transform body = statue.Find("Body");
            if (body == null && statue.parent != null) body = statue.parent.Find("Body");
            if (!TryBounds(body != null ? body : statue, out Bounds b)) return statue.position + Vector3.up * 2f;
            height = b.size.y;
            Vector3 p = b.center;
            p.y = b.min.y + b.size.y * 0.66f;
            return p;
        }

        private Vector3 DiverChest()
        {
            if (_diver == null) return transform.position;
            return TryBounds(_diver, out Bounds b) ? b.center : _diver.position + Vector3.up * 0.5f;
        }

        // In front of the diver, toward the statue and a little above eye line. Between two people
        // of a size (a Kataw and the diver), it floats halfway, just above them and a little toward
        // the camera, so the talk shot shows it in the middle.
        private Vector3 HoverPoint(Vector3 statueChest, float giverHeight)
        {
            if (_diver == null) return statueChest + Vector3.up;
            Vector3 chest = DiverChest();
            if (giverHeight > 0f && giverHeight < 3f)
            {
                Vector3 p = Vector3.Lerp(chest, statueChest, 0.5f) + Vector3.up * 0.4f;
                Camera cam = Camera.main;
                if (cam != null) p += (cam.transform.position - p).normalized * 0.6f;
                return p;
            }
            Vector3 toStatue = statueChest - chest;
            toStatue.y = 0f;
            float dist = toStatue.magnitude;
            Vector3 dir = dist > 0.01f ? toStatue / dist : _diver.forward;
            return chest + dir * Mathf.Min(2.2f, dist * 0.5f) + Vector3.up * 0.7f;
        }

        private static bool TryBounds(Transform t, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        // ───────────────────────── Look ─────────────────────────

        private void FitSize(GameObject model)
        {
            if (!TryBounds(model.transform, out Bounds b)) return;
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (longest > 0.0001f) model.transform.localScale *= _displaySize / longest;   // relic meshes are pivoted at their centre
        }

        // Two sets per renderer: see-through copies to fade in with, and the solid glowing ones it settles on.
        private void PrepareMaterials(GameObject model, Material fadeTemplate)
        {
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                Material[] source = r.sharedMaterials;
                var solid = new Material[source.Length];
                var fading = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] == null) continue;
                    solid[i] = new Material(source[i]);
                    Glow(solid[i], 0.55f);
                    fading[i] = fadeTemplate != null ? new Material(fadeTemplate) : MakeTransparent(new Material(source[i]));
                    if (source[i].HasProperty(BaseMapId) && fading[i].HasProperty(BaseMapId)) fading[i].SetTexture(BaseMapId, source[i].GetTexture(BaseMapId));
                    if (source[i].HasProperty(BaseColorId) && fading[i].HasProperty(BaseColorId)) fading[i].SetColor(BaseColorId, source[i].GetColor(BaseColorId));
                    Glow(fading[i], 3f);
                    _owned.Add(solid[i]);
                    _owned.Add(fading[i]);
                }
                _renderers.Add(r);
                _solid.Add(solid);
                _fading.Add(fading);
                r.sharedMaterials = fading;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            SetFade(0f, 3f);
        }

        private void Glow(Material m, float strength)
        {
            if (!m.HasProperty(EmissionId)) return;
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionMap") && m.HasProperty(BaseMapId)) m.SetTexture("_EmissionMap", m.GetTexture(BaseMapId));
            m.SetColor(EmissionId, _glowColor * strength);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        private static Material MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        private void SetFade(float alpha, float heat)
        {
            foreach (Material[] set in _fading)
            {
                foreach (Material m in set)
                {
                    if (m == null) continue;
                    if (m.HasProperty(BaseColorId)) { Color c = m.GetColor(BaseColorId); c.a = alpha; m.SetColor(BaseColorId, c); }
                    if (m.HasProperty(EmissionId)) m.SetColor(EmissionId, _glowColor * (heat * alpha));
                }
            }
        }

        private void ShowSolid()
        {
            for (int i = 0; i < _renderers.Count; i++) if (_renderers[i] != null) _renderers[i].sharedMaterials = _solid[i];
        }

        private void BuildHalo(Material template)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Halo";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (_displaySize * 2.6f);
            go.AddComponent<FaceCamera>();
            _halo = go.GetComponent<Renderer>();
            _halo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (template != null) _haloMat = new Material(template);
            else
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                _haloMat = new Material(s != null ? s : Shader.Find("Sprites/Default"));
            }
            _owned.Add(_haloMat);
            _halo.sharedMaterial = _haloMat;
            SetHalo(0f);
        }

        private void SetHalo(float strength)
        {
            if (_haloMat == null) return;
            Color c = _glowColor;
            c.a = Mathf.Clamp01(0.55f * strength);
            if (_haloMat.HasProperty(BaseColorId)) _haloMat.SetColor(BaseColorId, c);
            _halo.transform.localScale = Vector3.one * (_displaySize * (2.2f + 0.6f * strength));
        }

        private void BuildSparkles(Material material)
        {
            var go = new GameObject("Sparkles");
            go.transform.SetParent(transform, false);
            _sparkles = go.AddComponent<ParticleSystem>();
            _sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _sparkles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = _glowColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            var emission = _sparkles.emission;
            emission.rateOverTime = 16f;
            var shape = _sparkles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = _displaySize * 0.45f;
            var fade = _sparkles.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(_glowColor, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            else
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                var m = new Material(s != null ? s : Shader.Find("Sprites/Default"));
                _owned.Add(m);
                renderer.sharedMaterial = m;
            }
            _sparkles.Play();
        }

        private void OnDestroy()
        {
            foreach (Material m in _owned) if (m != null) Destroy(m);
        }
    }
}
