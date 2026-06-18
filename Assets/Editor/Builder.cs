using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.IO;

public class Builder {
    public static void Build() {
        try {
            string[] args = Environment.GetCommandLineArgs();
            string buildPath = "Builds";
            string gameName = "game";

            bool buildWebGL = args.Contains("-buildWebGL");
            bool buildWin = args.Contains("-buildWin");
            bool buildMac = args.Contains("-buildMac");
            bool buildLinux = args.Contains("-buildLinux");

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

            if (buildWebGL)
                BuildPipeline.BuildPlayer(scenes, Path.Combine(buildPath, "WebGL"), BuildTarget.WebGL, BuildOptions.None);
            if (buildWin)
                BuildPipeline.BuildPlayer(scenes, Path.Combine(buildPath, "Windows", gameName + ".exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (buildMac)
                BuildPipeline.BuildPlayer(scenes, Path.Combine(buildPath, "MacOS", gameName + ".app"), BuildTarget.StandaloneOSX, BuildOptions.None);
            if (buildLinux)
                BuildPipeline.BuildPlayer(scenes, Path.Combine(buildPath, "Linux", gameName + ".x86_64"), BuildTarget.StandaloneLinux64, BuildOptions.None);

            EditorApplication.Exit(0);
        } catch (Exception e) {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
