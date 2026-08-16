using OfficeHell.Config;
using UnityEngine;

namespace OfficeHell.View
{
    /// <summary>
    /// One view component for every world entity. Shape, colour and scale all come from ViewDef,
    /// which is how six visually distinct enemies exist before any art is produced.
    /// </summary>
    public sealed class EntityView : MonoBehaviour
    {
        public SpriteRenderer Body;

        LootBeamFx _lootBeam;
        SpriteRenderer _barBack;
        SpriteRenderer _barFill;
        SpriteRenderer _qualityLight;
        SpriteRenderer _ring;
        TextMesh _label;

        Color _baseColor = Color.white;
        Color _hitColor = Color.white;
        Color _tintColor = Color.white;
        float _tintAmount;
        float _baseScale = 1f;
        float _baseBodyY;
        Vector2 _bodyOffset;
        float _scaleMultiplier = 1f;
        float _squashX = 1f;
        float _squashY = 1f;
        float _bodyRoll;
        float _flashAmount;
        float _alpha = 1f;
        float _visualTop = 0.5f;
        float _decorationGap = 0.28f;

        Sprite[] _animationFrames;
        int _animationFrame;
        float _animationElapsed;
        float _animationFrameSeconds = 0.125f;

        Vector2 _lastTrackedPosition;
        bool _hasTrackedPosition;

        public static EntityView Create(string name, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            EntityView view = go.AddComponent<EntityView>();

            GameObject body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            view.Body = body.AddComponent<SpriteRenderer>();
            view.Body.sortingOrder = sortingOrder;
            return view;
        }

        public void Bind(ViewDef def, ViewShape shapeOverride, bool useOverride)
        {
            ViewShape shape = useOverride ? shapeOverride : def.Shape;
            Sprite[] frames = !useOverride ? ArtCatalog.Frames(def.SpriteSet) : null;

            _animationFrames = frames != null && frames.Length > 0 ? frames : null;
            _animationFrame = 0;
            _animationElapsed = 0f;
            _animationFrameSeconds = 1f / Mathf.Max(0.1f, def.AnimationFps);
            _bodyOffset = Vector2.zero;
            _scaleMultiplier = 1f;
            _squashX = 1f;
            _squashY = 1f;
            _bodyRoll = 0f;
            _flashAmount = 0f;
            _tintAmount = 0f;
            _alpha = 1f;
            _hasTrackedPosition = false;

            if (_animationFrames != null)
            {
                Body.sprite = _animationFrames[0];
                _baseColor = Color.white;

                float desiredHeight = SpriteHeightOf(def);
                float sourceHeight = Mathf.Max(0.01f, Body.sprite.bounds.size.y);
                _baseScale = desiredHeight / sourceHeight;
                _baseBodyY = desiredHeight * 0.5f;
                _visualTop = desiredHeight;
                _decorationGap = 0.18f;
            }
            else
            {
                Body.sprite = PrimitiveFactory.Get(shape);
                _baseColor = def.Color;
                _baseScale = def.Scale;
                _baseBodyY = 0f;
                _visualTop = def.Scale * 0.5f;
                _decorationGap = 0.28f;
            }

            _hitColor = HitColorFor(_baseColor);
            Body.flipX = false;
            ApplyBodyColor();
            ApplyBodyTransform();
        }

        static float SpriteHeightOf(ViewDef def)
        {
            return def.SpriteHeight > 0f ? def.SpriteHeight : def.Scale;
        }

        /// <summary>
        /// Item art is centred instead of foot anchored. Sizing by its longest edge preserves the
        /// authored aspect ratio while keeping the old ViewDef scale as the gameplay silhouette.
        /// </summary>
        public void SetStaticSprite(Sprite sprite, float worldSize)
        {
            if (sprite == null)
            {
                return;
            }

            _animationFrames = null;
            Body.sprite = sprite;
            _baseColor = Color.white;
            _hitColor = HitColorFor(_baseColor);

            Vector2 source = sprite.bounds.size;
            float longestEdge = Mathf.Max(0.01f, Mathf.Max(source.x, source.y));
            float size = Mathf.Max(0.01f, worldSize);
            _baseScale = size / longestEdge;
            _baseBodyY = 0f;
            _visualTop = size * 0.5f;
            _decorationGap = 0.18f;
            Body.flipX = false;
            ApplyBodyColor();
            ApplyBodyTransform();
        }

