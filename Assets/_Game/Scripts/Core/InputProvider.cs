using OfficeHell.Systems;
using UnityEngine;

namespace OfficeHell.Core
{
    /// <summary>
    /// The only place engine input is read. Writes a snapshot into InputSystem so the systems layer
    /// stays free of UnityEngine.Input and can later be fed by a touch drag or a replay instead.
    /// </summary>
    public sealed class InputProvider : MonoBehaviour
    {
        /// <summary>Screen pixels. Loose enough to ignore sensor jitter on a resting mouse.</summary>
        const float PointerMoveEpsilon = 2f;

        InputSystem _target;
        Camera _camera;

        Vector3 _lastPointerScreen;
        bool _hasPointerScreen;

        public void Bind(InputSystem target, Camera camera)
        {
            _target = target;
            _camera = camera;
        }

        void Update()
        {
            if (_target == null || _camera == null)
            {
                return;
            }

            InputSnapshot snapshot = new InputSnapshot();

            Vector3 mouse = Input.mousePosition;
            bool inside = mouse.x >= 0f && mouse.y >= 0f && mouse.x <= Screen.width && mouse.y <= Screen.height;
            bool moved = false;
            if (inside)
            {
                Vector3 world = _camera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 0f));
                snapshot.PointerWorld = new Vector2(world.x, world.y);
                snapshot.PointerValid = true;

                // Measured in screen space on purpose: the world point under a resting cursor drifts
                // every frame while the camera follows the player, which would look like mouse input.
                moved = _hasPointerScreen
                    && (mouse - _lastPointerScreen).sqrMagnitude > PointerMoveEpsilon * PointerMoveEpsilon;
                _lastPointerScreen = mouse;
                _hasPointerScreen = true;
            }

            snapshot.PointerMoved = moved || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);

            float x = 0f;
            float y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                x -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                x += 1f;
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                y -= 1f;
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                y += 1f;
            }

            snapshot.Axis = new Vector2(x, y);
            _target.Snapshot = snapshot;
        }
    }
}
