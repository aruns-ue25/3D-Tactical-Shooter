using System.Collections;
using UnityEngine;

namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// Utility for creating instant, punchy audiovisual feedback:
    /// - Procedural gunshot sound & impact sound (synthesized audio, zero asset dependencies)
    /// - Visible bullet tracer line traveling from muzzle to target
    /// - Muzzle flash
    /// - Impact sparks
    /// </summary>
    public static class CombatEffects
    {
        private static AudioClip gunshotClip;
        private static AudioClip hitImpactClip;
        private static AudioClip defeatClip;
        private static AudioClip footstepClip;
        private static AudioClip victoryClip;
        private static AudioClip doorBreakClip;
        private static AudioClip doorMoveClip;
        private static AudioClip objectiveSecuredClip;
        private static AudioClip downloadTickClip;
        private static AudioClip reloadClip;
        private static AudioClip dryFireClip;
        private static AudioClip medkitClip;
        private static Material tracerMaterial;

        public static void InitializeAudio()
        {
            if (gunshotClip == null) gunshotClip = GenerateGunshotSound();
            if (hitImpactClip == null) hitImpactClip = GenerateHitImpactSound();
            if (defeatClip == null) defeatClip = GenerateDefeatSound();
            if (footstepClip == null) footstepClip = GenerateFootstepSound();
            if (victoryClip == null) victoryClip = GenerateVictorySound();
            if (doorBreakClip == null) doorBreakClip = GenerateDoorBreakSound();
            if (doorMoveClip == null) doorMoveClip = GenerateDoorMoveSound();
            if (objectiveSecuredClip == null) objectiveSecuredClip = GenerateObjectiveSecuredSound();
            if (downloadTickClip == null) downloadTickClip = GenerateDownloadTickSound();
            if (reloadClip == null) reloadClip = GenerateReloadSound();
            if (dryFireClip == null) dryFireClip = GenerateDryFireSound();
            if (medkitClip == null) medkitClip = GenerateMedkitSound();
            if (tracerMaterial == null) tracerMaterial = CreateTracerMaterial();
        }

        public static void PlayFootstep(Vector3 position, bool isSprinting)
        {
            InitializeAudio();
            float pitch = isSprinting ? UnityEngine.Random.Range(1.05f, 1.2f) : UnityEngine.Random.Range(0.85f, 1.0f);
            float volume = isSprinting ? 0.5f : 0.35f;
            PlayClipAtPoint(footstepClip, position, volume, pitch);
        }

        public static void PlayGunshot(Vector3 position)
        {
            InitializeAudio();
            PlayClipAtPoint(gunshotClip, position, 0.75f, 1.0f);
        }

        public static void PlayHitImpact(Vector3 position)
        {
            InitializeAudio();
            PlayClipAtPoint(hitImpactClip, position, 0.9f, UnityEngine.Random.Range(0.95f, 1.05f));
        }

        public static void PlayDefeatSound(Vector3 position)
        {
            InitializeAudio();
            PlayClipAtPoint(defeatClip, position, 1.0f, 0.8f);
        }

        public static void PlayVictorySound(Vector3 position)
        {
            InitializeAudio();
            PlayClipAtPoint(victoryClip, position, 1.0f, 1.0f);
        }

        public static void PlayDoorBreakSound(Vector3 position)
        {
            InitializeAudio();
            PlayClipAtPoint(doorBreakClip, position, 1.0f, 0.95f);
        }

        public static void PlayDoorOpenSound(Vector3 position)
        {
            InitializeAudio();
            PlayClipAtPoint(doorMoveClip, position, 0.85f, 1.05f);
        }

        public static void PlayDoorCloseSound(Vector3 position)
        {
            InitializeAudio();
            PlayClipAtPoint(doorMoveClip, position, 0.85f, 0.88f);
        }

        public static void PlayObjectiveSecuredSound(Vector3 position)
        {
            InitializeAudio();
            PlayClipAtPoint(objectiveSecuredClip, position, 1.0f, 1.0f);
        }

        public static void PlayReloadSound(Vector3 position)
        {
            InitializeAudio();
            PlayClipAtPoint(reloadClip, position, 0.85f, 1.0f);
        }

        public static void PlayDryFireClick(Vector3 position)
        {
            InitializeAudio();
            PlayClipAtPoint(dryFireClip, position, 0.75f, 1.0f);
        }

        public static void PlayAmmoPickupSound(Vector3 position)
        {
            InitializeAudio();
            PlayClipAtPoint(reloadClip, position, 0.95f, 1.25f);
        }

        public static void PlayMedkitPickupSound(Vector3 position)
        {
            InitializeAudio();
            PlayClipAtPoint(medkitClip, position, 1.0f, 1.0f);
        }

        public static void PlayDownloadTick(Vector3 position, float progress)
        {
            InitializeAudio();
            float pitch = Mathf.Lerp(0.85f, 1.55f, progress);
            PlayClipAtPoint(downloadTickClip, position, 0.45f, pitch);
        }

        public static void SpawnTracer(Vector3 start, Vector3 end, MonoBehaviour runner = null)
        {
            InitializeAudio();
            if (tracerMaterial == null) tracerMaterial = CreateTracerMaterial();

            GameObject tracerObj = new GameObject("BulletTracer");
            LineRenderer lr = tracerObj.AddComponent<LineRenderer>();
            lr.material = tracerMaterial;
            lr.startColor = new Color(1f, 0.9f, 0.4f, 0.9f);
            lr.endColor = new Color(1f, 0.4f, 0.1f, 0.1f);
            lr.startWidth = 0.04f;
            lr.endWidth = 0.015f;
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            const float duration = 0.07f;
            // Native engine-level fail-safe destruction: guarantees the tracer can never persist or freeze
            Object.Destroy(tracerObj, duration + 0.03f);

            // Self-governing fader attached directly to the tracer object so that shooter death/StopAllCoroutines cannot abort cleanup
            var fader = tracerObj.AddComponent<BulletTracerFader>();
            fader.Initialize(lr, duration);
        }

        public static void SpawnImpactSpark(Vector3 point, Vector3 normal, MonoBehaviour runner = null)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "ImpactSpark";
            spark.transform.position = point + normal * 0.02f;
            spark.transform.localScale = Vector3.one * 0.12f;
            Object.Destroy(spark.GetComponent<Collider>());

            if (tracerMaterial == null) tracerMaterial = CreateTracerMaterial();
            Renderer r = spark.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = tracerMaterial;
                r.material.color = new Color(1f, 0.8f, 0.2f);
            }

            const float duration = 0.08f;
            // Native engine-level fail-safe destruction
            Object.Destroy(spark, duration + 0.03f);

            var shrinker = spark.AddComponent<ImpactSparkShrinker>();
            shrinker.Initialize(spark.transform.localScale, duration);
        }

        private static void PlayClipAtPoint(AudioClip clip, Vector3 pos, float volume, float pitch)
        {
            if (clip == null) return;
            GameObject audioObj = new GameObject("TempAudio");
            audioObj.transform.position = pos;
            AudioSource src = audioObj.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.spatialBlend = 0.2f; // Slight 3D blend for positional awareness
            src.Play();
            Object.Destroy(audioObj, clip.length + 0.1f);
        }

        private static Material CreateTracerMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material mat = new Material(shader);
            mat.color = new Color(1f, 0.85f, 0.3f);
            return mat;
        }

        private static AudioClip GenerateGunshotSound()
        {
            int sampleRate = 44100;
            int length = (int)(sampleRate * 0.14f);
            float[] data = new float[length];
            System.Random rand = new System.Random(42);

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float decay = Mathf.Exp(-t * 22f);
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
                float thump = Mathf.Sin(2f * Mathf.PI * 90f * (float)i / sampleRate);
                data[i] = (noise * 0.65f + thump * 0.35f) * decay;
            }

            AudioClip clip = AudioClip.Create("SynthesizedGunshot", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateHitImpactSound()
        {
            int sampleRate = 44100;
            int length = (int)(sampleRate * 0.08f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float decay = Mathf.Exp(-t * 28f);
                float tone = Mathf.Sin(2f * Mathf.PI * 520f * (float)i / sampleRate);
                data[i] = tone * decay * 0.7f;
            }

            AudioClip clip = AudioClip.Create("SynthesizedHit", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateDefeatSound()
        {
            int sampleRate = 44100;
            int length = (int)(sampleRate * 0.22f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float decay = Mathf.Exp(-t * 12f);
                float tone = Mathf.Sin(2f * Mathf.PI * (260f - t * 120f) * (float)i / sampleRate);
                data[i] = tone * decay * 0.8f;
            }

            AudioClip clip = AudioClip.Create("SynthesizedDefeat", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateFootstepSound()
        {
            int sampleRate = 44100;
            int length = (int)(sampleRate * 0.075f);
            float[] data = new float[length];
            System.Random rand = new System.Random(1337);

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float decay = Mathf.Exp(-t * 36f);
                float thud = Mathf.Sin(2f * Mathf.PI * (75f - t * 25f) * (float)i / sampleRate);
                float scuff = (float)(rand.NextDouble() * 2.0 - 1.0) * 0.35f;
                data[i] = (thud * 0.7f + scuff * 0.3f) * decay;
            }

            AudioClip clip = AudioClip.Create("SynthesizedFootstep", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateVictorySound()
        {
            int sampleRate = 44100;
            float duration = 1.0f;
            int length = (int)(sampleRate * duration);
            float[] data = new float[length];

            // Triumphant arpeggio: C5 (523Hz), E5 (659Hz), G5 (784Hz), C6 (1046Hz)
            float[] freqs = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f };
            float noteDur = 0.22f;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                int noteIndex = Mathf.Clamp((int)(t / noteDur), 0, freqs.Length - 1);
                float freq = freqs[noteIndex];
                float noteT = (t - noteIndex * noteDur);
                float env = Mathf.Exp(-noteT * 4.5f);
                if (noteIndex == freqs.Length - 1) env = Mathf.Exp(-noteT * 2.0f); // sustain final victory note
                float sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f +
                               Mathf.Sin(4f * Mathf.PI * freq * t) * 0.2f;
                data[i] = sample * env * 0.85f;
            }

            AudioClip clip = AudioClip.Create("SynthesizedVictory", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateDoorBreakSound()
        {
            int sampleRate = 44100;
            float duration = 0.5f;
            int length = (int)(sampleRate * duration);
            float[] data = new float[length];
            System.Random rand = new System.Random(777);

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float decay = Mathf.Exp(-t * 9f);

                // Heavy impact thud
                float thud = Mathf.Sin(2f * Mathf.PI * (80f - t * 45f) * (float)i / sampleRate) * 0.65f;

                // Splintering wood noise
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0) * 0.45f;
                float crack = (float)(rand.NextDouble() > 0.88 ? (rand.NextDouble() * 2.0 - 1.0) * 1.4f : 0f);

                data[i] = (thud + noise + crack) * decay * 0.95f;
            }

            AudioClip clip = AudioClip.Create("SynthesizedDoorBreak", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateObjectiveSecuredSound()
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int length = (int)(sampleRate * duration);
            float[] data = new float[length];

            // Electronic double chime: Note 1 (880Hz, A5) -> Note 2 (1320Hz, E6)
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float freq = t < 0.18f ? 880f : 1320f;
                float noteT = t < 0.18f ? t : (t - 0.18f);
                float decay = Mathf.Exp(-noteT * 9f);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f +
                             Mathf.Sin(4f * Mathf.PI * freq * t) * 0.2f;
                data[i] = tone * decay * 0.85f;
            }

            AudioClip clip = AudioClip.Create("SynthesizedObjectiveSecured", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateDownloadTickSound()
        {
            int sampleRate = 44100;
            float duration = 0.05f;
            int length = (int)(sampleRate * duration);
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 60f);
                float tone = Mathf.Sin(2f * Mathf.PI * 1050f * t) * 0.7f +
                             Mathf.Sin(2f * Mathf.PI * 2100f * t) * 0.25f;
                data[i] = tone * decay * 0.65f;
            }
            AudioClip clip = AudioClip.Create("SynthesizedDownloadTick", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateReloadSound()
        {
            int sampleRate = 44100;
            float duration = 0.28f;
            int length = (int)(sampleRate * duration);
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;
                // Click 1 (mag drop at t=0.03)
                if (t >= 0.02f && t < 0.08f)
                {
                    float dt = t - 0.02f;
                    sample += Mathf.Sin(2f * Mathf.PI * 1800f * dt) * Mathf.Exp(-dt * 90f) * 0.8f;
                }
                // Click 2 (mag lock at t=0.16)
                if (t >= 0.15f && t < 0.25f)
                {
                    float dt = t - 0.15f;
                    sample += (Mathf.Sin(2f * Mathf.PI * 950f * dt) + Mathf.Sin(2f * Mathf.PI * 2400f * dt) * 0.5f) * Mathf.Exp(-dt * 65f) * 0.9f;
                }
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("SynthesizedReload", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateDryFireSound()
        {
            int sampleRate = 44100;
            float duration = 0.08f;
            int length = (int)(sampleRate * duration);
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 90f);
                float tone = Mathf.Sin(2f * Mathf.PI * 2200f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 3400f * t) * 0.3f;
                data[i] = tone * decay * 0.8f;
            }
            AudioClip clip = AudioClip.Create("SynthesizedDryFire", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateMedkitSound()
        {
            int sampleRate = 44100;
            float duration = 0.36f;
            int length = (int)(sampleRate * duration);
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                // Stim pneumatic hiss + healing chime
                float hiss = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * 0.35f;
                float chime = Mathf.Sin(2f * Mathf.PI * 523.25f * t) * Mathf.Exp(-t * 7f) * 0.5f + // C5
                              Mathf.Sin(2f * Mathf.PI * 659.25f * t) * Mathf.Exp(-t * 6f) * 0.4f + // E5
                              Mathf.Sin(2f * Mathf.PI * 783.99f * t) * Mathf.Exp(-t * 5f) * 0.3f;   // G5
                data[i] = Mathf.Clamp(hiss + chime, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("SynthesizedMedkit", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateDoorMoveSound()
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int length = (int)(sampleRate * duration);
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                // Heavy metallic servo hum + latch click
                float hum = Mathf.Sin(2f * Mathf.PI * (120f + 60f * t) * t) * 0.45f;
                float latch = (t < 0.08f) ? Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 40f) * 0.6f : 0f;
                float friction = (UnityEngine.Random.value * 2f - 1f) * 0.12f * Mathf.Sin(Mathf.PI * (t / duration));
                data[i] = Mathf.Clamp(hum + latch + friction, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("SynthesizedDoorMove", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>
    /// Self-contained component ensuring bullet tracers fade and destroy themselves
    /// completely independent of the shooter's lifecycle.
    /// </summary>
    public class BulletTracerFader : MonoBehaviour
    {
        private LineRenderer lr;
        private float duration = 0.07f;
        private float elapsed = 0f;

        public void Initialize(LineRenderer line, float fadeDuration)
        {
            lr = line;
            duration = Mathf.Max(0.01f, fadeDuration);
            elapsed = 0f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            if (lr != null)
            {
                float alpha = Mathf.Clamp01(1f - progress);
                lr.startColor = new Color(1f, 0.9f, 0.4f, alpha);
            }

            if (elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Self-contained component ensuring impact sparks shrink and destroy themselves
    /// completely independent of the shooter's lifecycle.
    /// </summary>
    public class ImpactSparkShrinker : MonoBehaviour
    {
        private Vector3 startScale;
        private float duration = 0.08f;
        private float elapsed = 0f;

        public void Initialize(Vector3 initialScale, float shrinkDuration)
        {
            startScale = initialScale;
            duration = Mathf.Max(0.01f, shrinkDuration);
            elapsed = 0f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);

            if (elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }
    }

}
