using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OfficeHell.EditorTools
{
    /// <summary>
    /// Mechanises the layering rules instead of trusting discipline.
    /// Systems and Model must never read engine time and must never write Time.timeScale, because
    /// the moment one of them does, hitstop and pause stop being controllable from one place.
    /// </summary>
    public static class OfficeHellDisciplineCheck
    {
        static readonly string[] GuardedFolders =
        {
            "Assets/_Game/Scripts/Systems",
            "Assets/_Game/Scripts/Model",
        };

        static readonly string[] Banned =
        {
            "Time.deltaTime",
            "Time.timeScale",
            "Time.fixedDeltaTime",
            "Time.unscaledDeltaTime",
            "Time.time",
        };

        public static void RunBatch()
        {
            EditorApplication.Exit(Collect().Count == 0 ? 0 : 1);
        }

        [MenuItem("Office Hell/Run Discipline Check", false, 20)]
        public static void Run()
        {
            Collect();
        }

        static List<string> Collect()
        {
            List<string> violations = new List<string>();

            for (int f = 0; f < GuardedFolders.Length; f++)
            {
                if (!Directory.Exists(GuardedFolders[f]))
                {
                    continue;
                }

                string[] files = Directory.GetFiles(GuardedFolders[f], "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string[] lines = File.ReadAllLines(files[i]);
                    for (int l = 0; l < lines.Length; l++)
                    {
                        string code = StripComment(lines[l]);
                        for (int b = 0; b < Banned.Length; b++)
                        {
                            if (ContainsToken(code, Banned[b]))
                            {
                                violations.Add(files[i] + ":" + (l + 1) + "  " + Banned[b]);
                            }
                        }
                    }
                }
            }

            if (violations.Count == 0)
            {
                Debug.Log("[OfficeHell] discipline check passed: Systems and Model contain no engine time access.");
                return violations;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("[OfficeHell] discipline check failed, ").Append(violations.Count).Append(" violation(s):");
            for (int i = 0; i < violations.Count; i++)
            {
                sb.Append("\n  ").Append(violations[i]);
            }

            Debug.LogError(sb.ToString());
            return violations;
        }

        /// <summary>Documentation is allowed to name the banned api, only real calls are violations.</summary>
        static string StripComment(string line)
        {
            int idx = line.IndexOf("//", System.StringComparison.Ordinal);
            if (idx >= 0)
            {
                line = line.Substring(0, idx);
            }

            idx = line.IndexOf("/*", System.StringComparison.Ordinal);
            if (idx >= 0)
            {
                line = line.Substring(0, idx);
            }

            return line;
        }

        /// <summary>"Time.time" must not report on "Time.timeScale" twice.</summary>
        static bool ContainsToken(string code, string token)
        {
            int from = 0;
            while (true)
            {
                int idx = code.IndexOf(token, from, System.StringComparison.Ordinal);
                if (idx < 0)
                {
                    return false;
                }

                int after = idx + token.Length;
                bool boundedAfter = after >= code.Length || !char.IsLetterOrDigit(code[after]);
                if (boundedAfter)
                {
                    return true;
                }

                from = after;
            }
        }
    }
}
