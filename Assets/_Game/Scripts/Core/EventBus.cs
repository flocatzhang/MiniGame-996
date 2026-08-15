using System;
using System.Collections.Generic;
using UnityEngine;

namespace OfficeHell.Core
{
    /// <summary>
    /// Payload passed to every listener. A struct so dispatching never allocates.
    /// Meaning of each field is documented at the dispatch site.
    /// </summary>
    public struct EvtArg
    {
        public int I0;
        public int I1;
        public float F0;
        public float F1;
        public Vector2 P0;
        public Vector2 P1;
        public object O0;

        public static EvtArg Empty;
    }

    /// <summary>
    /// Model and system layers publish, view and UI layers subscribe.
    /// Mirrors the RegisterEvent / DispatchEvent convention used in the main client project.
    /// </summary>
    public sealed class EventBus
    {
        readonly Dictionary<EventID, Action<EvtArg>> _map = new Dictionary<EventID, Action<EvtArg>>();

        public void Register(EventID id, Action<EvtArg> cb)
        {
            if (cb == null)
            {
                return;
            }

            Action<EvtArg> existing;
            if (_map.TryGetValue(id, out existing))
            {
                _map[id] = existing + cb;
            }
            else
            {
                _map[id] = cb;
            }
        }

        public void Unregister(EventID id, Action<EvtArg> cb)
        {
            if (cb == null)
            {
                return;
            }

            Action<EvtArg> existing;
            if (_map.TryGetValue(id, out existing))
            {
                _map[id] = existing - cb;
            }
        }

        public void Dispatch(EventID id)
        {
            Dispatch(id, EvtArg.Empty);
        }

        public void Dispatch(EventID id, EvtArg arg)
        {
            Action<EvtArg> cb;
            if (!_map.TryGetValue(id, out cb) || cb == null)
            {
                return;
            }

            try
            {
                cb(arg);
            }
            catch (Exception e)
            {
                // A broken listener must never break the frame during a jam build.
                Debug.LogError("[EventBus] listener threw on " + id + ": " + e);
            }
        }

        public void Clear()
        {
            _map.Clear();
        }
    }
}
