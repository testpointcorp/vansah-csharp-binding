using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vansah;

namespace Vansah_CSharp_Binding
{
    /// <summary>
    /// THIS FILE IS ONLY FOR TESTING THE BINDER. It is not part of the shipped VansahNode library --
    /// it is a local, gitignored harness used to verify VansahNode against a live Vansah instance.
    ///
    /// It exercises every VansahNode function end-to-end. All configuration is read from a .env file in
    /// the working directory (see .env for the keys). Every test is independent and guarded: if a required
    /// value is blank the test is SKIPPED, not failed. No test data is deleted -- the destructive
    /// RemoveTestRun/RemoveTestLog methods are intentionally NOT exercised here so the runs/logs remain
    /// visible in Vansah for inspection.
    /// </summary>
    public class VansahNodeFullTests
    {
        private static Dictionary<string, string> env = new();
        private static int passed, failed, skipped;

        static void Main(string[] args)
        {
            env = LoadDotEnv(FindDotEnv());

            string token = Get("VANSAH_TOKEN");
            string url = Get("VANSAH_URL");
            string projectKey = Get("VANSAH_PROJECT_KEY");
            string folderPath = Get("VANSAH_FOLDER_PATH");
            string issueKey = Get("VANSAH_JIRA_ISSUE_KEY");
            string caseKey = Get("VANSAH_TEST_CASE_KEY");
            string atpKey = Get("VANSAH_ATP_KEY");
            string atpAssetType = Get("VANSAH_ATP_ASSET_TYPE", "folder");
            string stpKey = Get("VANSAH_STP_KEY");
            string sprint = Get("VANSAH_SPRINT_NAME");
            string release = Get("VANSAH_RELEASE_NAME");
            string environmentName = Get("VANSAH_ENVIRONMENT_NAME");
            bool debug = IsTrue(Get("VANSAH_DEBUG"));
            string screenshot = ResolveScreenshot(Get("VANSAH_SCREENSHOT_PATH"));

            Console.WriteLine("=========================================================");
            Console.WriteLine(" Vansah C# Binding -- full function test harness");
            Console.WriteLine($" URL         : {(string.IsNullOrEmpty(url) ? "(binding default)" : url)}");
            Console.WriteLine($" Project key : {projectKey}");
            Console.WriteLine($" Debug       : {debug}");
            Console.WriteLine("=========================================================");

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("VANSAH_TOKEN is blank -- all tests skipped. Fill in .env to run.");
                return;
            }

            // Pass "plans" as a CLI arg to run ONLY the Standard/Advanced Test Plan tests.
            bool plansOnly = Array.Exists(args, a => string.Equals(a, "plans", StringComparison.OrdinalIgnoreCase));
            if (plansOnly) Console.WriteLine(" Mode        : plans-only (STP + ATP)");

            // Factory: a freshly-configured node per test (state like run/log ids must not leak between tests).
            Func<VansahNode> node = () =>
            {
                var vs = new VansahNode();
                vs.SetVansahToken = token;
                if (!string.IsNullOrWhiteSpace(url)) vs.SetVansahURL = url;
                vs.SpaceKey = projectKey;
                vs.SprintName = sprint;
                vs.release_Name = release;
                vs.environment_Name = environmentName;
                vs.setDebug(debug);
                return vs;
            };

