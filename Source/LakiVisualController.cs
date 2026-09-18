using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Laki
{
    internal sealed class LakiVisualController : MonoBehaviour
    {
        private LakiKind kind;
        private Transform[] parts;
        private MeshRenderer[] renderers;
        private readonly Vector3[] origins = new Vector3[5];
        private readonly float[] sizes = new float[5];
        private readonly Vector3[] burstOrigins = new Vector3[5];
        private MaterialPropertyBlock block;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private IGamePause pause;
        private NoteController note;
        private Transform root;
        private float age, burstAge;
        private bool bursting;

        public void Setup(LakiKind kind, Transform[] parts, MeshRenderer[] renderers, IGamePause pause)
        {
            this.kind = kind; this.parts = parts; this.renderers = renderers; this.pause = pause;
            root = transform;
            block = new MaterialPropertyBlock();
            SetAlpha(.8f);
        }
        public bool Attach(NoteController controller)
        {
            var parent = controller.noteTransform;
            var scale = parent.localScale;
            float noteScale = controller.uniformScale;
            if (!VisualScale.TryCompensation(noteScale, scale.x, scale.y, scale.z, out float inverse)) return false;
            if (!TryGetExtents(parent, out var extents)) return false;
            SceneManager.MoveGameObjectToScene(gameObject, controller.gameObject.scene);
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            // Only our dedicated visual root is scaled; the note/cuttable transforms are untouched.
            root.localScale = Vector3.one * inverse;
            note = controller;
            float x = VisualScale.Offset(extents.x, noteScale), y = VisualScale.Offset(extents.y, noteScale);
            origins[0] = new Vector3(-x, .09f * noteScale, -.015f);
            origins[1] = new Vector3(-.075f * noteScale, y, -.015f);
            origins[2] = new Vector3(.10f * noteScale, y + .012f, -.015f);
            origins[3] = new Vector3(x, -.09f * noteScale, -.015f);
            origins[4] = new Vector3(-.07f * noteScale, -y, -.015f);
            for (int i = 0; i < 5; i++)
            {
                sizes[i] = i == 0 || i == 3 ? .038f : .021f;
                if (kind == LakiKind.Super && i == 2) sizes[i] = .048f;
                if (kind == LakiKind.Secret && i == 0) sizes[i] = .052f;
                parts[i].localPosition = origins[i];
                parts[i].localScale = Vector3.one * sizes[i];
                parts[i].gameObject.layer = parent.gameObject.layer;
            }
            gameObject.layer = parent.gameObject.layer;
            gameObject.SetActive(true);
            return true;
        }

        private static bool TryGetExtents(Transform parent, out Vector2 extents)
        {
            extents = new Vector2(.20f, .20f);
            Vector3 scale = parent.lossyScale;
            if (!Finite(scale.x) || !Finite(scale.y) || !Finite(scale.z) ||
                scale.x < .4999f || scale.x > 1.6001f ||
                scale.y < .4999f || scale.y > 1.6001f ||
                scale.z < .4999f || scale.z > 1.6001f) return false;
            // One small scan of THIS selected note before adding our children.
            // Never inspect or rewrite its materials/colors. Oversize/custom layouts fail closed.
            var existing = parent.GetComponentsInChildren<Renderer>(false);
            if (existing.Length == 0 || existing.Length > 64) return false;
            foreach (var renderer in existing)
            {
                if (!renderer.enabled) continue;
                Bounds bounds = renderer.localBounds;
                Transform rendererTransform = renderer.transform;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 p = parent.InverseTransformPoint(rendererTransform.TransformPoint(local));
                    if (!Finite(p.x) || !Finite(p.y)) return false;
                    extents.x = Mathf.Max(extents.x, Mathf.Abs(p.x));
                    extents.y = Mathf.Max(extents.y, Mathf.Abs(p.y));
                }
            }
            return extents.x <= .34f && extents.y <= .34f;
        }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);

        public void Celebrate()
        {
            // Preserve the compensated world scale when detaching from the pooled note.
            note = null;
            root.SetParent(null, true);
            bursting = true;
            burstAge = 0;
            for (int i = 0; i < 5; i++) burstOrigins[i] = parts[i].localPosition;
        }
        public void Hide()
        {
            note = null;
            bursting = false;
            gameObject.SetActive(false);
            root.SetParent(null, true);
        }
        private void Update()
        {
            try
            {
                if (pause != null && pause.isPaused) return;
                float delta = Mathf.Min(Time.unscaledDeltaTime, .05f);
                if (bursting)
                {
                    burstAge += delta;
                    float duration = kind == LakiKind.Secret ? .28f : kind == LakiKind.Super ? .22f : .24f;
                    float t = Mathf.Clamp01(burstAge / duration);
                    if (t >= 1) { Hide(); return; }
                    float distance = kind == LakiKind.Super ? .115f : .075f;
                    for (int i = 0; i < 5; i++)
                    {
                        Vector3 direction = origins[i].normalized;
                        var drift = direction * (distance * (1 - (1 - t) * (1 - t)));
                        if (kind == LakiKind.Secret) drift += Vector3.up * (.03f * t);
                        parts[i].localPosition = burstOrigins[i] + drift;
                        float flash = kind == LakiKind.Super ? 1 + .30f * Mathf.Sin(t * Mathf.PI) : 1 + .10f * t;
                        parts[i].localScale = Vector3.one * sizes[i] * flash * (1 - .55f * t);
                        parts[i].localRotation = Quaternion.Euler(0, 0, (kind == LakiKind.Secret ? -15 : 25) * t);
                    }
                    SetAlpha(.8f * (1 - t));
                }
                else
                {
                    if (note == null || note.hidden || note.dissolving || !note.gameObject.activeInHierarchy) { Hide(); return; }
                    age += delta;
                    for (int i = 0; i < 5; i++)
                    {
                        float phase = age * 1.6f + i * 1.4f;
                        parts[i].localPosition = origins[i] + Vector3.up * (.005f * Mathf.Sin(phase));
                        parts[i].localScale = Vector3.one * sizes[i] * (1 + .035f * Mathf.Sin(phase));
                        parts[i].localRotation = Quaternion.Euler(0, 0, 5 * Mathf.Sin(phase * .7f));
                    }
                }
            }
            catch (Exception e)
            {
                enabled = false;
                gameObject.SetActive(false);
                Plugin.Log.Warn("Laki Notes visual stopped: " + e.Message);
            }
        }
        private void SetAlpha(float alpha)
        {
            block.SetColor(ColorId, new Color(1, 1, 1, alpha));
            for (int i = 0; i < renderers.Length; i++) renderers[i].SetPropertyBlock(block);
        }
    }
}
