using System;
using System.IO;
using CardBattler.Combat;
using UnityEditor;
using UnityEngine;

public static class SimulatorMenu
{
    [MenuItem("Card Battler/Simulador")]
    static void Run()
    {
        try
        {
            var dir = Simulator.RunAndSave(Content.Load(), new SimConfig(), Simulator.DefaultRoot,
                (msg, p) => EditorUtility.DisplayProgressBar("Simulador de balanceamento", msg, p));
            Debug.Log("Simulação salva em " + dir);
            EditorUtility.RevealInFinder(Path.Combine(dir, "report.md"));
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // Batch mode: unity run <projeto> -- -executeMethod SimulatorMenu.RunBatch -simMatches 500 -simSeed 1 -simMatchups arquivo.json -simJsonl
    public static void RunBatch()
    {
        var args = Environment.GetCommandLineArgs();
        string Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        int.TryParse(Arg("-simMatches"), out int matches);
        int seed = int.TryParse(Arg("-simSeed"), out int s) ? s : 1;
        var dir = Simulator.RunFromArgs(matches, seed, Arg("-simMatchups"), Array.IndexOf(args, "-simJsonl") >= 0);
        Debug.Log("Simulação salva em " + dir);
    }
}
