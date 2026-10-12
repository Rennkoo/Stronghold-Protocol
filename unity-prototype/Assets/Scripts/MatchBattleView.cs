using System;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;

[Serializable] public class MatchCombat {public string key;public float gt;public int killed,total;public bool finished;public PlayInfo[] infos;public ReplayState[] states;public StressEvent[] events;public MatchTile[] tiles;}
public sealed class MatchBattleView:MonoBehaviour {
    sealed class Actor {public GameObject go;public SkeletonAnimation spine;public UnitAnimationDriver driver;public BenchmarkModel model;public PlayInfo info;public ReplayState state;public Vector3 from,to;}
    readonly Dictionary<string,BenchmarkModel> models=new Dictionary<string,BenchmarkModel>();
    readonly Dictionary<string,SkeletonDataAsset> loaded=new Dictionary<string,SkeletonDataAsset>();
    readonly Dictionary<int,Actor> actors=new Dictionary<int,Actor>();
    readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
    readonly Dictionary<string,Material> tileMaterials=new Dictionary<string,Material>();
    Camera camera;RenderTexture target;GameObject board;string key;float received;MatchCombat current;int fallback;string modelError;
    public int VisibleCount {get;private set;}
    public int FallbackCount {get{return fallback;}}
    public bool HasBattle {get{return current!=null;}}
    void Awake(){
        var full=Resources.Load<TextAsset>("match-models");if(full)foreach(var model in JsonUtility.FromJson<BenchmarkModels>(full.text).models)models[model.assetId+":"+model.skillIndex]=model;
        foreach(string prefix in new[]{"stress","replay-workload"}){
            var w=Resources.Load<TextAsset>(prefix);var m=Resources.Load<TextAsset>(prefix=="stress"?"models":"replay-models");
            if(!w||!m)continue;var data=JsonUtility.FromJson<StressWorkload>(w.text);var manifest=JsonUtility.FromJson<BenchmarkModels>(m.text);
            foreach(var u in data.units)if(!models.ContainsKey(u.assetId))models.Add(u.assetId,manifest.models[u.model]);
        }
        camera=new GameObject("Match battle camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=10;
        camera.transform.position=new Vector3(10,-9,-30);camera.backgroundColor=new Color(.04f,.05f,.065f);camera.clearFlags=CameraClearFlags.SolidColor;
        target=new RenderTexture(1024,640,16);target.Create();camera.targetTexture=target;
    }
    public void Apply(MatchCombat combat,bool paused){
        if(combat==null||string.IsNullOrEmpty(combat.key)||combat.infos==null||combat.states==null){current=null;foreach(var a in actors.Values)a.go.SetActive(false);return;}
        if(key!=combat.key){ClearField();key=combat.key;BuildBoard(combat.tiles);}
        current=combat;received=Time.unscaledTime;VisibleCount=0;
        foreach(var info in combat.infos){
            if(actors.ContainsKey(info.id)){actors[info.id].info=info;continue;}
            var actor=new Actor{info=info};
            BenchmarkModel model=null;
            if(!string.IsNullOrEmpty(info.assetId)&&(models.TryGetValue(info.assetId+":"+Mathf.Max(1,info.skillIndex),out model)||models.TryGetValue(info.assetId+":1",out model)||models.TryGetValue(info.assetId,out model))){
                try{
                    if(!loaded.TryGetValue(model.path,out var data)){
                        var atlasText=Resources.Load<TextAsset>(model.path+"/atlas");var skeleton=Resources.Load<TextAsset>(model.path+"/skeleton");
                        var material=new Material(Shader.Find("Spine/Skeleton"));if(!model.pma)material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");owned.Add(material);
                        skeleton.name="skeleton.skel";var atlas=SpineAtlasAsset.CreateRuntimeInstance(atlasText,Resources.LoadAll<Texture2D>(model.path),material,true);owned.Add(atlas);
                        data=SkeletonDataAsset.CreateRuntimeInstance(skeleton,atlas,true,.004f);owned.Add(data);if(data.GetSkeletonData(true)==null)throw new Exception("Skeleton decode failed");loaded.Add(model.path,data);
                    }
                    actor.spine=SkeletonAnimation.NewSkeletonAnimationGameObject(data);actor.go=actor.spine.gameObject;actor.model=model;actor.driver=new UnitAnimationDriver(actor.spine,model,info.enemy);
                }catch(Exception e){modelError=info.assetId+": "+e.Message;Debug.LogWarning("Match model fallback: "+modelError);}
            }
            if(actor.go==null){actor.go=GameObject.CreatePrimitive(PrimitiveType.Quad);Destroy(actor.go.GetComponent<Collider>());actor.go.transform.localScale=Vector3.one*.5f;
                string materialKey=info.enemy?"fallback-enemy":"fallback-ally";
                if(!tileMaterials.TryGetValue(materialKey,out var mat)){mat=new Material(Shader.Find("Unlit/Color"));mat.color=info.enemy?new Color(.9f,.3f,.25f):new Color(.2f,.7f,.95f);owned.Add(mat);tileMaterials.Add(materialKey,mat);}actor.go.GetComponent<MeshRenderer>().sharedMaterial=mat;fallback++;}
            actor.go.name="Match actor "+info.id;actor.from=actor.to=new Vector3(info.x,-info.y,-1);actor.go.transform.position=actor.to;actors.Add(info.id,actor);
        }
        foreach(var a in actors.Values)a.go.SetActive(false);
        foreach(var state in combat.states)if(actors.TryGetValue(state.id,out var a)){
            a.state=state;a.go.SetActive(true);VisibleCount++;a.from=a.go.transform.position;a.to=new Vector3(state.x,-state.y,-1);
            if(a.spine){a.go.transform.localScale=new Vector3(a.info.facing<0?-1:1,1,1);a.spine.timeScale=paused?0:2;a.driver.SetBase(state.anim,(state.flags&(2|4|256))!=0);a.go.GetComponent<MeshRenderer>().sortingOrder=20000-Mathf.RoundToInt(state.y*100);}
        }
        if(combat.events!=null)foreach(var ev in combat.events)if(actors.TryGetValue(ev.a,out var a)&&a.driver!=null){if(ev.kind=="atk")a.driver.Attack();else if(ev.kind=="skill"&&ev.value!=0)a.driver.Skill();else if(ev.kind=="deploy")a.driver.Deploy(a.model.deploy);else if(ev.kind=="die")a.driver.Die(a.model.die);}
    }
    void Update(){float t=Mathf.Clamp01((Time.unscaledTime-received)/.08f);foreach(var a in actors.Values)if(a.go.activeSelf)a.go.transform.position=Vector3.Lerp(a.from,a.to,t);}
    void BuildBoard(MatchTile[] tiles){
        board=new GameObject("Match map tiles");if(tiles==null)return;foreach(var tile in tiles){var go=GameObject.CreatePrimitive(PrimitiveType.Quad);Destroy(go.GetComponent<Collider>());go.transform.SetParent(board.transform);go.transform.position=new Vector3(tile.col,-tile.row,0);go.transform.localScale=Vector3.one*.96f;
            if(!tileMaterials.TryGetValue(tile.kind,out var mat)){mat=new Material(Shader.Find("Unlit/Color"));mat.color=tile.kind.Contains("forbidden")?new Color(.09f,.11f,.14f):tile.kind.Contains("end")?new Color(.1f,.35f,.65f):tile.kind.Contains("start")?new Color(.65f,.15f,.12f):new Color(.23f,.27f,.3f);owned.Add(mat);tileMaterials.Add(tile.kind,mat);}go.GetComponent<MeshRenderer>().sharedMaterial=mat;}
    }
    public void Draw(Rect rect){
        GUI.DrawTexture(rect,target,ScaleMode.StretchToFill);
        if(current==null)return;
        foreach(var a in actors.Values)if(a.go.activeSelf&&a.state!=null){var p=camera.WorldToViewportPoint(a.go.transform.position);float x=rect.x+p.x*rect.width,y=rect.y+(1-p.y)*rect.height;
            GUI.color=Color.black;GUI.DrawTexture(new Rect(x-20,y-22,40,4),Texture2D.whiteTexture);GUI.color=Color.green;GUI.DrawTexture(new Rect(x-20,y-22,40*Mathf.Clamp01((float)a.state.hp/Mathf.Max(1,a.state.maxHp)),4),Texture2D.whiteTexture);GUI.color=Color.white;
            if(!a.spine)GUI.Label(new Rect(x-45,y-18,90,36),a.info.name);
        }
        GUI.Label(new Rect(rect.x+8,rect.y+8,rect.width-16,45),$"作战 {current.gt:F1}s · 击败 {current.killed}/{current.total} · 可见 {VisibleCount} · 缺素材占位 {fallback}"+(modelError==null?"":" · 模型加载失败"));
    }
    void ClearField(){foreach(var a in actors.Values)Destroy(a.go);actors.Clear();if(board)Destroy(board);fallback=0;}
    public void SaveCapture(string path){camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();int lit=0;foreach(var pixel in texture.GetPixels32())if(pixel.r>30||pixel.g>30||pixel.b>30)lit++;System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());Destroy(texture);RenderTexture.active=previous;if(lit<100)throw new Exception("Match capture blank");Debug.Log("MATCH CAMERA RENDER PASS: pixels="+lit);}
    void OnDestroy(){ClearField();if(camera)Destroy(camera.gameObject);if(target){target.Release();Destroy(target);}foreach(var obj in owned)if(obj)Destroy(obj);}
}
