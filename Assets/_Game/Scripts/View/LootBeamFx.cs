using OfficeHell.Config;
using UnityEngine;

namespace OfficeHell.View
{
    /// <summary>
    /// One pooled, code-built effect for all four loot tiers. It has no Update; ViewBinder owns the
    /// animation sample while the particle system uses unscaled time so hitstop never freezes sparkle.
    /// </summary>
    public sealed class LootBeamFx : MonoBehaviour
    {
        static Material _particleMaterial;

        SpriteRenderer _wideBeam;
        SpriteRenderer _coreBeam;
        SpriteRenderer _accentBeam;
        SpriteRenderer _groundGlow;
        SpriteRenderer _groundRing;
        ParticleSystem _particles;

        Quality _quality;
        Color _color;
        Color _brightColor;
        bool _configured;

        float _height;
        float _width;
        float _alpha;
        float _pulse;
        float _ringRadius;
        float _ringAlpha;
        float _ringDuration;
        float _startedAt;
        int _initialBurstCount;
        int _seed;

        public static LootBeamFx Create(Transform parent, int sortingOrder)
        {
            GameObject go = new GameObject("LootBeamFx");
            go.transform.SetParent(parent, false);

            LootBeamFx fx = go.AddComponent<LootBeamFx>();
            fx.Build(sortingOrder);
            go.SetActive(false);
            return fx;
        }

        void Build(int sortingOrder)
        {
            _groundGlow = NewRenderer("GroundGlow", PrimitiveFactory.LootGlow, sortingOrder - 4);
            _groundRing = NewRenderer("GroundRing", PrimitiveFactory.Ring, sortingOrder - 3);
            _wideBeam = NewRenderer("WideBeam", PrimitiveFactory.LootBeam, sortingOrder - 3);
            _coreBeam = NewRenderer("CoreBeam", PrimitiveFactory.LootBeam, sortingOrder - 2);
            _accentBeam = NewRenderer("AccentBeam", PrimitiveFactory.LootBeam, sortingOrder - 1);

            GameObject particleGo = new GameObject("Sparks");
            particleGo.transform.SetParent(transform, false);
            _particles = particleGo.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = _particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.useUnscaledTime = true;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = 32;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.4f, 0.06f, 0.02f);

            ParticleSystem.VelocityOverLifetimeModule velocity = _particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;

            ParticleSystem.ColorOverLifetimeModule colorOverLife = _particles.colorOverLifetime;
            colorOverLife.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.72f, 0.68f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLife.color = fade;

            ParticleSystemRenderer renderer = particleGo.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sortingOrder + 1;
            renderer.sharedMaterial = ParticleMaterial;
        }

