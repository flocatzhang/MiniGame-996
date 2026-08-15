using OfficeHell.Config;
using UnityEngine;

namespace OfficeHell.View
{
    /// <summary>
    /// Placeholder audio generated from the xml row. Lets every Play call site be wired and balanced
    /// before a single wav exists, and keeps the greybox free of binary assets.
    /// </summary>
    public static class Synth
    {
        public static AudioClip Build(SfxDef def, int sampleRate)
        {
            int samples = Mathf.Max(64, (int)(def.Dur * sampleRate));
            float[] data = new float[samples];

            switch (def.Synth)
            {
                case SynthKind.Thud:
                    Thud(data, sampleRate, def.Freq);
                    break;
                case SynthKind.Chime:
                    Chime(data, sampleRate, def.Freq);
                    break;
                case SynthKind.Noise:
                    Noise(data);
                    break;
                case SynthKind.Sweep:
                    Sweep(data, sampleRate, def.Freq);
                    break;
                case SynthKind.Bell:
                    Bell(data, sampleRate, def.Freq);
                    break;
                default:
                    Blip(data, sampleRate, def.Freq);
                    break;
            }

            AudioClip clip = AudioClip.Create(def.Id, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Two bar ambient pad so ducking has something audible to act on.</summary>
        public static AudioClip BuildBgmLoop(int sampleRate)
        {
            int samples = sampleRate * 4;
            float[] data = new float[samples];
            float[] chord = { 110f, 164.81f, 220f, 261.63f };

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float v = 0f;
                for (int n = 0; n < chord.Length; n++)
                {
                    v += Mathf.Sin(2f * Mathf.PI * chord[n] * t) / chord.Length;
                }

                float pulse = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                data[i] = v * 0.18f * pulse;
            }

            AudioClip clip = AudioClip.Create("bgm_synth", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Blip(float[] data, int rate, float freq)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float env = Env(i, data.Length, 0.02f, 0.7f);
                data[i] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t)) * 0.22f * env;
            }
        }

        static void Thud(float[] data, int rate, float freq)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float f = Mathf.Lerp(freq, freq * 0.35f, (float)i / data.Length);
                float env = Env(i, data.Length, 0.005f, 0.5f);
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * 0.4f * env;
            }
        }

        static void Chime(float[] data, int rate, float freq)
        {
            float[] partials = { 1f, 1.5f, 2.0f };
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float v = 0f;
                for (int n = 0; n < partials.Length; n++)
                {
                    v += Mathf.Sin(2f * Mathf.PI * freq * partials[n] * t) / (n + 1);
                }

                data[i] = v * 0.2f * Env(i, data.Length, 0.01f, 0.9f);
            }
        }

        static void Bell(float[] data, int rate, float freq)
        {
            float[] partials = { 1f, 2.76f, 5.4f };
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float v = 0f;
                for (int n = 0; n < partials.Length; n++)
                {
                    v += Mathf.Sin(2f * Mathf.PI * freq * partials[n] * t) * Mathf.Exp(-3f * t * (n + 1));
                }

                data[i] = v * 0.22f * Env(i, data.Length, 0.002f, 0.98f);
            }
        }

        static void Noise(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Random.Range(-1f, 1f) * 0.16f * Env(i, data.Length, 0.01f, 0.6f);
            }
        }

        static void Sweep(float[] data, int rate, float freq)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float f = Mathf.Lerp(freq * 0.5f, freq * 2f, (float)i / data.Length);
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * 0.2f * Env(i, data.Length, 0.01f, 0.8f);
            }
        }

        static float Env(int i, int total, float attackFraction, float decayPower)
        {
            float t = (float)i / total;
            float attack = attackFraction > 0f ? Mathf.Clamp01(t / attackFraction) : 1f;
            float decay = Mathf.Pow(1f - t, 1f + decayPower * 4f);
            return attack * decay;
        }
    }
}
