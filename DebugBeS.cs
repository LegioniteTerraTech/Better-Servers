using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Better_Servers
{
    internal static class DebugBeS
    {
        internal static bool LogAll = false;
        internal static bool ShouldLog = true;
        internal static bool ShouldLogPathing = false;
        private static bool ShouldLogNet = true;
#if DEBUG
        private static bool LogDev = true;
#else
        private static bool LogDev = false;
#endif

        internal static void Info(string message)
        {
            if (!ShouldLog || !LogAll)
                return;
            UnityEngine.Debug.Log("BetterServers: " + message);
        }
        internal static void Log(string message)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log("BetterServers: " + message);
        }
        internal static void Log(Exception e)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log("BetterServers: " + e);
        }

        internal static void LogNet(string message)
        {
            if (!ShouldLogNet)
                return;
            UnityEngine.Debug.Log("BetterServers: " + message);
        }

        internal static void Assert(string message)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log("BetterServers: " + message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        internal static void Assert(bool shouldAssert, string message)
        {
            if (!ShouldLog || !shouldAssert)
                return;
            UnityEngine.Debug.Log("BetterServers: " + message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        internal static void LogError(string message)
        {
            if (!ShouldLog)
                return;
            UnityEngine.Debug.Log("BetterServers: " + message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        internal static void LogDevOnly(string message)
        {
            if (!LogDev)
                return;
            UnityEngine.Debug.Log("BetterServers: " + message + "\n" + StackTraceUtility.ExtractStackTrace().ToString());
        }
        internal static void Exception(string message)
        {
            throw new Exception("BetterServers: Exception - ", new Exception(message));
        }
        private static List<string> warning = new List<string>();
        private static bool postStartup = false;
        private static bool seriousError = false;
        internal static void ErrorReport(string Warning)
        {
            warning.Add(Warning);
            seriousError = true;
        }
        internal static void Warning(string Warning)
        {
            warning.Add(Warning);
        }
        internal static void DoShowWarnings()
        {
            if (warning.Any())
            {
                foreach (var item in warning)
                {
                    ManUI.inst.ShowErrorPopup("BetterServers: " + item);
                }
                warning.Clear();
                if (!postStartup && seriousError)
                {
                    if (TerraTechETCUtil.MassPatcher.CheckIfUnstable())
                        ManUI.inst.ShowErrorPopup("BetterServers: Error happened on Unstable Branch.  If the issue persists, switch back to Stable Branch.");
                    else
                        ManUI.inst.ShowErrorPopup("BetterServers: Error happened during startup!  BetterServers might not work correctly.");
                }
                seriousError = false;
            }
            postStartup = true;
        }
        internal static void FatalError()
        {
            ManUI.inst.ShowErrorPopup("BetterServers: ENCOUNTERED CRITICAL ERROR");
            UnityEngine.Debug.Log("BetterServers: ENCOUNTERED CRITICAL ERROR");
            UnityEngine.Debug.Log("BetterServers: MAY NOT WORK PROPERLY AFTER THIS ERROR, PLEASE REPORT!");
        }
        internal static void FatalError(string e)
        {
            ManUI.inst.ShowErrorPopup("BetterServers: ENCOUNTERED CRITICAL ERROR: " + e);
            UnityEngine.Debug.Log("BetterServers: ENCOUNTERED CRITICAL ERROR");
            UnityEngine.Debug.Log("BetterServers: MAY NOT WORK PROPERLY AFTER THIS ERROR, PLEASE REPORT!");
        }
    }
}
