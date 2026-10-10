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
        private static Material tracerMaterial;

        public static void InitializeAudio()
        {
            if (gunshotClip == null) gunshotClip = GenerateGunshotSound();
            if (hitImpactClip == null) hitImpactClip = GenerateHitImpactSound();
            if (defeatClip == null) defeatClip = GenerateDefeatSound();
            if (tracerMaterial == null) tracerMaterial = CreateTracerMaterial();
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

        public static void SpawnTracer(Vector3 start, Vector3 end, MonoBehaviour runner)
        {
            InitializeAudio();
            if (runner != null)
            {
                runner.StartCoroutine(TracerRoutine(start, end));
            }
        }

        private static IEnumerator TracerRoutine(Vector3 start, Vector3 end)
        {
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

            float duration = 0.07f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - (elapsed / duration);
                lr.startColor = new Color(1f, 0.9f, 0.4f, alpha);
                yield return null;
            }

            Object.Destroy(tracerObj);
        }

        public static void SpawnImpactSpark(Vector3 point, Vector3 normal, MonoBehaviour runner)
        {
            if (runner != null)
            {
                runner.StartCoroutine(ImpactSparkRoutine(point, normal));
            }
        }

        private static IEnumerator ImpactSparkRoutine(Vector3 point, Vector3 normal)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "ImpactSpark";
            spark.transform.position = point + normal * 0.02f;
            spark.transform.localScale = Vector3.one * 0.12f;
            Object.Destroy(spark.GetComponent<Collider>());

            Renderer r = spark.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = tracerMaterial;
                r.material.color = new Color(1f, 0.8f, 0.2f);
            }

            float duration = 0.08f;
            float elapsed = 0f;
            Vector3 startScale = spark.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                if (spark != null)
                {
                    spark.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
                }
                yield return null;
            }

            if (spark != null) Object.Destroy(spark);
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
    }
}
