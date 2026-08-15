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
        InputSystem _target;
        Camera _camera;

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
            if (inside)
            {
                Vector3 world = _camera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 0f));
                snapshot.PointerWorld = new Vector2(world.x, world.y);
                snapshot.PointerValid = true;
            }

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
