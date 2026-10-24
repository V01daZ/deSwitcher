using System;
using System.IO;
using System.Linq;
using DeSwitcher.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeSwitcher.Runtime
{
    public sealed class DangerWindow : MonoBehaviour
    {
        private const float LockSeconds = 5f;
        private DangerEntry current;
        private float shownAt;
        private bool trustStage;
        private bool ru;
        private bool stylesReady;
        private GUIStyle titleStyle;
        private GUIStyle dimStyle;
        private GUIStyle wrapStyle;
        private GUIStyle siteStyle;

        private void EnsureStyles()
        {
            if (stylesReady)
                return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.54f, 0.6f) } };
            dimStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.55f, 0.54f, 0.51f) } };
            wrapStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(0.81f, 0.79f, 0.76f) } };
            siteStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.24f, 0.88f, 1f) } };
            stylesReady = true;
        }

        private void Awake()
        {
            ru = Application.systemLanguage == SystemLanguage.Russian;
            DontDestroyOnLoad(gameObject);
        }

        private float nextMenuCheck;
        private Type menuType;

        private void Update()
        {
            if (current == null)
            {
                current = DangerQueue.Take();
                if (current != null)
                {
                    shownAt = Time.realtimeSinceStartup;
                    trustStage = false;
                }
            }

            if (Time.unscaledTime >= nextMenuCheck)
            {
                nextMenuCheck = Time.unscaledTime + 0.5f;
                TryAttachCredit();
            }
        }

        private void TryAttachCredit()
        {
            try
            {
                if (SceneManager.GetActiveScene().name != "Menu")
                    return;
                if (FindObjectOfType<DeswitchCredit>() != null)
                    return;
                if (menuType == null)
                    menuType = AccessTools.TypeByName("MenuController");
                var menu = menuType != null ? FindObjectOfType(menuType) : null;
                if (menu != null)
                    DeswitchCredit.Attach(menu);
            }
            catch
            {
            }
        }

        private void OnGUI()
        {
            if (current == null)
                return;
            EnsureStyles();

            GUI.depth = -1000;
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float w = 560, h = 360;
            var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.color = new Color(0.06f, 0.06f, 0.07f, 1f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float elapsed = Time.realtimeSinceStartup - shownAt;
            bool locked = elapsed < LockSeconds;

            GUI.color = new Color(1f, 0.24f, 0.35f, 1f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width * Mathf.Clamp01(elapsed / LockSeconds), 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(box.x + 26, box.y + 18, box.width - 52, box.height - 36));

            GUILayout.Label(ru ? "ОПАСНЫЙ МОД" : "DANGEROUS MOD", titleStyle);
            GUILayout.Label(current.Name + "  -  Mods/" + Path.GetFileName(current.Folder), dimStyle);
            GUILayout.Space(10);
            GUILayout.Label(trustStage
                ? (ru ? "Если вы ошибётесь, вирус из этого мода получит доступ ко всему, к чему есть доступ у вас: файлам, паролям, аккаунтам. Делайте это, только если доверяете автору." : "If you are wrong, a virus in this mod gets access to everything you can: files, passwords, accounts. Only do this if you trust the author.")
                : (ru ? "Этот мод пытается делать то, что модам не нужно. Мы его не загрузили. Так часто выглядят вирусы, которые крадут аккаунты и пароли." : "This mod tries to do things a mod never needs. We did not load it. Viruses that steal accounts and passwords often look like this."),
                wrapStyle);

            if (!trustStage)
            {
                GUILayout.Space(8);
                foreach (var f in current.Verdict.Findings.Where(x => x.Severity == Severity.Dangerous).Select(x => x.RuleId).Distinct().Take(4))
                    GUILayout.Label("-> " + FindingText(f), dimStyle);
            }

            GUILayout.Space(10);
            GUILayout.Label("deswitchstudio.com", siteStyle);

            GUILayout.FlexibleSpace();
            GUI.enabled = !locked;
            GUILayout.BeginHorizontal();
            if (!trustStage)
            {
                if (GUILayout.Button(ru ? "В карантин" : "Quarantine", GUILayout.Height(34)))
                    Finish(() => Quarantine());
                if (GUILayout.Button(ru ? "Уберу сам" : "I'll remove it", GUILayout.Height(34)))
                    Finish(null);
                if (current.Verdict.CanTrust && GUILayout.Button(ru ? "Доверяю..." : "Trust anyway...", GUILayout.Height(34)))
                {
                    trustStage = true;
                    shownAt = Time.realtimeSinceStartup;
                }
            }
            else
            {
                if (GUILayout.Button(ru ? "Всё равно доверяю" : "Trust it anyway", GUILayout.Height(34)))
                    Finish(() => Guard.Trust(current.Verdict.Hash));
                if (GUILayout.Button(ru ? "Отмена" : "Cancel", GUILayout.Height(34)))
                    trustStage = false;
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void Finish(Action act)
        {
            try
            {
                act?.Invoke();
            }
            catch (Exception e)
            {
                Log.Error("Danger action failed: " + e);
            }
            current = null;
        }

        private void Quarantine()
        {
            string root = Path.Combine(Bootstrap.HomeDir, "Quarantine");
            Directory.CreateDirectory(root);
            string dest = Path.Combine(root, Path.GetFileName(current.Folder));
            int n = 2;
            while (Directory.Exists(dest))
                dest = Path.Combine(root, Path.GetFileName(current.Folder) + " (" + n++ + ")");
            Directory.Move(current.Folder, dest);
        }

        private string FindingText(string rule)
        {
            switch (rule)
            {
                case "net": return ru ? "выход в интернет" : "connects to the internet";
                case "process": return ru ? "запуск программ" : "launches programs";
                case "fs-outside": return ru ? "доступ к файлам вне игры" : "reads files outside the game";
                case "fs-write": return ru ? "запись в файлы игры" : "writes into game files";
                case "registry": return ru ? "доступ к реестру" : "touches the registry";
                case "pinvoke": return ru ? "вызов системных библиотек" : "calls system libraries";
                case "asm-load": return ru ? "загрузка другого кода" : "loads other code";
                case "reflection-dangerous": return ru ? "скрытые вызовы" : "hidden calls";
                case "tamper": return ru ? "попытка отключить защиту" : "tries to disable protection";
                case "codegen": return ru ? "создание кода на лету" : "generates code at runtime";
                case "serialization": return ru ? "опасная распаковка данных" : "unsafe data unpacking";
                default: return rule;
            }
        }
    }
}
