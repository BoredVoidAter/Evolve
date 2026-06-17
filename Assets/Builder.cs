using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.IO;

public class Builder {
    public static void Build() {
        string[] args = Environment.GetCommandLineArgs();
        string buildPath = "Builds";
        
        // Define your game's executable name here
        string gameName = "game"; 

        // Parse CLI arguments
        bool buildWebGL = args.Contains("-buildWebGL");
        bool buildWin = args.Contains("-buildWin");
        bool buildMac = args.Contains("-buildMac");
        bool buildLinux = args.Contains("-buildLinux");

        // Get enabled scenes from Build Settings
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        // Build requested platforms
        if (buildWebGL) {
            // WebGL wants just a folder path
            BuildPipeline.BuildPlayer(scenes, Path.Combine(buildPath, "WebGL"), BuildTarget.WebGL, BuildOptions.None);
        }
        if (buildWin) {
            // Windows wants the .exe path
            BuildPipeline.BuildPlayer(scenes, Path.Combine(buildPath, "Windows", gameName + ".exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
        }
        if (buildMac) {
            // Mac wants the .app path
            BuildPipeline.BuildPlayer(scenes, Path.Combine(buildPath, "MacOS", gameName + ".app"), BuildTarget.StandaloneOSX, BuildOptions.None);
        }
        if (buildLinux) {
            // Linux wants the .x86_64 path
            BuildPipeline.BuildPlayer(scenes, Path.Combine(buildPath, "Linux", gameName + ".x86_64"), BuildTarget.StandaloneLinux64, BuildOptions.None);
        }
    }
}