        /// <summary>
        /// Height of the drawn body above the entity origin, for anything that wants to sit over an
        /// entity's head. Character art is anchored at the feet while a primitive is centred on the
        /// origin, so no caller can assume one or the other. Mirrors what Bind does for _visualTop
        /// and is only valid for the callers that bind without a shape override, which is every
        /// character in the game.
        /// </summary>
        public static float VisualTopOf(ViewDef def)
        {
            if (def == null)
            {
                return 0.5f;
            }

            Sprite[] frames = ArtCatalog.Frames(def.SpriteSet);
            bool animated = frames != null && frames.Length > 0;
            return animated ? SpriteHeightOf(def) : def.Scale * 0.5f;
        }

        /// <summary>
        /// A SpriteRenderer tint multiplies, so tinting white character art towards white is a no-op
        /// and the hit would read as nothing at all. Bright bodies shift hue instead. Deliberately a
        /// washed out red rather than a saturated one: at full strength this covers the whole body,
        /// and a pure red body reads as an alarm state rather than as "that landed".
        /// </summary>
        static Color HitColorFor(Color baseColor)
        {
            float luminance = baseColor.r * 0.299f + baseColor.g * 0.587f + baseColor.b * 0.114f;
            return luminance > 0.72f ? new Color(1f, 0.52f, 0.47f, baseColor.a) : Color.white;
        }

        public void SetWorldPosition(Vector2 pos)
        {
            transform.position = new Vector3(pos.x, pos.y, 0f);
        }

        public Vector2 SetTrackedWorldPosition(Vector2 pos)
        {
            Vector2 delta = _hasTrackedPosition ? pos - _lastTrackedPosition : Vector2.zero;
            _lastTrackedPosition = pos;
            _hasTrackedPosition = true;
            SetWorldPosition(pos);
            return delta;
        }

        public void TickAnimation(float logicalDt, float facingX, bool moving)
        {
            if (_animationFrames == null || _animationFrames.Length == 0)
            {
                return;
            }

            if (Mathf.Abs(facingX) > 0.001f)
            {
                // Character frame sets are authored facing left.
                Body.flipX = facingX > 0f;
            }

            if (logicalDt <= 0f)
            {
                return;
            }

            if (!moving)
            {
                _animationFrame = 0;
                _animationElapsed = 0f;
                Body.sprite = _animationFrames[0];
                return;
            }

            if (_animationFrames.Length == 1)
            {
                return;
            }

            _animationElapsed += logicalDt;
            while (_animationElapsed >= _animationFrameSeconds)
            {
                _animationElapsed -= _animationFrameSeconds;
                _animationFrame = (_animationFrame + 1) % _animationFrames.Length;
            }

            Body.sprite = _animationFrames[_animationFrame];
        }

        public void SetScaleMultiplier(float mul)
        {
            _scaleMultiplier = mul;
            ApplyBodyTransform();
        }

        public void SetBodyOffset(float y)
        {
            _bodyOffset = new Vector2(0f, y);
            ApplyBodyTransform();
        }

        /// <summary>
        /// Every part of a hit or death pose in one call. Setting them one at a time would rebuild the
        /// body transform four times per corpse per frame, and late game there are dozens of corpses.
        /// </summary>
        public void SetBodyPose(Vector2 offset, float scaleMultiplier, float squashX, float squashY, float roll)
        {
            _bodyOffset = offset;
            _scaleMultiplier = scaleMultiplier;
            _squashX = squashX;
            _squashY = squashY;
            _bodyRoll = roll;
            ApplyBodyTransform();
        }

