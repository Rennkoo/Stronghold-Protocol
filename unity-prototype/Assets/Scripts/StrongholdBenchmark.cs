using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;

[Serializable] public class BenchmarkModel { public string id, path, idle, move, attack, skill,die,deploy; public bool pma; }
[Serializable] public class BenchmarkModels { public BenchmarkModel[] models; }
[Serializable] public class ReplayCount {public string kind;public int count;}
[Serializable] public class ReplayResult {public int snapshots,units,models,failures;public ReplayCount[] events;public ReplayState[] states;public float[] positions;public int[] hp,visibleIds;}
[Serializable] public class BenchmarkResult {
    public string unity, device, gpu, utc, scene = "120 skeletons; synthetic board; no combat simulation";
    public int units, uniqueModels, width, height, samples;
    public double fps, p50Ms, p95Ms, p99Ms;
    public int effectsEmitted,effectsDropped,peakEffects,boardTriangles,focusedSamples;
}

public class StrongholdBenchmark : MonoBehaviour {
    readonly List<SkeletonAnimation> actors = new List<SkeletonAnimation>();
    readonly List<float> frames = new List<float>(3600);
    readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    float elapsed, attackClock;
    int modelCount, failed;
    bool saved,initialized;
    int sampleCount=3600;
    Camera renderCamera;
    StressWorkload workload;
    TerrainHeightMap terrainHeights;
    BenchmarkModel[] entries;
    StressEffects effects;
    int tick,boardTriangles,focusedSamples;
    float tickAccumulator,simulationTime;
    UnitAnimationDriver[] animationDrivers;
    MeshRenderer[] actorRenderers;
    ReplayPlayback replay;
    bool replayCaptured,replaySaved;
    int[] hp;
    string status = "Warming up (30 seconds)", output;
    public int unitCount = 120;

