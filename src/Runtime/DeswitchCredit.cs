using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeSwitcher.Runtime
{
    public sealed class DeswitchCredit : MonoBehaviour
    {
        private object menu;
        private GameObject[] pages;
        private GameObject label;
        private TMP_Text title;
        private RectTransform tapeRoot;
        private bool tapesBuilt;

        public static void Attach(object menu)
        {
            try
            {
                if (SceneManager.GetActiveScene().name != "Menu")
                    return;
                var pages = Traverse.Create(menu).Field("pages").GetValue() as GameObject[];
                if (pages == null || pages.Length == 0)
                    return;
                var canvas = pages[0].GetComponentInParent<Canvas>();
                if (canvas == null)
                    return;
                canvas = canvas.rootCanvas;

                var labelGo = new GameObject("DeswitchCredit", typeof(RectTransform));
                labelGo.transform.SetParent(canvas.transform, false);
                labelGo.transform.SetAsLastSibling();
                var text = labelGo.AddComponent<TextMeshProUGUI>();
                var sample = pages[0].GetComponentInChildren<TextMeshProUGUI>(true);
                if (sample != null)
                {
                    text.font = sample.font;
                    text.fontSharedMaterial = sample.fontSharedMaterial;
                    text.fontSize = sample.fontSize * 0.4f;
                }
                text.text = "by deswitchstudio.com";
                text.color = new Color(1f, 0.8f, 0f);
                text.alignment = TextAlignmentOptions.BottomRight;
                text.enableWordWrapping = false;
                text.raycastTarget = false;
                var rt = (RectTransform)labelGo.transform;
                rt.pivot = rt.anchorMax = rt.anchorMin = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-30f, 24f);
                rt.sizeDelta = new Vector2(800f, 80f);

                var credit = canvas.gameObject.AddComponent<DeswitchCredit>();
                credit.menu = menu;
                credit.pages = pages;
                credit.label = labelGo;
                foreach (var t in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (t.text != null && t.text.ToLowerInvariant().Contains("people playground"))
                    {
                        credit.title = t;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("DeswitchCredit: " + e);
            }
        }

        private void Update()
        {
            if (menu == null || label == null)
                return;
            object currentPage = Traverse.Create(menu).Property("CurrentPage").GetValue();
            bool onFirst = ReferenceEquals(currentPage, pages[0]);
            if (label.activeSelf != onFirst)
                label.SetActive(onFirst);
        }

        private void LateUpdate()
        {
            if (title == null)
                return;
            try
            {
                if (!tapesBuilt)
                {
                    tapesBuilt = true;
                    BuildTapes();
                }
                if (tapeRoot != null)
                {
                    var tr = (RectTransform)title.transform;
                    tapeRoot.anchorMin = tr.anchorMin;
                    tapeRoot.anchorMax = tr.anchorMax;
                    tapeRoot.pivot = tr.pivot;
                    tapeRoot.anchoredPosition3D = tr.anchoredPosition3D;
                    tapeRoot.sizeDelta = tr.sizeDelta;
                    tapeRoot.localRotation = tr.localRotation;
                    tapeRoot.localScale = tr.localScale;
                    bool active = title.gameObject.activeInHierarchy;
                    if (tapeRoot.gameObject.activeSelf != active)
                        tapeRoot.gameObject.SetActive(active);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("DeswitchCredit tape: " + e);
                title = null;
            }
        }

        private void BuildTapes()
        {
            title.ForceMeshUpdate(false, false);
            Bounds b = title.textBounds;
            if (b.size.x <= 0f || b.size.y <= 0f)
            {
                tapesBuilt = false;
                return;
            }
            var go = new GameObject("CautionTape", typeof(RectTransform));
            tapeRoot = (RectTransform)go.transform;
            tapeRoot.SetParent(title.transform.parent, false);
            tapeRoot.SetSiblingIndex(title.transform.GetSiblingIndex());
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            float height = b.size.y * 1.45f;
            float width = b.size.x + height * 2.5f;
            AddTape(new Vector2(b.center.x + height * 0.6f, b.center.y), width, height);
            title.color = new Color(0.07f, 0.07f, 0.07f, 1f);
        }

        private void AddTape(Vector2 center, float width, float height)
        {
            var go = new GameObject("Tape", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(tapeRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var titleRt = (RectTransform)title.transform;
            Vector2 off = (new Vector2(0.5f, 0.5f) - titleRt.pivot) * titleRt.rect.size;
            rt.anchoredPosition = center - off;
            rt.sizeDelta = new Vector2(width, height);
            var img = go.AddComponent<RawImage>();
            img.texture = MakeTape(Mathf.RoundToInt(128f * width / height), 128);
            img.raycastTarget = false;
        }

        private static Texture2D MakeTape(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color dark = new Color(0.08f, 0.08f, 0.08f, 1f);
            Color yellow = new Color(1f, 0.8f, 0f, 1f);
            Color darkStripe = new Color(0.1f, 0.1f, 0.1f, 1f);
            Color light = new Color(1f, 0.93f, 0.62f, 1f);
            int edge = 3;
            int band = Mathf.RoundToInt((h - edge * 2) * 0.16f);
            int stripe = Mathf.Max(1, band / 2);
            var rnd = new System.Random(1118200);
            float[] tear = new float[h];
            int step = 5;
            float prev = 6f + (float)rnd.NextDouble() * 14f;
            for (int i = 0; i < h; i += step)
            {
                float next = 6f + (float)rnd.NextDouble() * 14f;
                if (rnd.NextDouble() < 0.12)
                    next += 10f + (float)rnd.NextDouble() * 14f;
                for (int j = 0; j < step && i + j < h; j++)
                    tear[i + j] = Mathf.Lerp(prev, next, (float)j / step);
                prev = next;
            }
            var px = new Color[w * h];
            for (int k = 0; k < h; k++)
            {
                for (int l = 0; l < w; l++)
                {
                    Color c;
                    if (k >= edge && k < h - edge)
                        c = (k >= edge + band && k < h - edge - band) ? yellow : (((l + k) / stripe % 2 == 0) ? yellow : darkStripe);
                    else
                        c = dark;
                    float d = l - tear[k];
                    if (d < 3f && d >= 0f)
                        c = Color.Lerp(light, c, d / 3f);
                    c.a = Mathf.Clamp01(d + 1f);
                    px[k * w + l] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
