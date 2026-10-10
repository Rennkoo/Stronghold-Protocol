using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

// Recorded authoritative state, with interpolated motion. Does not run combat simulation.
public sealed class ReplayPlayback {
    readonly StressWorkload data;
    readonly SkeletonAnimation[] actors;
    readonly UnitAnimationDriver[] drivers;
    readonly MeshRenderer[] renderers;
    readonly BenchmarkModel[] models;
    readonly int[] hp;
    readonly StressEffects effects;
    readonly TerrainHeightMap terrain;
    readonly Dictionary<int,int> indices=new Dictionary<int,int>();
    readonly Dictionary<int,ReplayState>[] snapshots;
    int frame=-1,eventCount;
    float clock;
    bool finished;
    public bool Finished {get{return finished;}}
    public float GameTime {get{return clock;}}
    public readonly Dictionary<string,int> Counts=new Dictionary<string,int>();
    public ReplayState[] FinalStates {get{return data.replayFrames[data.replayFrames.Length-1].states;}}
    public string Status {get{return "Replay "+Mathf.Min(clock,data.duration).ToString("F1")+" / "+data.duration+"s; snapshots "+(frame+1)+"; events "+eventCount+(finished?" (complete)":"");}}
    public ReplayPlayback(StressWorkload data,SkeletonAnimation[] actors,UnitAnimationDriver[] drivers,MeshRenderer[] renderers,BenchmarkModel[] models,int[] hp,StressEffects effects,TerrainHeightMap terrain) {
        this.data=data;this.actors=actors;this.drivers=drivers;this.renderers=renderers;this.models=models;this.hp=hp;this.effects=effects;
        this.terrain=terrain;
        for(int i=0;i<data.units.Length;i++){indices.Add(data.units[i].id,i);actors[i].gameObject.SetActive(false);actors[i].timeScale=2;}
        foreach(int id in data.initialIds)actors[indices[id]].gameObject.SetActive(true);
        snapshots=new Dictionary<int,ReplayState>[data.replayFrames.Length];
        for(int f=0;f<snapshots.Length;f++) {
            snapshots[f]=new Dictionary<int,ReplayState>();
            foreach(var state in data.replayFrames[f].states){if(!indices.ContainsKey(state.id))throw new Exception("Unknown replay unit "+state.id);snapshots[f].Add(state.id,state);}
        }
    }
    Vector3 Position(float x,float y) {
        float height=terrain.Get(x,y);
        return StressWorkload.World(x,y,height+.025f);
    }
    public void Update(float dt) {
        if(finished)return;
        clock+=dt*2;
        while(frame+1<data.replayFrames.Length && data.replayFrames[frame+1].gt<=clock) {
            frame++;
            foreach(var ev in data.replayFrames[frame].events){
                eventCount++;if(!Counts.ContainsKey(ev.kind))Counts[ev.kind]=0;Counts[ev.kind]++;
                Event(ev);
            }
            for(int i=0;i<actors.Length;i++) {
                if(!snapshots[frame].TryGetValue(data.units[i].id,out var state)){actors[i].gameObject.SetActive(false);continue;}
                actors[i].gameObject.SetActive(true);
                hp[i]=state.hp;data.units[i].maxHp=state.maxHp;
                if(state.hp>0)drivers[i].SetBase(state.anim,(state.flags&(2|4|256))!=0);
            }
        }
        if(frame>=0)foreach(var pair in snapshots[frame]) {
            int i=indices[pair.Key];var a=pair.Value;float x=a.x,y=a.y;
            if(frame+1<snapshots.Length && snapshots[frame+1].TryGetValue(pair.Key,out var b)){
                float alpha=Mathf.InverseLerp(data.replayFrames[frame].gt,data.replayFrames[frame+1].gt,clock);
                x=Mathf.Lerp(a.x,b.x,alpha);y=Mathf.Lerp(a.y,b.y,alpha);
            }
            actors[i].transform.position=Position(x,y);
            renderers[i].sortingOrder=20000-Mathf.RoundToInt(y*100);
        }
        if(frame==data.replayFrames.Length-1){finished=true;Debug.Log("REPLAY COMPLETE: snapshots="+(frame+1)+" events="+eventCount+" units="+indices.Count);}
    }
    void Event(StressEvent ev) {
        if(!indices.TryGetValue(ev.a,out int a))return;
        var from=actors[a].transform.position+Vector3.up*.45f;
        if(ev.kind=="spawn")actors[a].gameObject.SetActive(true);
        else if(ev.kind=="deploy")drivers[a].Deploy(models[data.units[a].model].deploy);
        else if(ev.kind=="atk"){
            drivers[a].Attack();
            if(indices.TryGetValue(ev.b,out int b))effects.Emit("attack",from,actors[b].transform.position+Vector3.up*.45f,0,ev.style,null);
        }else if(ev.kind=="skill" && ev.value!=0){drivers[a].Skill();effects.Emit("skill",from,from,0,null,null);}
        else if(ev.kind=="dmg" || ev.kind=="heal")effects.Emit(ev.kind=="dmg"?"damage":"heal",from,from,ev.value,null,ev.type);
        else if(ev.kind=="die")drivers[a].Die(models[data.units[a].model].die);
    }
}