        void ApplyBodyTransform()
        {
            float scale = _baseScale * _scaleMultiplier;
            Body.transform.localScale = new Vector3(scale * _squashX, scale * _squashY, 1f);

            // The pivot sits at the feet for character art, so squashing has to pull the body down with
            // it or a flattened enemy would appear to hover.
            Body.transform.localPosition = new Vector3(
                _bodyOffset.x,
                _baseBodyY * _scaleMultiplier * _squashY + _bodyOffset.y,
                0f);

            Body.transform.localRotation = Quaternion.Euler(0f, 0f, _bodyRoll);
        }

        public void SetFlashAmount(float amount)
        {
            _flashAmount = Mathf.Clamp01(amount);
            ApplyBodyColor();
        }

        public void SetAlpha(float a)
        {
            _alpha = a;
            ApplyBodyColor();
        }

        /// <summary>
        /// A steady colour for a state that lasts, held apart from the hit flash so that a buff cannot
        /// be mistaken for damage. The flash is layered over the top, so getting hit while buffed still
        /// reads as getting hit.
        /// </summary>
        public void SetTint(Color color, float amount)
        {
            _tintColor = color;
            _tintAmount = Mathf.Clamp01(amount);
            ApplyBodyColor();
        }

        /// <summary>Loot takes its colour from the quality tier, which has to become the new base.</summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            _hitColor = HitColorFor(color);
            ApplyBodyColor();
        }

        void ApplyBodyColor()
        {
            Color c = _tintAmount > 0f ? Color.Lerp(_baseColor, _tintColor, _tintAmount) : _baseColor;
            if (_flashAmount > 0f)
            {
                c = Color.Lerp(c, _hitColor, _flashAmount);
            }

            c.a = _baseColor.a * _alpha;
            Body.color = c;
        }

        // ---------- optional decorations, created lazily ----------

        public void ShowLootBeam(Quality quality, Color color, float time, int seed)
        {
            if (_lootBeam == null)
            {
                _lootBeam = LootBeamFx.Create(transform, Body.sortingOrder);
            }

            _lootBeam.Show(quality, color, time, seed);
        }

        public void HideBeam()
        {
            if (_lootBeam != null)
            {
                _lootBeam.Hide();
            }
        }

        /// <summary>
        /// Draws the authored tier light above the beam geometry but below the item. Keeping this
        /// separate from Ring means pooled loot cannot borrow an enemy aura, while the stable sorting
        /// gap prevents the stronger purple and orange beam pulses from intermittently hiding it.
        /// </summary>
        public void ShowQualityLight(Sprite sprite, float radius)
        {
            if (sprite == null)
            {
                HideQualityLight();
                return;
            }

            if (_qualityLight == null)
            {
                GameObject go = new GameObject("QualityLight");
                go.transform.SetParent(transform, false);
                _qualityLight = go.AddComponent<SpriteRenderer>();
                _qualityLight.sortingOrder = Body.sortingOrder - 1;
            }

            _qualityLight.sprite = sprite;
            _qualityLight.enabled = true;
            _qualityLight.color = Color.white;
            float sourceWidth = Mathf.Max(0.01f, sprite.bounds.size.x);
            float scale = Mathf.Max(0f, radius) * 2f / sourceWidth;
            _qualityLight.transform.localScale = Vector3.one * scale;
        }

        public void HideQualityLight()
        {
            if (_qualityLight != null)
            {
                _qualityLight.enabled = false;
            }
        }

        public void ShowRing(Color color, float radius)
        {
            ShowRing(null, color, radius);
        }

