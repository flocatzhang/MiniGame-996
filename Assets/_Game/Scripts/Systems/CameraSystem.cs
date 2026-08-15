using OfficeHell.Config;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// Follows the player and stays clamped inside the arena. Runs on unscaled delta so the camera
    /// keeps easing while the logic clock is frozen for a hitstop.
    ///
    /// The clamp is a gameplay rule, not a polish detail: the bounded field is what stops "run left
    /// forever" from being the answer to pressure on a widescreen layout, and the player has to be
    /// able to see the wall for that to read.
    /// </summary>
    public sealed class CameraSystem
    {
        readonly GameContext _ctx;
        Camera _camera;
        Transform _rig;

        public CameraSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>
        /// The rig carries the follow position and the camera carries the shake offset as a local
        /// position, so shake can never feed back into the follow lerp.
        /// </summary>
        public void Bind(Camera camera, Transform rig)
        {
            _camera = camera;
            _rig = rig;

            if (_camera != null)
            {
                _camera.orthographic = true;
                _camera.orthographicSize = _ctx.Cfg.Camera.OrthographicSize;
            }
        }

        public void Tick(float unscaledDt)
        {
            if (_camera == null || _rig == null)
            {
                return;
            }

            CameraDef def = _ctx.Cfg.Camera;
            PlayerModel p = _ctx.Run.Player;

            _camera.orthographicSize = def.OrthographicSize;

            Vector3 current = _rig.position;
            Vector2 want = Clamp(p.Pos);
            Vector3 target = new Vector3(want.x, want.y, current.z);
            _rig.position = Vector3.Lerp(current, target, Mathf.Clamp01(def.FollowLerp * unscaledDt));
        }

        public void SnapToPlayer()
        {
            if (_camera == null || _rig == null)
            {
                return;
            }

            Vector2 want = Clamp(_ctx.Run.Player.Pos);
            Vector3 c = _rig.position;
            _rig.position = new Vector3(want.x, want.y, c.z);
            _camera.orthographicSize = _ctx.Cfg.Camera.OrthographicSize;
        }

        Vector2 Clamp(Vector2 focus)
        {
            CameraDef def = _ctx.Cfg.Camera;
            ArenaDef arena = _ctx.Cfg.Arena;

            float halfH = def.OrthographicSize;
            float halfW = halfH * def.Aspect;

            float limitX = Mathf.Max(0f, arena.HalfWidth - halfW);
            float limitY = Mathf.Max(0f, arena.HalfHeight - halfH);

            return new Vector2(
                Mathf.Clamp(focus.x, -limitX, limitX),
                Mathf.Clamp(focus.y, -limitY, limitY));
        }
    }
}
