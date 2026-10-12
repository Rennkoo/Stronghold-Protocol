using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Spine.Unity;

[Serializable] public class PlayPlacement {public int id,row,col;}
[Serializable] public class PlayCommand {public string action;public int id;public PlayPlacement[] placements;}
[Serializable] public class PlayInfo {public int id,maxHp;public string assetId,name;public bool enemy;public float x,y,facing;}
[Serializable] public class PlaySkill {public int id;public bool ready;public float sp,spMax;}
[Serializable] public class PlayResponse {public string error,message,result;public float gt;public bool finished;public int killed,total;public PlayInfo[] infos;public ReplayState[] states;public StressEvent[] events;public PlaySkill[] skills;}

public sealed class LiveBattleControl:MonoBehaviour {
    StressWorkload data;SkeletonAnimation[] actors;BenchmarkModel[] models;int[] hp;StressEffects effects;TerrainHeightMap terrain;Camera camera;
    UnitAnimationDriver[] drivers;
    readonly Dictionary<int,int> slots=new Dictionary<int,int>();
    readonly Queue<PlayCommand> commands=new Queue<PlayCommand>();
    readonly string[] names={"Matterhorn","Texas","Estelle","Insider","Greyy","Silence","Exusiai"};
    PlayPlacement[] placements=new PlayPlacement[7];PlaySkill[] skills;
    bool running,paused,busy;int selected;float nextPoll;string message="Choose an operator, then click a board tile. Start when ready.";PlayResponse state;
    bool smoke,smokeFinished;int unsupported;
    public void Initialize(StressWorkload data,SkeletonAnimation[] actors,BenchmarkModel[] models,int[] hp,StressEffects effects,TerrainHeightMap terrain,Camera camera){
        this.data=data;this.actors=actors;this.models=models;this.hp=hp;this.effects=effects;this.terrain=terrain;this.camera=camera;
        drivers=new UnitAnimationDriver[actors.Length];
        for(int i=0;i<7;i++){placements[i]=new PlayPlacement{id=i+1,row=Mathf.RoundToInt(data.units[i].y),col=Mathf.RoundToInt(data.units[i].x)};}
        Preview();
        smoke=Array.IndexOf(Environment.GetCommandLineArgs(),"-play-smoke")>=0;
        if(smoke)commands.Enqueue(new PlayCommand{action="start",placements=placements});
    }
    void Preview(){for(int i=0;i<actors.Length;i++){actors[i].gameObject.SetActive(i<7);if(i<7){actors[i].transform.position=Position(placements[i].col,placements[i].row);hp[i]=data.units[i].maxHp;}}}
    Vector3 Position(float x,float y){return StressWorkload.World(x,y,terrain.Get(x,y)+.025f);}
    void Update(){
        if(data==null)return;
        if(!running && Input.GetMouseButtonDown(0) && Input.mousePosition.x>340){
            var ray=camera.ScreenPointToRay(Input.mousePosition);float nearest=float.MaxValue;StressTerrain target=null;
            foreach(var tile in data.terrain){if(tile.x<2||tile.x>10||tile.y<9||tile.y>12)continue;
                var plane=new Plane(Vector3.up,new Vector3(0,tile.z,0));
                if(plane.Raycast(ray,out float distance)){var hit=ray.GetPoint(distance);float error=new Vector2(hit.x-tile.x,hit.z-tile.y).sqrMagnitude;if(error<.45f && distance<nearest){nearest=distance;target=tile;}}
            }
            if(target!=null){int r=Mathf.RoundToInt(target.y),c=Mathf.RoundToInt(target.x);bool occupied=false;
                for(int i=0;i<7;i++)if(i!=selected&&placements[i].row==r&&placements[i].col==c)occupied=true;
                if(occupied)message="Tile already occupied";else{placements[selected].row=r;placements[selected].col=c;Preview();message="Position updated; server checks legal terrain at Start.";}
            }
        }
        if(!busy && commands.Count>0)StartCoroutine(Send(commands.Dequeue()));
        else if(!busy && running&&!paused && state!=null&&!state.finished && Time.unscaledTime>=nextPoll)StartCoroutine(Send(new PlayCommand{action="step"}));
    }
    IEnumerator Send(PlayCommand command){
        busy=true;using(var request=new UnityWebRequest("http://127.0.0.1:3141/command","POST")){
            request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(command)));request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");request.timeout=5;
            yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success){message="Bridge error: "+request.downloadHandler.text;paused=true;}
            else{
                var response=JsonUtility.FromJson<PlayResponse>(request.downloadHandler.text);
                if(!string.IsNullOrEmpty(response.error)){message=response.error;}
                else{if(command.action=="start"){slots.Clear();for(int i=0;i<actors.Length;i++)actors[i].gameObject.SetActive(false);running=true;paused=false;}Apply(response);}
            }
        }busy=false;nextPoll=Time.unscaledTime+.05f;
    }
    void Apply(PlayResponse response){
        state=response;skills=response.skills;message=response.message;
        foreach(var info in response.infos){
            if(slots.ContainsKey(info.id))continue;
            int slot=-1;for(int i=0;i<data.units.Length;i++)if(data.units[i].assetId==info.assetId&&!slots.ContainsValue(i)){slot=i;break;}
            if(slot<0){message="Unsupported unit model: "+info.assetId;unsupported++;Debug.LogError(message);continue;}
            slots.Add(info.id,slot);drivers[slot]=new UnitAnimationDriver(actors[slot],models[data.units[slot].model],info.enemy);actors[slot].timeScale=2;
        }
        foreach(var actor in actors)actor.gameObject.SetActive(false);
        foreach(var s in response.states)if(slots.TryGetValue(s.id,out int i)){
            actors[i].gameObject.SetActive(true);actors[i].transform.position=Position(s.x,s.y);hp[i]=s.hp;data.units[i].maxHp=s.maxHp;
            actors[i].GetComponent<MeshRenderer>().sortingOrder=20000-Mathf.RoundToInt(s.y*100);drivers[i].SetBase(s.anim,(s.flags&(2|4|256))!=0);
        }
        foreach(var ev in response.events)if(slots.TryGetValue(ev.a,out int a)){
            var from=actors[a].transform.position+Vector3.up*.45f;
            if(ev.kind=="deploy"){drivers[a]=new UnitAnimationDriver(actors[a],models[data.units[a].model],data.units[a].enemy);drivers[a].Deploy(models[data.units[a].model].deploy);}
            else if(ev.kind=="atk"){drivers[a].Attack();if(slots.TryGetValue(ev.b,out int b))effects.Emit("attack",from,actors[b].transform.position+Vector3.up*.45f,0,ev.style,null);}
            else if(ev.kind=="skill"&&ev.value!=0){drivers[a].Skill();effects.Emit("skill",from,from,0,null,null);}
            else if(ev.kind=="dmg"||ev.kind=="heal")effects.Emit(ev.kind=="dmg"?"damage":"heal",from,from,ev.value,null,ev.type);
            else if(ev.kind=="die")drivers[a].Die(models[data.units[a].model].die);
        }
        if(smoke && response.finished && !smokeFinished){smokeFinished=true;GetComponent<StrongholdBenchmark>().Capture(System.IO.Path.Combine(Application.persistentDataPath,"live-play-final.png"));Debug.Log("LIVE PLAY COMPLETE: time="+response.gt+" kills="+response.killed+" total="+response.total+" unsupported="+unsupported);}
    }
    void OnGUI(){
        if(data==null)return;
        GUI.Box(new Rect(10,10,320,540),"LIVE local battle - real simulation");
        GUI.Label(new Rect(25,40,290,60),message);
        for(int i=0;i<7;i++){
            if(GUI.Button(new Rect(25,110+i*42,290,35),(selected==i?"> ":"")+names[i]+" ["+placements[i].row+","+placements[i].col+"]"))selected=i;
        }
        if(!running){if(GUI.Button(new Rect(25,420,290,40),"Start battle"))commands.Enqueue(new PlayCommand{action="start",placements=placements});}
        else{
            if(GUI.Button(new Rect(25,420,140,35),paused?"Resume":"Pause")){paused=!paused;foreach(var actor in actors)actor.timeScale=paused?0:2;}
            GUI.enabled=!busy;
            if(GUI.Button(new Rect(175,420,140,35),"New battle")){running=false;paused=true;state=null;commands.Clear();Preview();}
            GUI.enabled=true;
            var skill=skills==null?null:Array.Find(skills,s=>s.id==selected+1);
            GUI.enabled=skill!=null&&skill.ready&&!busy&&!paused;
            if(GUI.Button(new Rect(25,465,290,35),"Activate selected skill"))commands.Enqueue(new PlayCommand{action="skill",id=selected+1});GUI.enabled=true;
            if(state!=null)GUI.Label(new Rect(25,505,290,35),"Time "+state.gt.ToString("F1")+"   Kills "+state.killed+"/"+state.total+(state.finished?"  "+state.result:""));
        }
    }
}