        /// <summary>
        /// Imported ground art is authored at UI pixel density rather than as a one-unit primitive.
        /// Normalising by its width keeps the requested gameplay radius while retaining its authored
        /// top-down ellipse instead of stretching it back into a perfect circle.
        /// </summary>
        public void ShowRing(Sprite sprite, Color color, float radius)
        {
            if (_ring == null)
            {
                GameObject go = new GameObject("Ring");
                go.transform.SetParent(transform, false);
                _ring = go.AddComponent<SpriteRenderer>();
                _ring.sortingOrder = Body.sortingOrder - 2;
            }

            _ring.sprite = sprite != null ? sprite : PrimitiveFactory.Ring;
            _ring.enabled = true;
            _ring.color = color;
            float sourceWidth = Mathf.Max(0.01f, _ring.sprite.bounds.size.x);
            float scale = Mathf.Max(0f, radius) * 2f / sourceWidth;
            _ring.transform.localScale = Vector3.one * scale;
        }

        public void HideRing()
        {
            if (_ring != null)
            {
                _ring.enabled = false;
            }
        }

        public void ShowBar(float fill01, float width)
        {
            if (_barBack == null)
            {
                GameObject back = new GameObject("BarBack");
                back.transform.SetParent(transform, false);
                _barBack = back.AddComponent<SpriteRenderer>();
                _barBack.sprite = PrimitiveFactory.Pixel;
                _barBack.color = new Color(0f, 0f, 0f, 0.6f);
                _barBack.sortingOrder = Body.sortingOrder + 1;

                GameObject fill = new GameObject("BarFill");
                fill.transform.SetParent(transform, false);
                _barFill = fill.AddComponent<SpriteRenderer>();
                _barFill.sprite = PrimitiveFactory.Pixel;
                _barFill.color = new Color(0.9f, 0.25f, 0.25f, 1f);
                _barFill.sortingOrder = Body.sortingOrder + 2;
            }

            float y = _visualTop * _scaleMultiplier + _decorationGap;
            _barBack.enabled = true;
            _barFill.enabled = true;
            _barBack.transform.localScale = new Vector3(width, 0.12f, 1f);
            _barBack.transform.localPosition = new Vector3(0f, y, 0f);

            float w = Mathf.Max(0f, width * Mathf.Clamp01(fill01));
            _barFill.transform.localScale = new Vector3(w, 0.09f, 1f);
            _barFill.transform.localPosition = new Vector3(-(width - w) * 0.5f, y, 0f);
        }

        public void HideBar()
        {
            if (_barBack != null)
            {
                _barBack.enabled = false;
                _barFill.enabled = false;
            }
        }

        public void ShowLabel(string text, Color color)
        {
            if (_label == null)
            {
                GameObject go = new GameObject("Label");
                go.transform.SetParent(transform, false);
                _label = go.AddComponent<TextMesh>();
                _label.font = FontProvider.Font;
                _label.fontSize = 48;
                _label.characterSize = 0.035f;
                _label.anchor = TextAnchor.LowerCenter;
                _label.alignment = TextAlignment.Center;

                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = FontProvider.FontMaterial;
                mr.sortingOrder = Body.sortingOrder + 3;
            }

            _label.gameObject.SetActive(true);
            _label.text = text;
            _label.color = color;
            _label.transform.localPosition = new Vector3(
                0f,
                _visualTop * _scaleMultiplier + _decorationGap + 0.14f,
                0f);
        }

        public void HideLabel()
        {
            if (_label != null)
            {
                _label.gameObject.SetActive(false);
            }
        }

        /// <summary>Called before the instance goes back in the pool so nothing leaks to the next user.</summary>
        public void ResetDecorations()
        {
            HideBeam();
            HideQualityLight();
            HideBar();
            HideLabel();
            HideRing();
            _animationFrames = null;
            _animationFrame = 0;
            _animationElapsed = 0f;
            _hasTrackedPosition = false;
            _scaleMultiplier = 1f;
            _bodyOffset = Vector2.zero;
            _squashX = 1f;
            _squashY = 1f;
            _bodyRoll = 0f;
            _flashAmount = 0f;
            _tintAmount = 0f;
            _alpha = 1f;
            ApplyBodyTransform();
            ApplyBodyColor();
            Body.enabled = true;
            Body.flipX = false;
            transform.localRotation = Quaternion.identity;
        }
    }
}
