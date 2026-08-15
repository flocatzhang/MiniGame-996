using System.Collections.Generic;

namespace OfficeHell.Model
{
    public enum StatType
    {
        MaxSan = 0,
        Atk = 1,
        CritChance = 2,
        CritMulti = 3,
        Def = 4,
        Dodge = 5,
        MoveSpeed = 6,
        Haste = 7,
        Luck = 8,
        PickupRadius = 9,
        Count = 10,
    }

    public enum ModifierOp
    {
        Flat = 0,
        PercentAdd = 1,
        PercentMulti = 2,
    }

    public struct StatModifier
    {
        public StatType Stat;
        public ModifierOp Op;
        public float Value;

        /// <summary>Equipment instance id, card id or weapon slot. Used to remove a whole batch at once.</summary>
        public int SourceId;

        public StatModifier(StatType stat, ModifierOp op, float value, int sourceId)
        {
            Stat = stat;
            Op = op;
            Value = value;
            SourceId = sourceId;
        }
    }

    /// <summary>
    /// The single numeric pipeline. Equipment affixes, level up bundles, curses and any future
    /// global difficulty coefficient are all StatModifier rows, there is no second path.
    /// </summary>
    public sealed class StatSheet
    {
        readonly float[] _base = new float[(int)StatType.Count];
        readonly float[] _final = new float[(int)StatType.Count];
        readonly List<StatModifier> _mods = new List<StatModifier>(32);
        bool _dirty = true;

        public int ModifierCount
        {
            get { return _mods.Count; }
        }

        public void SetBase(StatType t, float v)
        {
            _base[(int)t] = v;
            _dirty = true;
        }

        public float GetBase(StatType t)
        {
            return _base[(int)t];
        }

        public void AddBase(StatType t, float delta)
        {
            _base[(int)t] += delta;
            _dirty = true;
        }

        public void AddModifier(StatModifier m)
        {
            _mods.Add(m);
            _dirty = true;
        }

        public void RemoveBySource(int sourceId)
        {
            for (int i = _mods.Count - 1; i >= 0; i--)
            {
                if (_mods[i].SourceId == sourceId)
                {
                    _mods.RemoveAt(i);
                    _dirty = true;
                }
            }
        }

        public void ClearModifiers()
        {
            if (_mods.Count > 0)
            {
                _mods.Clear();
                _dirty = true;
            }
        }

        public float Get(StatType t)
        {
            if (_dirty)
            {
                Recalc();
            }

            return _final[(int)t];
        }

        void Recalc()
        {
            int count = (int)StatType.Count;
            for (int i = 0; i < count; i++)
            {
                _final[i] = _base[i];
            }

            // Flat first, then additive percent on the flat total, then multiplicative percent.
            for (int i = 0; i < _mods.Count; i++)
            {
                if (_mods[i].Op == ModifierOp.Flat)
                {
                    _final[(int)_mods[i].Stat] += _mods[i].Value;
                }
            }

            for (int s = 0; s < count; s++)
            {
                float pctAdd = 0f;
                for (int i = 0; i < _mods.Count; i++)
                {
                    if (_mods[i].Op == ModifierOp.PercentAdd && (int)_mods[i].Stat == s)
                    {
                        pctAdd += _mods[i].Value;
                    }
                }

                if (pctAdd != 0f)
                {
                    _final[s] *= 1f + pctAdd * 0.01f;
                }
            }

            for (int i = 0; i < _mods.Count; i++)
            {
                if (_mods[i].Op == ModifierOp.PercentMulti)
                {
                    _final[(int)_mods[i].Stat] *= 1f + _mods[i].Value * 0.01f;
                }
            }

            _dirty = false;
        }
    }
}