        SpriteRenderer NewRenderer(string name, Sprite sprite, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        static Material ParticleMaterial
        {
            get
            {
                if (_particleMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    _particleMaterial = new Material(shader);
                    _particleMaterial.mainTexture = PrimitiveFactory.LootSpark.texture;
                    _particleMaterial.hideFlags = HideFlags.DontSave;
                }

                return _particleMaterial;
            }
        }

        public void Show(Quality quality, Color color, float time, int seed)
        {
            bool changed = !_configured || _quality != quality || _color != color;
            if (changed)
            {
                Configure(quality, color);
            }

            _seed = seed;
            bool activated = !gameObject.activeSelf;
            if (activated)
            {
                _startedAt = time;
                gameObject.SetActive(true);
                _particles.Clear(true);
            }

            if (!_particles.isPlaying)
            {
                _particles.Play(true);
            }

            if (activated && _initialBurstCount > 0)
            {
                _particles.Emit(_initialBurstCount);
            }

            Tick(time);
        }

        void Configure(Quality quality, Color color)
        {
            _quality = quality;
            _color = color;
            _configured = true;

            float particleRate;
            int maxParticles;
            float particleSizeMin;
            float particleSizeMax;
            float particleSpeedMin;
            float particleSpeedMax;
            float particleLifetimeMin;
            float particleLifetimeMax;

            switch (quality)
            {
                case Quality.Blue:
                    _height = 1.65f;
                    _width = 0.30f;
                    _alpha = 0.26f;
                    _pulse = 0.045f;
                    _ringRadius = 0.34f;
                    _ringAlpha = 0.18f;
                    _ringDuration = 0.68f;
                    _initialBurstCount = 5;
                    particleRate = 8f;
                    maxParticles = 14;
                    particleSizeMin = 0.025f;
                    particleSizeMax = 0.055f;
                    particleSpeedMin = 0.45f;
                    particleSpeedMax = 1.05f;
                    particleLifetimeMin = 0.9f;
                    particleLifetimeMax = 1.5f;
                    break;

                case Quality.Purple:
                    _height = 2.55f;
                    _width = 0.46f;
                    _alpha = 0.52f;
                    _pulse = 0.075f;
                    _ringRadius = 0.46f;
                    _ringAlpha = 0.36f;
                    _ringDuration = 0.78f;
                    _initialBurstCount = 10;
                    particleRate = 16f;
                    maxParticles = 30;
                    particleSizeMin = 0.035f;
                    particleSizeMax = 0.08f;
                    particleSpeedMin = 0.65f;
                    particleSpeedMax = 1.45f;
                    particleLifetimeMin = 1.15f;
                    particleLifetimeMax = 1.9f;
                    break;

                case Quality.Orange:
                    _height = 4.10f;
                    _width = 0.70f;
                    _alpha = 0.84f;
                    _pulse = 0.12f;
                    _ringRadius = 0.62f;
                    _ringAlpha = 0.62f;
                    _ringDuration = 0.9f;
                    _initialBurstCount = 18;
                    particleRate = 28f;
                    maxParticles = 56;
                    particleSizeMin = 0.045f;
                    particleSizeMax = 0.11f;
                    particleSpeedMin = 0.85f;
                    particleSpeedMax = 2.1f;
                    particleLifetimeMin = 1.35f;
                    particleLifetimeMax = 2.2f;
                    break;

                default:
                    _height = 0.95f;
                    _width = 0.20f;
                    _alpha = 0.12f;
                    _pulse = 0.025f;
                    _ringRadius = 0.24f;
                    _ringAlpha = 0.06f;
                    _ringDuration = 0.58f;
                    _initialBurstCount = 2;
                    particleRate = 2f;
                    maxParticles = 4;
                    particleSizeMin = 0.018f;
                    particleSizeMax = 0.035f;
                    particleSpeedMin = 0.25f;
                    particleSpeedMax = 0.55f;
                    particleLifetimeMin = 0.65f;
                    particleLifetimeMax = 1.05f;
                    break;
            }

            Color bright = Color.Lerp(color, Color.white, quality == Quality.Orange ? 0.42f : 0.22f);
            _brightColor = bright;
            _wideBeam.color = WithAlpha(color, _alpha * 0.22f);
            _coreBeam.color = WithAlpha(bright, _alpha * 0.72f);
            _accentBeam.color = WithAlpha(bright, _alpha * 0.34f);
            _groundGlow.color = WithAlpha(color, _alpha * 0.48f);
            _groundRing.color = WithAlpha(bright, _ringAlpha);

            _accentBeam.enabled = quality >= Quality.Purple;
            _groundRing.enabled = true;

            ParticleSystem.MainModule main = _particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(particleLifetimeMin, particleLifetimeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(particleSizeMin, particleSizeMax);
            main.startColor = WithAlpha(bright, Mathf.Lerp(0.36f, 0.92f, (int)quality / 3f));
            main.maxParticles = maxParticles;

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.rateOverTime = particleRate;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.scale = new Vector3(_width * 1.35f, 0.06f, 0.02f);

            ParticleSystem.VelocityOverLifetimeModule velocity = _particles.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(particleSpeedMin, particleSpeedMax);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        public void Tick(float time)
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            float phase = time * (1.35f + (int)_quality * 0.18f) + _seed * 0.37f;
            float pulse = 1f + Mathf.Sin(phase) * _pulse;
            float counterPulse = 1f + Mathf.Sin(phase * 0.73f + 1.4f) * _pulse * 0.7f;

            _wideBeam.transform.localScale = new Vector3(_width * 2.5f * counterPulse, _height, 1f);
            _coreBeam.transform.localScale = new Vector3(_width * pulse, _height, 1f);
            _accentBeam.transform.localScale = new Vector3(_width * 0.42f * counterPulse, _height * 0.92f, 1f);
            _accentBeam.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.55f) * 5f);

            _groundGlow.transform.localScale = new Vector3(_ringRadius * 3.1f * pulse, _ringRadius * 0.78f * counterPulse, 1f);
            _groundGlow.transform.localPosition = new Vector3(0f, 0.035f, 0f);

            float ringProgress = Mathf.Clamp01((time - _startedAt) / _ringDuration);
            float ringEase = 1f - Mathf.Pow(1f - ringProgress, 3f);
            float ringScale = Mathf.Lerp(1.9f, 0.68f, ringEase);
            float ringFade = Mathf.Sqrt(1f - ringProgress);
            _groundRing.enabled = ringProgress < 1f;
            _groundRing.color = WithAlpha(_brightColor, _ringAlpha * ringFade);
            _groundRing.transform.localScale = new Vector3(_ringRadius * 2f * ringScale, _ringRadius * 0.56f * ringScale, 1f);
            _groundRing.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            _groundRing.transform.localRotation = Quaternion.identity;
        }

        public void Hide()
        {
            if (_particles != null)
            {
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            gameObject.SetActive(false);
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