    void Start() {
        var args=Environment.GetCommandLineArgs();
        int sampleArg=Array.IndexOf(args,"-benchmark-samples");
        if(sampleArg>=0 && sampleArg+1<args.Length && int.TryParse(args[sampleArg+1],out int requested))sampleCount=Mathf.Clamp(requested,600,3600);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Application.runInBackground = true;
        var camera = new GameObject("Benchmark Camera").AddComponent<Camera>();
        renderCamera = camera;
        camera.orthographic = true; camera.orthographicSize = 5.4f;
        camera.transform.position = new Vector3(9.5f, 3.1f, -20);
        camera.backgroundColor = new Color(.055f,.065f,.085f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        bool replayMode=Array.IndexOf(args,"-replay")>=0;
        var stress = Array.IndexOf(args,"-simple")>=0 ? null : Resources.Load<TextAsset>(replayMode?"replay-workload":"stress");
        if(replayMode && !stress)throw new Exception("Run export-unity-stress.mjs --replay first");
        if(stress) workload = JsonUtility.FromJson<StressWorkload>(stress.text);
        if(workload!=null)terrainHeights=new TerrainHeightMap(workload.terrain);
        if(workload != null) CreateStressBoard(camera); else CreateBoard();
        var manifest = Resources.Load<TextAsset>(replayMode?"replay-models":workload != null ? "models" : "simple-models") ?? Resources.Load<TextAsset>("models");
        if (!manifest) throw new Exception("Missing model manifest: run prepare-unity.mjs");
        entries = JsonUtility.FromJson<BenchmarkModels>(manifest.text).models;
        if (entries == null || entries.Length == 0) throw new Exception("No real skeleton assets available");
        var loaded = new Dictionary<string, SkeletonDataAsset>();
        for (int i=0; i<unitCount; i++) {
            var unit = workload != null ? workload.units[i] : null;
            var entry = entries[unit != null ? unit.model : i % entries.Length];
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
                    data = SkeletonDataAsset.CreateRuntimeInstance(skeleton, atlas, true, workload != null ? workload.modelScale : .004f);
                    if (data.GetSkeletonData(true) == null) throw new Exception("Skeleton decode failed: " + entry.id);
                    owned.Add(data); loaded.Add(entry.id, data);
                }
                var actor = SkeletonAnimation.NewSkeletonAnimationGameObject(data);
                actor.name = "Unit " + i + " " + entry.id;
                if(unit != null) {
                    actor.transform.position = UnitPosition(unit, 0);
                    actor.transform.rotation = camera.transform.rotation;
                    actor.transform.localScale = new Vector3(unit.facing*unit.scale,unit.scale*unit.scaleY,1);
                } else actor.transform.position = new Vector3((i%20), (5-i/20)*1.05f, 0);
                actor.GetComponent<MeshRenderer>().sortingOrder = i;
                actor.AnimationState.Data.DefaultMix = .1f;
                actor.AnimationState.SetAnimation(0, unit != null && unit.enemy ? entry.move : entry.idle, true).TrackTime = i*.173f;
                actors.Add(actor);
            } catch (Exception e) { failed++; Debug.LogException(e); }
        }
        modelCount = loaded.Count;
        if (failed > 0) status = "INVALID BENCHMARK: failed models " + failed;
        Debug.Log("Models initialized: units="+actors.Count+" models="+modelCount+" failures="+failed);
        if(workload != null && failed == 0) {
            animationDrivers = new UnitAnimationDriver[actors.Count];
            actorRenderers = new MeshRenderer[actors.Count]; hp = new int[actors.Count];
            for(int i=0;i<actors.Count;i++) {
                animationDrivers[i]=new UnitAnimationDriver(actors[i],entries[workload.units[i].model],workload.units[i].enemy);
                actorRenderers[i]=actors[i].GetComponent<MeshRenderer>();
            }
            for(int i=0;i<hp.Length;i++) hp[i]=workload.units[i].maxHp;
            effects = new GameObject("Pooled projectiles and numbers").AddComponent<StressEffects>();
            effects.Initialize(camera);
            var transforms=new Transform[actors.Count];for(int i=0;i<actors.Count;i++)transforms[i]=actors[i].transform;
            effects.SetBars(transforms,hp,workload.units);
            if(replayMode){
                replay=new ReplayPlayback(workload,actors.ToArray(),animationDrivers,actorRenderers,entries,hp,effects,terrainHeights);
                status="Recorded battle playback (2x)";
            }
        }
        initialized=true;
    }
    Vector3 UnitPosition(StressUnit unit,float time) {
        float x=unit.x,y=unit.y;
        if(unit.enemy && workload.replayFrames==null) {x=1+((unit.x-time*.35f+40)%19);y+=Mathf.Sin(time+unit.id)*.25f;}
        x=Mathf.Round(x*100)/100;y=Mathf.Round(y*100)/100;
        float height=terrainHeights.Get(x,y);
        return StressWorkload.World(x,y,height+.025f);
    }
    void CreateStressBoard(Camera camera) {
        var c=workload.camera;
        camera.orthographic=false;camera.fieldOfView=c.fov;
        camera.nearClipPlane=c.near;camera.farClipPlane=c.far;
        camera.transform.position=StressWorkload.World(c.position);
        camera.transform.rotation=Quaternion.LookRotation(StressWorkload.World(c.target)-camera.transform.position,StressWorkload.World(c.up));
        unitCount=workload.units.Length;
        foreach(var bucket in workload.buckets) {
            int count=bucket.position.Length/3;
            var positions=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var colors=new Color[count];
            for(int i=0;i<count;i++) {
                positions[i]=StressWorkload.World(bucket.position[i*3],bucket.position[i*3+1],bucket.position[i*3+2]);
                normals[i]=StressWorkload.World(bucket.normal[i*3],bucket.normal[i*3+1],bucket.normal[i*3+2]);
                uv[i]=new Vector2(bucket.uv[i*2],bucket.uv[i*2+1]);
                colors[i]=new Color(bucket.color[i*3],bucket.color[i*3+1],bucket.color[i*3+2]);
            }
            // Swapping Y/Z changes handedness; reverse each triangle to preserve outward-facing surfaces.
            var indices=(int[])bucket.index.Clone();for(int i=0;i<indices.Length;i+=3){int swap=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=swap;}
            var mesh=new Mesh {name="Web board "+bucket.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.vertices=positions;mesh.normals=normals;mesh.uv=uv;mesh.colors=colors;mesh.triangles=indices;mesh.RecalculateBounds();owned.Add(mesh);
            boardTriangles+=indices.Length/3;
            var material=new Material(Shader.Find("Stronghold/Board"));owned.Add(material);
            material.mainTexture=bucket.name=="pipe"?Texture2D.whiteTexture:Resources.Load<Texture2D>(bucket.name=="decal"?"Board/common":"Board/D");
            if(bucket.name=="board")material.SetTexture("_EmissionTex",Resources.Load<Texture2D>("Board/E"));
            if(bucket.name=="glass")material.SetColor("_Color",new Color(.65f,.75f,.85f));
            var go=new GameObject("Board "+bucket.name);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
    void StepStress(float dt) {
        if(!effects || failed>0)return;
        tickAccumulator+=dt;
        while(tickAccumulator>=.05f) {
            tickAccumulator-=.05f;
            var frame=workload.ticks[tick];simulationTime=frame.gt;
            for(int i=0;i<actors.Count;i++) {
                if(hp[i]<=0)hp[i]=workload.units[i].maxHp;
                actors[i].transform.position=UnitPosition(workload.units[i],simulationTime);
                actorRenderers[i].sortingOrder=20000-Mathf.RoundToInt(actors[i].transform.position.z*100);
            }
            foreach(var ev in frame.events) {
                int a=ev.a-1,b=ev.b-1;
                var from=actors[a].transform.position+Vector3.up*.45f;var to=actors[b].transform.position+Vector3.up*.45f;
                effects.Emit(ev.kind,from,to,ev.value,ev.style,ev.type);
                if(ev.kind=="attack") {
                    hp[b]-=ev.value;effects.Emit("damage",to,to,ev.value,null,ev.type);
                    animationDrivers[a].Attack();
                } else if(ev.kind=="heal") hp[b]=Mathf.Min(workload.units[b].maxHp,hp[b]+ev.value);
                else if(ev.kind=="skill") {
                    animationDrivers[a].Skill();
                }
            }
            tick=(tick+1)%workload.ticks.Length;
        }
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
        if(!initialized)return;
        float dt=Time.unscaledDeltaTime; elapsed+=dt; attackClock+=dt;
        if(replay!=null){
            replay.Update(dt);status=replay.Status;
            if(!replayCaptured && replay.GameTime>=25){replayCaptured=true;Capture(Path.Combine(Application.persistentDataPath,"replay-mid.png"));}
            if(replay.Finished && !replaySaved){
                replaySaved=true;
                var counts=new List<ReplayCount>();foreach(var pair in replay.Counts)counts.Add(new ReplayCount{kind=pair.Key,count=pair.Value});
                var positions=new float[actors.Count*3];var visible=new List<int>();
                for(int i=0;i<actors.Count;i++){
                    var p=actors[i].transform.position;positions[i*3]=p.x;positions[i*3+1]=p.y;positions[i*3+2]=p.z;
                    if(actors[i].gameObject.activeSelf)visible.Add(workload.units[i].id);
                }
                var result=new ReplayResult{snapshots=workload.replayFrames.Length,units=actors.Count,models=modelCount,failures=failed,events=counts.ToArray(),states=replay.FinalStates,positions=positions,hp=hp,visibleIds=visible.ToArray()};
                output=Path.Combine(Application.persistentDataPath,"replay-result.json");
                File.WriteAllText(output,JsonUtility.ToJson(result,true));Capture(Path.Combine(Application.persistentDataPath,"replay-final.png"));
                Debug.Log("REPLAY RESULT: "+output);
            }
            return;
        }
        if(workload != null) StepStress(dt);
        else if(attackClock>=2) {
            attackClock=0;
            // Keep a repeatable mixture of idle and attack animations without a simulation or RNG.
            foreach(var actor in actors) {
                var attack=actor.Skeleton.Data.FindAnimation("Attack");
                if(attack != null) actor.AnimationState.SetAnimation(0,attack,true);
            }
        }
        if(elapsed>=30 && !saved && failed==0) {
            frames.Add(dt*1000);
            if(Application.isFocused) focusedSamples++;
            status="Recording " + frames.Count + "/"+sampleCount+" frames";
            if(frames.Count>=sampleCount) SaveResult();
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
            fps=frames.Count*1000/total,p50Ms=Percentile(.50),p95Ms=Percentile(.95),p99Ms=Percentile(.99),
            scene=workload != null ? "Web stress units/events + exported board geometry/atlases; simplified Unity FX/materials; no combat simulation" : "120 skeletons; synthetic board; no combat simulation",
            effectsEmitted=effects?effects.emitted:0,effectsDropped=effects?effects.dropped:0,peakEffects=effects?effects.peak:0,
            boardTriangles=boardTriangles,focusedSamples=focusedSamples
        };
        output=Path.Combine(Application.persistentDataPath,"benchmark-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json");
        File.WriteAllText(output,JsonUtility.ToJson(result,true));
        status=string.Format("Complete: {0:F1} FPS; p95 {1:F2} ms",result.fps,result.p95Ms);
        Debug.Log(status+" Result: "+output);
        Capture(Path.ChangeExtension(output,".png"));
    }
    void Capture(string path) {
        // Readback occurs after measurement, or outside benchmark mode during replay.
        var target = new RenderTexture(Screen.width, Screen.height, 24);
        var previous = RenderTexture.active;
        renderCamera.targetTexture = target;
        renderCamera.Render();
        RenderTexture.active = target;
        var capture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);
        capture.Apply();
        File.WriteAllBytes(path,capture.EncodeToPNG());
        renderCamera.targetTexture = null;
        RenderTexture.active = previous;
        Destroy(capture); target.Release(); Destroy(target);
    }
    float Percentile(double q) {return frames[Math.Min(frames.Count-1,(int)Math.Ceiling(frames.Count*q)-1)];}
    void OnGUI() {
        GUI.Box(new Rect(12,12,680,95),"Unity rendering prototype (not the full game)");
        GUI.Label(new Rect(25,38,650,25),"Units: "+actors.Count+"; models: "+modelCount+"; failures: "+failed+" | "+status);
        GUI.Label(new Rect(25,65,650,30),output ?? (workload != null ? "Web stress: 178 tiles + projectiles + damage/heal numbers; pooled FX" : "30s warmup + 3600 frames. Compare Windows build, not Editor FPS."));
    }
    void OnDestroy() { foreach(var o in owned) if(o) Destroy(o); }
}
