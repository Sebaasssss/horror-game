using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Batch-mode smoke test: drives every screen and a full CPU-vs-CPU match in edit mode, saving PNG captures
// (raw 3x and through the CRT shader) to Tools/out/captures plus a text log of match events.
//   Unity -batchmode -projectPath . -executeMethod RetroCapture.Run -logFile -
public static class RetroCapture
{
    private static string outDir;
    private static RetroDisplay display;
    private static readonly StringBuilder log = new StringBuilder();

    [MenuItem("Tools/Retro/Capture Smoke Test")]
    public static void RunFromMenu()
    {
        Capture(false);
    }

    public static void Run()
    {
        int code = 0;
        try
        {
            Capture(true);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            code = 1;
        }
        EditorApplication.Exit(code);
    }

    private static void Capture(bool batch)
    {
        outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "out", "captures"));
        Directory.CreateDirectory(outDir);
        foreach (string file in Directory.GetFiles(outDir, "*.png"))
            File.Delete(file);
        log.Clear();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = new GameObject("Capture Root");
        display = new RetroDisplay(root.transform, false);
        AudioManager audio = new AudioManager(root.transform);

        ScriptedInput p1 = new ScriptedInput(t => default);
        ScriptedInput p2 = new ScriptedInput(t => default);
        GameFlow flow = new GameFlow(root.transform, display, audio, p1, p2);

        flow.Go(new TitleScreen(), true);
        Step(flow, 40);
        Save("01_title");
        Save("01_title");
        flow.Go(new ModeScreen(), true);
        Step(flow, 20);
        Save("02_mode");

        flow.mode = GameMode.Versus;
        flow.Go(new SelectScreen(), true);
        Step(flow, 20);
        Save("03_select");

        flow.Go(new StageSelectScreen(), true);
        Step(flow, 20);
        Save("03b_stage_select");

        flow.pick1 = Roster.Find("ghost");
        flow.pick2 = Roster.Find("jason");
        flow.Go(new VersusScreen(), true);
        Step(flow, 40);
        Save("04_versus");
        flow.Go(new TitleScreen(), true);
        Step(flow, 40);
        Save("06_title_again");
        flow.Go(new BlankScreen(), true);
        Step(flow, 14);

        // Full CPU-vs-CPU match driven directly, capturing key moments.
        Transform matchRoot = Nodes.Group(root.transform, "Match World");
        CpuInput cpu1 = new CpuInput(0.9f, 11);
        CpuInput cpu2 = new CpuInput(0.9f, 23);
        Match match = new Match(matchRoot, display, audio, flow.pick1, flow.pick2, cpu1, cpu2, "utez");
        MatchPhase lastPhase = match.phase;
        int hits = 0, lastHealth = match.p1.health + match.p2.health;
        int shots = 0;
        int lastDismember = 0;
        for (int t = 0; t < 60 * 60 * 6 && !match.Finished; t++)
        {
            match.Tick();
            int health = match.p1.health + match.p2.health;
            if (health < lastHealth)
            {
                hits++;
                if (hits <= 6 || hits % 10 == 0)
                {
                    Step(null, 0);
                    Save("10_hit_" + hits.ToString("000"));
                }
            }
            lastHealth = health;
            if (match.dismemberments != lastDismember)
            {
                lastDismember = match.dismemberments;
                Log($"t={t} dismember #{lastDismember}: P1 {match.p1.kit.Name} P2 {match.p2.kit.Name}");
                for (int k = 0; k < 24; k++)
                    match.Tick();
                Save("15_dismember_" + lastDismember);
                for (int k = 0; k < 60; k++)
                    match.Tick();
                Save("16_mutilated_" + lastDismember);
            }

            if (t == 20 || t == 90)
                Save("05_intro_" + t);
            if (t > 100 && t % 240 == 0 && shots < 12)
            {
                shots++;
                Save("20_fight_" + t.ToString("00000"));
            }

            if (match.phase != lastPhase)
            {
                Log($"t={t} phase {lastPhase} -> {match.phase} round={match.round} P1 {match.p1.health} ({match.p1.roundWins}) P2 {match.p2.health} ({match.p2.roundWins})");
                lastPhase = match.phase;
                Save("30_phase_" + t.ToString("00000") + "_" + match.phase);
                if (match.phase == MatchPhase.Fatality)
                {
                    for (int k = 0; k < 220; k += 1)
                    {
                        match.Tick();
                        if (k == 8 || k == 30 || k == 90 || k == 160 || k == 210)
                            Save("40_fatality_" + k.ToString("000"));
                    }
                }
            }
        }
        Log($"match finished={match.Finished} winner={(match.winner != null ? match.winner.def.Id : "none")} fatality={match.fatality} kind={match.fatalityKind} dismemberments={match.dismemberments} hits={hits}");
        Fighter winner = match.winner ?? match.p1;
        match.Dispose();
        flow.Go(new ResultsScreen(winner.def, winner.index, match.fatality), true);
        Step(flow, 30);
        Save("50_results");
        File.WriteAllText(Path.Combine(outDir, "log.txt"), log.ToString());
        Debug.Log("[RetroCapture] " + log);
        display.Dispose();
        Object.DestroyImmediate(root);
    }

    private static void Step(GameFlow flow, int ticks)
    {
        if (flow == null)
            return;
        for (int i = 0; i < ticks; i++)
            flow.Tick();
    }

    private static void Log(string line)
    {
        log.AppendLine(line);
    }

    private static void Save(string name)
    {
        Render(display.worldCamera);
        RenderTexture previous = RenderTexture.active;

        RenderTexture.active = display.target;
        Texture2D raw = new Texture2D(display.target.width, display.target.height, TextureFormat.RGBA32, false);
        raw.ReadPixels(new Rect(0, 0, raw.width, raw.height), 0, 0);
        raw.Apply();
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), Upscale(raw, 3).EncodeToPNG());

        if (display.CrtMaterial != null)
        {
            RenderTexture crt = RenderTexture.GetTemporary(1152, 648, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(display.target, crt, display.CrtMaterial);
            RenderTexture.active = crt;
            Texture2D crtTex = new Texture2D(crt.width, crt.height, TextureFormat.RGBA32, false);
            crtTex.ReadPixels(new Rect(0, 0, crt.width, crt.height), 0, 0);
            crtTex.Apply();
            File.WriteAllBytes(Path.Combine(outDir, name + "_crt.png"), crtTex.EncodeToPNG());
            RenderTexture.ReleaseTemporary(crt);
            Object.DestroyImmediate(crtTex);
        }
        RenderTexture.active = previous;
        Object.DestroyImmediate(raw);
    }

    private static void Render(Camera camera)
    {
        UniversalRequest(camera);
    }

    private static void UniversalRequest(Camera camera)
    {
        var request = new RenderPipeline.StandardRequest { destination = camera.targetTexture };
        if (RenderPipeline.SupportsRenderRequest(camera, request))
            RenderPipeline.SubmitRenderRequest(camera, request);
        else
            camera.Render();
    }

    private static Texture2D Upscale(Texture2D source, int factor)
    {
        Texture2D result = new Texture2D(source.width * factor, source.height * factor, TextureFormat.RGBA32, false);
        Color32[] src = source.GetPixels32();
        Color32[] dst = new Color32[result.width * result.height];
        for (int y = 0; y < result.height; y++)
            for (int x = 0; x < result.width; x++)
                dst[y * result.width + x] = src[(y / factor) * source.width + x / factor];
        result.SetPixels32(dst);
        result.Apply();
        return result;
    }
}
