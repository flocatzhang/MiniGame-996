using System;
using System.Collections.Generic;
using UnityEngine;

namespace OfficeHell.Core
{
    /// <summary>Marks a pooled instance so Recycle does not need the key passed back in.</summary>
    public sealed class PooledTag : MonoBehaviour
    {
        public string Key;
    }

    /// <summary>
    /// GameObject pool keyed by string. Every spawned view goes through here so that
    /// death-and-restart can recycle the whole world in one call without reloading the scene.
    /// </summary>
    public sealed class PoolService
    {
        readonly Dictionary<string, Stack<GameObject>> _idle = new Dictionary<string, Stack<GameObject>>(32);
        readonly Dictionary<string, List<GameObject>> _active = new Dictionary<string, List<GameObject>>(32);
        readonly Transform _root;

        public PoolService(Transform root)
        {
            _root = root;
        }

        public GameObject Spawn(string key, Func<GameObject> factory)
        {
            Stack<GameObject> idle;
            if (!_idle.TryGetValue(key, out idle))
            {
                idle = new Stack<GameObject>(16);
                _idle[key] = idle;
            }

            GameObject go = null;
            while (idle.Count > 0 && go == null)
            {
                go = idle.Pop();
            }

            if (go == null)
            {
                go = factory();
                PooledTag tag = go.GetComponent<PooledTag>();
                if (tag == null)
                {
                    tag = go.AddComponent<PooledTag>();
                }

                tag.Key = key;

                // Factories usually parent the instance to their own layer root. Only adopt
                // orphans, otherwise pooling would silently flatten the scene hierarchy.
                if (_root != null && go.transform.parent == null)
                {
                    go.transform.SetParent(_root, false);
                }
            }

            go.SetActive(true);
            ActiveList(key).Add(go);
            return go;
        }

        public void Recycle(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            PooledTag tag = go.GetComponent<PooledTag>();
            string key = tag != null ? tag.Key : "__untagged";
            ActiveList(key).Remove(go);
            go.SetActive(false);
            IdleStack(key).Push(go);
        }

        /// <summary>Called by the restart path. Keeps instances alive for reuse, only deactivates them.</summary>
        public void RecycleAll()
        {
            foreach (KeyValuePair<string, List<GameObject>> kv in _active)
            {
                List<GameObject> list = kv.Value;
                Stack<GameObject> idle = IdleStack(kv.Key);
                for (int i = 0; i < list.Count; i++)
                {
                    GameObject go = list[i];
                    if (go == null)
                    {
                        continue;
                    }

                    go.SetActive(false);
                    idle.Push(go);
                }

                list.Clear();
            }
        }

        public int CountActive()
        {
            int total = 0;
            foreach (KeyValuePair<string, List<GameObject>> kv in _active)
            {
                total += kv.Value.Count;
            }

            return total;
        }

        List<GameObject> ActiveList(string key)
        {
            List<GameObject> list;
            if (!_active.TryGetValue(key, out list))
            {
                list = new List<GameObject>(32);
                _active[key] = list;
            }

            return list;
        }

        Stack<GameObject> IdleStack(string key)
        {
            Stack<GameObject> idle;
            if (!_idle.TryGetValue(key, out idle))
            {
                idle = new Stack<GameObject>(16);
                _idle[key] = idle;
            }

            return idle;
        }
    }
}
