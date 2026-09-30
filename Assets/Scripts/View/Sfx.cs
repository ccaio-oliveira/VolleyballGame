using UnityEngine;

namespace Volley.View
{
    /// <summary>
    /// Sound effects synthesized at load time — placeholders until recorded samples replace
    /// them. A ball hit is a short noise click plus a resonant decaying tone; the whistle is
    /// two sustained tones with amplitude modulation.
    /// </summary>
    public static class Sfx
    {
        private const int SampleRate = 44100;

        public static AudioClip Hit(string name, float duration, float frequency, float hardness, float decay)
        {
            int count = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[count];

            var rng = new System.Random(name.GetHashCode());
            float phase = 0f, overtonePhase = 0f, lowPass = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;

                // contact click: noise only in the first few milliseconds
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lowPass += (noise - lowPass) * 0.55f;   // one-pole low-pass filter
                float click = lowPass * Mathf.Exp(-t / 0.0035f) * hardness;

                // body: every real impact drops slightly in pitch as it decays.
                // Frequency is integrated into the phase — never multiplied by time.
                float instantFrequency = frequency * (0.85f + 0.15f * Mathf.Exp(-t * 25f));
                phase += 2f * Mathf.PI * instantFrequency / SampleRate;
                overtonePhase += 2f * Mathf.PI * instantFrequency * 2.4f / SampleRate;

                float body = (Mathf.Sin(phase) + 0.35f * Mathf.Sin(overtonePhase) * Mathf.Exp(-t * decay * 2f))
                           * Mathf.Exp(-t * decay);

                samples[i] = Mathf.Clamp(click * 0.9f + body * 0.85f, -1f, 1f);
            }

            return CreateClip(name, samples);
        }

        public static AudioClip Whistle()
        {
            const float duration = 0.60f;
            int count = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[count];

            var rng = new System.Random(7);
            float phaseA = 0f, phaseB = 0f, lowPass = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;

                // attack · sustain · release — a whistle does not decay
                float envelope = t < 0.012f ? t / 0.012f
                               : t < 0.44f  ? 1f
                               : Mathf.Max(0f, 1f - (t - 0.44f) / 0.10f);

                phaseA += 2f * Mathf.PI * 3150f / SampleRate;
                phaseB += 2f * Mathf.PI * 4020f / SampleRate;

                // the pea inside the whistle modulates AMPLITUDE, not pitch
                float tremolo = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * 26f * t);

                // breath noise only at the start
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lowPass += (noise - lowPass) * 0.7f;
                float breath = lowPass * Mathf.Exp(-t / 0.03f) * 0.25f;

                float tone = Mathf.Sin(phaseA) * 0.65f + Mathf.Sin(phaseB) * 0.35f;
                samples[i] = (tone * tremolo + breath) * envelope * 0.45f;
            }

            return CreateClip("whistle", samples);
        }

        private static AudioClip CreateClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
