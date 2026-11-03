using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.Islands
{
    /// <summary>
    /// Drives every moving part of a building (spinning wheels, bobbing clouds, pulsing glows, smoke).
    /// Animation speed follows the machine activity and nothing runs while the building is off screen.
    /// </summary>
    public sealed class BuildingAnimator : MonoBehaviour
    {
        class Spin { public Transform T; public Vector3 Axis; public float Dps; }
        class Bob { public Transform T; public Vector3 Base; public float Amp; public float Freq; public float Phase; }
        class Pulse { public Renderer R; public Color C; public float Lo; public float Hi; public float Freq; public float Phase; }
        class Puff { public Transform T; public Vector3 Start; public float Rise; public float Period; public float Offset; public float Size; }
        class Slide { public Transform T; public Vector3 From; public Vector3 To; public float Period; public float Offset; }
        class Orbit { public Transform T; public Vector3 Center; public float Radius; public float Dps; public float Phase; public float BobAmp; }

        readonly List<Spin> spins = new List<Spin>();
        readonly List<Bob> bobs = new List<Bob>();
        readonly List<Pulse> pulses = new List<Pulse>();
        readonly List<Puff> puffs = new List<Puff>();
        readonly List<Slide> slides = new List<Slide>();
        readonly List<Orbit> orbits = new List<Orbit>();
        MaterialPropertyBlock block;

        /// <summary>0..1 smoothed activity of the machine (idle machines move slowly).</summary>
        [HideInInspector] public float Activity = 1f;
        [HideInInspector] public Renderer VisibilityRenderer;

        public void AddSpin(Transform t, Vector3 axis, float dps)
        {
            spins.Add(new Spin { T = t, Axis = axis.normalized, Dps = dps });
        }

        public void AddBob(Transform t, float amp, float freq, float phase)
        {
            bobs.Add(new Bob { T = t, Base = t.localPosition, Amp = amp, Freq = freq, Phase = phase });
        }

        public void AddPulse(Renderer r, Color c, float lo, float hi, float freq, float phase)
        {
            pulses.Add(new Pulse { R = r, C = c, Lo = lo, Hi = hi, Freq = freq, Phase = phase });
        }

        public void AddPuff(Transform t, float rise, float period, float offset, float size)
        {
            puffs.Add(new Puff { T = t, Start = t.localPosition, Rise = rise, Period = period, Offset = offset, Size = size });
        }

        public void AddSlide(Transform t, Vector3 delta, float period, float offset)
        {
            slides.Add(new Slide { T = t, From = t.localPosition, To = t.localPosition + delta, Period = period, Offset = offset });
        }

        public void AddOrbit(Transform t, Vector3 center, float radius, float dps, float phase, float bobAmp)
        {
            orbits.Add(new Orbit { T = t, Center = center, Radius = radius, Dps = dps, Phase = phase, BobAmp = bobAmp });
        }

        void Update()
        {
            if (VisibilityRenderer != null && !VisibilityRenderer.isVisible) return;

            float dt = Time.deltaTime;
            float time = Time.time;
            float k = Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(Activity));

            for (int i = 0; i < spins.Count; i++)
            {
                Spin s = spins[i];
                if (s.T != null) s.T.Rotate(s.Axis * (s.Dps * dt * k), Space.Self);
            }
            for (int i = 0; i < bobs.Count; i++)
            {
                Bob b = bobs[i];
                if (b.T != null) b.T.localPosition = b.Base + Vector3.up * (Mathf.Sin(time * b.Freq * (0.4f + 0.6f * k) + b.Phase) * b.Amp);
            }
            for (int i = 0; i < orbits.Count; i++)
            {
                Orbit o = orbits[i];
                if (o.T == null) continue;
                float ang = (time * o.Dps * k + o.Phase) * Mathf.Deg2Rad;
                Vector3 p = o.Center + new Vector3(Mathf.Cos(ang) * o.Radius, Mathf.Sin(time * 2f + o.Phase) * o.BobAmp, Mathf.Sin(ang) * o.Radius);
                o.T.localPosition = p;
            }
            for (int i = 0; i < slides.Count; i++)
            {
                Slide s = slides[i];
                if (s.T == null) continue;
                float f = Mathf.Repeat(time * k / s.Period + s.Offset, 1f);
                s.T.localPosition = Vector3.Lerp(s.From, s.To, f);
                s.T.localScale = Vector3.one * Mathf.Clamp01(Mathf.Min(f, 1f - f) * 8f);
            }
            for (int i = 0; i < puffs.Count; i++)
            {
                Puff p = puffs[i];
                if (p.T == null) continue;
                float f = Mathf.Repeat(time * k / p.Period + p.Offset, 1f);
                p.T.localPosition = p.Start + new Vector3(Mathf.Sin(f * 5f + p.Offset * 9f) * 0.08f * f, p.Rise * f, 0f);
                p.T.localScale = Vector3.one * (p.Size * Mathf.Sin(f * Mathf.PI) * (0.5f + f * 0.9f));
            }
            if (pulses.Count > 0)
            {
                if (block == null) block = new MaterialPropertyBlock();
                for (int i = 0; i < pulses.Count; i++)
                {
                    Pulse p = pulses[i];
                    if (p.R == null) continue;
                    float f = (Mathf.Sin(time * p.Freq + p.Phase) * 0.5f + 0.5f);
                    float intensity = Mathf.Lerp(p.Lo, p.Hi, f) * Mathf.Lerp(0.35f, 1f, k);
                    p.R.GetPropertyBlock(block);
                    block.SetColor("_EmissionColor", p.C * intensity);
                    p.R.SetPropertyBlock(block);
                }
            }
        }
    }
}