            if (!plansOnly)
            {
            // 1. Quick Test against a Jira issue (string result).
            Run("QuickTest / Jira issue (string result)", issueKey, caseKey, () =>
            {
                var vs = node();
                vs.JiraIssueKey = issueKey;
                vs.AddQuickTestFromJiraIssue(caseKey, "passed");
            });

            // 2. Quick Test against a Jira issue (int result).
            Run("QuickTest / Jira issue (int result)", issueKey, caseKey, () =>
            {
                var vs = node();
                vs.JiraIssueKey = issueKey;
                vs.AddQuickTestFromJiraIssue(caseKey, 2);
            });

            // 3. Quick Test against a Test Folder (folderPath asset).
            Run("QuickTest / Test Folder (folderPath)", folderPath, caseKey, () =>
            {
                var vs = node();
                vs.TestFolderID = folderPath;
                vs.AddQuickTestFromTestFolders(caseKey, "passed");
            });

            // 4. Step-by-step run against a Jira issue: start the run, then record each step (with an attachment
            //    on step 1), then correct step 2 with UpdateTestLog.
            Run("Step run / Jira issue (run + step logs + update)", issueKey, caseKey, () =>
            {
                var vs = node();
                vs.JiraIssueKey = issueKey;
                vs.AddTestRunFromJiraIssue(caseKey);
                vs.AddTestLog("passed", "Step #1 -- login page loads", 1, screenshot);
                vs.AddTestLog("failed", "Step #2 -- invalid credentials rejected", 2);
                vs.UpdateTestLog("passed", "Step #2 re-run -- now passing");
            });

            // 4b. Record steps OUT OF ORDER (3, then 1, then 2). Each must update its own step from the cache,
            //     with no /details GET in between.
            Run("Step run / out-of-order step logs", issueKey, caseKey, () =>
            {
                var vs = node();
                vs.JiraIssueKey = issueKey;
                vs.AddTestRunFromJiraIssue(caseKey);
                vs.AddTestLog("passed", "Step #3 logged first", 3);
                vs.AddTestLog("passed", "Step #1 logged second", 1);
                vs.AddTestLog("failed", "Step #2 logged third", 2);
            });

            // 5. Step-by-step run against a Test Folder.
            Run("Step run / Test Folder (run + step log)", folderPath, caseKey, () =>
            {
                var vs = node();
                vs.TestFolderID = folderPath;
                vs.AddTestRunFromTestFolder(caseKey);
                vs.AddTestLog("passed", "Step #1 from folder run", 1);
            });

            // 5b. Record a step, remove that log, then record the same step again. RemoveTestLog leaves an
            //     Untested placeholder for the step, so the second AddTestLog updates that fresh placeholder
            //     (never a deleted id) and lands on the correct step.
            Run("Step log remove then re-add", issueKey, caseKey, () =>
            {
                var vs = node();
                vs.JiraIssueKey = issueKey;
                vs.AddTestRunFromJiraIssue(caseKey);
                vs.AddTestLog("passed", "Step #1 -- first attempt", 1); // updates the pre-created log
                vs.RemoveTestLog();                                     // deletes step 1's log, recreates a placeholder
                vs.AddTestLog("failed", "Step #1 -- re-added after removal", 1); // updates the placeholder
            });
            } // !plansOnly

            // 6. Standard Test Plan (Java-style setters): set the plan key, then run + log steps.
            Run("Standard Test Plan run (setter API)", stpKey, caseKey, () =>
            {
                var vs = node();
                vs.setStandardTestPlanKey(stpKey);   // iteration defaults to 1
                vs.AddTestRunFromStandardTestPlan(caseKey);
                vs.AddTestLog("passed", "Step #1 in standard test plan", 1);
                vs.AddTestLog("passed", "Step #2 in standard test plan", 2);
            });

            // 7. Advanced Test Plan (Java-style setters): set the plan key + requirement, then run + log steps.
            Run("Advanced Test Plan run (setter API)", atpKey, caseKey, () =>
            {
                var vs = node();
                vs.setAdvancedTestPlanKey(atpKey);
                vs.setTestPlanIteration(9);          // out of range -> warning, keeps default 1
                if (string.Equals(atpAssetType, "issue", StringComparison.OrdinalIgnoreCase))
                    vs.JiraIssueKey = issueKey;
                else
                    vs.TestFolderID = folderPath;
                vs.AddTestRunFromAdvancedTestPlan(atpAssetType, caseKey);
                vs.AddTestLog("passed", "Step #1 in advanced test plan", 1);
                vs.AddTestLog("passed", "Step #2 in advanced test plan", 2);
            });

