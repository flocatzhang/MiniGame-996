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

        SpriteRenderer _beam;
        SpriteRenderer _barBack;
        SpriteRenderer _barFill;
        SpriteRenderer _ring;
        TextMesh _label;

        Color _baseColor = Color.white;
        float _baseScale = 1f;
        float _baseBodyY;
        float _bodyOffsetY;
        float _scaleMultiplier = 1f;
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
            _bodyOffsetY = 0f;
            _scaleMultiplier = 1f;
            _hasTrackedPosition = false;

            if (_animationFrames != null)
            {
                Body.sprite = _animationFrames[0];
                _baseColor = Color.white;

                float desiredHeight = def.SpriteHeight > 0f ? def.SpriteHeight : def.Scale;
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

            Body.color = _baseColor;
            Body.flipX = false;
            ApplyBodyTransform();
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
            _bodyOffsetY = y;
            ApplyBodyTransform();
        }

        void ApplyBodyTransform()
        {
            Body.transform.localScale = Vector3.one * (_baseScale * _scaleMultiplier);
            Body.transform.localPosition = new Vector3(
                0f,
                _baseBodyY * _scaleMultiplier + _bodyOffsetY,
                0f);
        }

        public void SetFlash(bool on)
        {
            Body.color = on ? Color.white : _baseColor;
        }

        public void SetAlpha(float a)
        {
            Color c = Body.color;
            c.a = a;
            Body.color = c;
        }

        // ---------- optional decorations, created lazily ----------

        public void ShowBeam(Color color, float height, float width)
        {
            if (_beam == null)
            {
                GameObject go = new GameObject("Beam");
                go.transform.SetParent(transform, false);
                _beam = go.AddComponent<SpriteRenderer>();
                _beam.sprite = PrimitiveFactory.Pixel;
                _beam.sortingOrder = Body.sortingOrder - 1;
            }

            _beam.enabled = true;
            _beam.color = new Color(color.r, color.g, color.b, 0.35f);
            _beam.transform.localScale = new Vector3(width, height, 1f);
            _beam.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        }

        public void HideBeam()
        {
            if (_beam != null)
            {
                _beam.enabled = false;
            }
        }

        public void RotateBeam(float degrees)
        {
            if (_beam != null && _beam.enabled)
            {
                _beam.transform.localRotation = Quaternion.Euler(0f, degrees, 0f);
            }
        }

        public void ShowRing(Color color, float radius)
        {
            if (_ring == null)
            {
                GameObject go = new GameObject("Ring");
                go.transform.SetParent(transform, false);
                _ring = go.AddComponent<SpriteRenderer>();
                _ring.sprite = PrimitiveFactory.Ring;
                _ring.sortingOrder = Body.sortingOrder - 2;
            }

            _ring.enabled = true;
            _ring.color = color;
            _ring.transform.localScale = Vector3.one * radius * 2f;
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
            HideBar();
            HideLabel();
            HideRing();
            _animationFrames = null;
            _animationFrame = 0;
            _animationElapsed = 0f;
            _hasTrackedPosition = false;
            _scaleMultiplier = 1f;
            _bodyOffsetY = 0f;
            ApplyBodyTransform();
            Body.enabled = true;
            Body.color = _baseColor;
            Body.flipX = false;
            transform.localRotation = Quaternion.identity;
        }
    }
}
