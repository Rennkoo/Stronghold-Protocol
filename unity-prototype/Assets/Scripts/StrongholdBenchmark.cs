using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;

[Serializable] public class BenchmarkModel { public string id, path, idle, attack; public bool pma; }
[Serializable] public class BenchmarkModels { public BenchmarkModel[] models; }
[Serializable] public class BenchmarkResult {
    public string unity, device, gpu, utc, scene = "120 skeletons; synthetic board; no combat simulation";
    public int units, uniqueModels, width, height, samples;
    public double fps, p50Ms, p95Ms, p99Ms;
}

public class StrongholdBenchmark : MonoBehaviour {
    readonly List<SkeletonAnimation> actors = new List<SkeletonAnimation>();
    readonly List<float> frames = new List<float>(3600);
    readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    float elapsed, attackClock;
    int modelCount, failed;
    bool saved;
    Camera renderCamera;
    string status = "Warming up (30 seconds)", output;
    public int unitCount = 120;

    void Start() {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Application.runInBackground = true;
        var camera = new GameObject("Benchmark Camera").AddComponent<Camera>();
        renderCamera = camera;
        camera.orthographic = true; camera.orthographicSize = 5.4f;
        camera.transform.position = new Vector3(9.5f, 3.1f, -20);
        camera.backgroundColor = new Color(.055f,.065f,.085f);
        CreateBoard();
        var manifest = Resources.Load<TextAsset>("models");
        if (!manifest) throw new Exception("Missing model manifest: run prepare-unity.mjs");
        var entries = JsonUtility.FromJson<BenchmarkModels>(manifest.text).models;
        if (entries == null || entries.Length == 0) throw new Exception("No real skeleton assets available");
        var loaded = new Dictionary<string, SkeletonDataAsset>();
        for (int i=0; i<unitCount; i++) {
            var entry = entries[i % entries.Length];
            try {
                if (!loaded.TryGetValue(entry.id, out var data)) {
                    var atlasText = Resources.Load<TextAsset>(entry.path + "/atlas");
                    var skeleton = Resources.Load<TextAsset>(entry.path + "/skeleton");
                    if (skeleton) skeleton.name = "skeleton.skel";
                    var textures = Resources.LoadAll<Texture2D>(entry.path);
                    var shader = Shader.Find("Spine/Skeleton");
                    if (!shader || !atlasText || !skeleton || textures.Length == 0)
                        throw new Exception("Incomplete model " + entry.id);
                    var material = new Material(shader);
                    if (!entry.pma) material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                    owned.Add(material);
                    var atlas = SpineAtlasAsset.CreateRuntimeInstance(atlasText, textures, material, true);
                    owned.Add(atlas);
                    data = SkeletonDataAsset.CreateRuntimeInstance(skeleton, atlas, true, .004f);
                    if (data.GetSkeletonData(true) == null) throw new Exception("Skeleton decode failed: " + entry.id);
                    owned.Add(data); loaded.Add(entry.id, data);
                }
                var actor = SkeletonAnimation.NewSkeletonAnimationGameObject(data);
                actor.name = "Unit " + i + " " + entry.id;
                actor.transform.position = new Vector3((i%20), (5-i/20)*1.05f, 0);
                actor.GetComponent<MeshRenderer>().sortingOrder = i;
                actor.AnimationState.SetAnimation(0, entry.idle, true).TrackTime = i*.173f;
                actors.Add(actor);
            } catch (Exception e) { failed++; Debug.LogException(e); }
        }
        modelCount = loaded.Count;
        if (failed > 0) status = "INVALID BENCHMARK: failed models " + failed;
    }
    void CreateBoard() {
        var texture = new Texture2D(1,1); texture.SetPixel(0,0,Color.white); texture.Apply(); owned.Add(texture);
        var sprite = Sprite.Create(texture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1); owned.Add(sprite);
        for(int y=0;y<6;y++) for(int x=0;x<20;x++) {
            var tile = new GameObject("Tile").AddComponent<SpriteRenderer>();
            tile.sprite=sprite; tile.sortingOrder=-100;
            tile.color=(x+y)%2==0 ? new Color(.18f,.22f,.27f) : new Color(.12f,.16f,.20f);
            tile.transform.position=new Vector3(x,y*1.05f,1);
            tile.transform.localScale=new Vector3(.97f,1.02f,1);
        }
    }
    void Update() {
        float dt=Time.unscaledDeltaTime; elapsed+=dt; attackClock+=dt;
        if(attackClock>=2) {
            attackClock=0;
            // Keep a repeatable mixture of idle and attack animations without a simulation or RNG.
            foreach(var actor in actors) {
                var attack=actor.Skeleton.Data.FindAnimation("Attack");
                if(attack != null) actor.AnimationState.SetAnimation(0,attack,true);
            }
        }
        if(elapsed>=30 && !saved && failed==0) {
            frames.Add(dt*1000);
            status="Recording " + frames.Count + "/3600 frames";
            if(frames.Count>=3600) SaveResult();
        }
    }
    void SaveResult() {
        saved=true;
        double total=0; foreach(float v in frames) total+=v;
        frames.Sort();
        var result=new BenchmarkResult {
            unity=Application.unityVersion,device=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,
            utc=DateTime.UtcNow.ToString("O"),units=actors.Count,uniqueModels=modelCount,
            width=Screen.width,height=Screen.height,samples=frames.Count,
            fps=frames.Count*1000/total,p50Ms=Percentile(.50),p95Ms=Percentile(.95),p99Ms=Percentile(.99)
        };
        output=Path.Combine(Application.persistentDataPath,"benchmark-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json");
        File.WriteAllText(output,JsonUtility.ToJson(result,true));
        status=string.Format("Complete: {0:F1} FPS; p95 {1:F2} ms",result.fps,result.p95Ms);
        Debug.Log(status+" Result: "+output);
        // Visual verification happens after measurement, so readback does not contaminate frame samples.
        var target = new RenderTexture(Screen.width, Screen.height, 24);
        var previous = RenderTexture.active;
        renderCamera.targetTexture = target;
        renderCamera.Render();
        RenderTexture.active = target;
        var capture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);
        capture.Apply();
        File.WriteAllBytes(Path.ChangeExtension(output,".png"),capture.EncodeToPNG());
        renderCamera.targetTexture = null;
        RenderTexture.active = previous;
        Destroy(capture); target.Release(); Destroy(target);
    }
    float Percentile(double q) {return frames[Math.Min(frames.Count-1,(int)Math.Ceiling(frames.Count*q)-1)];}
    void OnGUI() {
        GUI.Box(new Rect(12,12,680,95),"Unity rendering prototype (not the full game)");
        GUI.Label(new Rect(25,38,650,25),"Units: "+actors.Count+"; models: "+modelCount+"; failures: "+failed+" | "+status);
        GUI.Label(new Rect(25,65,650,30),output ?? "30s warmup + 3600 frames. Compare Windows build, not Editor FPS.");
    }
    void OnDestroy() { foreach(var o in owned) if(o) Destroy(o); }
}