            Console.WriteLine("=========================================================");
            Console.WriteLine($" RESULT: {passed} passed, {failed} failed, {skipped} skipped");
            Console.WriteLine(" (RemoveTestRun / RemoveTestLog not run -- test data preserved)");
            Console.WriteLine("=========================================================");
            Environment.Exit(failed == 0 ? 0 : 1);
        }

        // Runs one test, skipping if any required env value is blank. Captures the binding's console output
        // and classifies PASS/FAIL from it (the binding prints "...Successfully..." / "created" on success,
        // or the API error message on failure).
        static void Run(string name, string requiredA, string requiredB, Action test)
        {
            Console.WriteLine();
            Console.WriteLine($"--- TEST: {name} ---");
            if (string.IsNullOrWhiteSpace(requiredA) || string.IsNullOrWhiteSpace(requiredB))
            {
                Console.WriteLine("SKIPPED (a required .env value is blank)");
                skipped++;
                return;
            }

            var original = Console.Out;
            var sw = new StringWriter();
            Console.SetOut(sw);
            try { test(); }
            catch (Exception ex) { sw.WriteLine("EXCEPTION: " + ex.Message); }
            finally { Console.SetOut(original); }

            string output = sw.ToString();
            Console.Write(output);

            // Known failure phrases the binding prints when a call is rejected. Any of these fails the test even
            // if another line in the same test reported success.
            string[] errorMarkers = {
                "EXCEPTION", "already exists", "No test log found", "went wrong", "not properly set",
                "Please Provide", "is Disabled", "cannot be located", "Failed to validate"
            };
            bool sawError = Array.Exists(errorMarkers, m => output.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0);
            bool sawSuccess = output.IndexOf("Successfully", StringComparison.OrdinalIgnoreCase) >= 0
                              || output.IndexOf("created", StringComparison.OrdinalIgnoreCase) >= 0;
            if (sawSuccess && !sawError)
            {
                Console.WriteLine("PASS");
                passed++;
            }
            else
            {
                Console.WriteLine("FAIL");
                failed++;
            }
        }

        // ---------- helpers ----------

        static string Get(string key, string fallback = "")
        {
            if (env.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) return v;
            return fallback;
        }

        static bool IsTrue(string v) =>
            string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1";

        static string FindDotEnv()
        {
            // Walk up from the working directory to find a .env (the build output runs from bin/.../).
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, ".env");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return ".env";
        }

        static Dictionary<string, string> LoadDotEnv(string path)
        {
            var map = new Dictionary<string, string>();
            if (!File.Exists(path))
            {
                Console.WriteLine($"WARNING: no .env found at {path}");
                return map;
            }
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq).Trim();
                var val = line.Substring(eq + 1).Trim();
                // Strip a trailing inline comment only when the value is unquoted.
                if (val.Length > 0 && val[0] != '"' && val[0] != '\'')
                {
                    int hash = val.IndexOf(" #");
                    if (hash >= 0) val = val.Substring(0, hash).Trim();
                }
                if (val.Length >= 2 && (val[0] == '"' || val[0] == '\'') && val[val.Length - 1] == val[0])
                    val = val.Substring(1, val.Length - 2);
                map[key] = val;
            }
            return map;
        }

        // Returns a usable screenshot path: the configured one if it exists, else a generated 1x1 PNG in temp.
        static string ResolveScreenshot(string configured)
        {
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
            try
            {
                string tmp = Path.Combine(Path.GetTempPath(), "vansah_test_screenshot.png");
                // 1x1 transparent PNG.
                byte[] png = Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
                File.WriteAllBytes(tmp, png);
                return tmp;
            }
            catch
            {
                return configured ?? "";
            }
        }
    }
}
