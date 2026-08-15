using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// Written every frame by the view side InputProvider so this system stays free of
    /// engine input plumbing and can be driven by a replay or a test.
    /// </summary>
    public struct InputSnapshot
    {
        public Vector2 PointerWorld;
        public bool PointerValid;
        public Vector2 Axis;
    }

    /// <summary>
    /// Turns raw input into a bounded move intent. Mouse follow is the primary scheme, the
    /// keyboard axis is only a convenience for playtesting and maps to the same intent vector,
    /// so a touch drag can be dropped in later without touching movement.
    /// </summary>
    public sealed class InputSystem
    {
        const float PointerStopRadius = 0.55f;
        const float PointerResumeRadius = 0.75f;

        readonly GameContext _ctx;
        bool _pointerFollowActive;

        public InputSnapshot Snapshot;

        public InputSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        public void Reset()
        {
            _pointerFollowActive = false;
            Snapshot = new InputSnapshot();
        }

        public void Tick(float dt)
        {
            PlayerModel p = _ctx.Run.Player;

            if (Snapshot.Axis.sqrMagnitude > 0.01f)
            {
                _pointerFollowActive = false;
                p.MoveIntent = Snapshot.Axis.normalized;
            }
            else if (Snapshot.PointerValid)
            {
                Vector2 delta = Snapshot.PointerWorld - p.Pos;
                float distance = delta.magnitude;

                // Separate stop and resume radii keep camera follow and sub-pixel pointer motion
                // from toggling movement on opposite sides of a single threshold every frame.
                if (_pointerFollowActive)
                {
                    if (distance <= PointerStopRadius)
                    {
                        _pointerFollowActive = false;
                    }
                }
                else if (distance >= PointerResumeRadius)
                {
                    _pointerFollowActive = true;
                }

                if (_pointerFollowActive && distance > 0.0001f)
                {
                    // A long frame must not step through the cursor and reverse the facing on the
                    // next frame. Taper only the final step so normal travel remains full speed.
                    float maxStep = p.EffectiveMoveSpeed(GameClock.Now) * Mathf.Max(0f, dt);
                    float remaining = Mathf.Max(0f, distance - PointerStopRadius);
                    float strength = maxStep > 0.0001f ? Mathf.Min(1f, remaining / maxStep) : 0f;
                    p.MoveIntent = delta / distance * strength;
                }
                else
                {
                    p.MoveIntent = Vector2.zero;
                }
            }
            else
            {
                _pointerFollowActive = false;
                p.MoveIntent = Vector2.zero;
            }

            if (p.MoveIntent.sqrMagnitude > 0.01f)
            {
                p.Facing = p.MoveIntent;
            }
        }
    }
}
