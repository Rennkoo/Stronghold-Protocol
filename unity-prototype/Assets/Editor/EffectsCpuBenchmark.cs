using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

[Serializable] public class EffectsCpuTrial {public string variant;public double msPerFrame;public int vertices,indices;}
[Serializable] public class EffectsCpuResult {public string unity,scope="Editor CPU only; 120 bars; deterministic 60Hz FX input; not whole-game FPS";public int samples=600;public bool identicalGeometry;public EffectsCpuTrial[] trials;}
public static class EffectsCpuBenchmark {
    sealed class Fixture:IDisposable {
        readonly GameObject root,cameraRoot;
        readonly GameObject[] bars=new GameObject[120];
        readonly Action<float> advance;
        readonly Action<string,Vector3,Vector3,int,string,string> emit;
        readonly Vector3[] positions=new Vector3[120];
        public readonly Mesh mesh;
        readonly string[] styles={"arrow","bolt","orb","none","bomb","chain"};
        public Fixture(Type type){
            root=new GameObject(type.Name);cameraRoot=new GameObject("CPU fixture camera");
            var component=(MonoBehaviour)root.AddComponent(type);component.enabled=false;
            type.GetMethod("Initialize").Invoke(component,new object[]{cameraRoot.AddComponent<Camera>()});
            advance=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),component,type.GetMethod("AdvanceAndRender"));
            emit=(Action<string,Vector3,Vector3,int,string,string>)Delegate.CreateDelegate(typeof(Action<string,Vector3,Vector3,int,string,string>),component,type.GetMethod("Emit"));
            var transforms=new Transform[120];var hp=new int[120];var units=new StressUnit[120];
            for(int i=0;i<120;i++){bars[i]=new GameObject("Bar");positions[i]=new Vector3(i%20,0,i/20);bars[i].transform.position=positions[i];transforms[i]=bars[i].transform;hp[i]=2000;units[i]=new StressUnit{maxHp=3000,enemy=i>=40};}
            type.GetMethod("SetBars").Invoke(component,new object[]{transforms,hp,units});
            mesh=root.GetComponent<MeshFilter>().sharedMesh;
        }
        public void Step(int frame){
            if(frame%3==0){
                for(int k=0;k<14;k++){var from=positions[k];var to=positions[40+k];emit("attack",from,to,0,styles[k%6],"phys");emit("damage",to,to,100+frame%800,null,"phys");}
                for(int k=0;k<4;k++)emit("heal",positions[k],positions[k],150,null,null);
                if(frame%30==0)emit("skill",positions[0],positions[0],0,null,null);
            }
            advance(1f/60);
        }
        public void Dispose(){
            // Destroy native allocations before component teardown (which uses deferred runtime cleanup).
            var material=root.GetComponent<MeshRenderer>().sharedMaterial;var texture=material.mainTexture;
            UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraRoot);foreach(var bar in bars)UnityEngine.Object.DestroyImmediate(bar);
        }
    }
    public static void Run(){
        var reference=typeof(StressEffects).Assembly.GetType("EffectsReference");
        if(reference==null)throw new Exception("Generate LocalReference with tools/benchmark-unity-effects.ps1");
        var trials=new List<EffectsCpuTrial>();Vector3[] originalVertices=null;Vector2[] originalUvs=null;Color[] originalColors=null;int[] originalIndices=null;
        foreach(var type in new[]{reference,typeof(StressEffects),typeof(StressEffects),reference})using(var fixture=new Fixture(type)){
            for(int f=0;f<180;f++)fixture.Step(f);
            GC.Collect();var watch=Stopwatch.StartNew();
            for(int f=180;f<780;f++)fixture.Step(f);
            watch.Stop();
            trials.Add(new EffectsCpuTrial{variant=type==reference?"old":"new",msPerFrame=watch.Elapsed.TotalMilliseconds/600,vertices=fixture.mesh.vertexCount,indices=fixture.mesh.triangles.Length});
            if(originalVertices==null){originalVertices=fixture.mesh.vertices;originalUvs=fixture.mesh.uv;originalColors=fixture.mesh.colors;originalIndices=fixture.mesh.triangles;}
            else{Equal(originalVertices,fixture.mesh.vertices);Equal(originalUvs,fixture.mesh.uv);Equal(originalColors,fixture.mesh.colors);Equal(originalIndices,fixture.mesh.triangles);}
        }
        string path=Environment.GetEnvironmentVariable("STRONGHOLD_FX_CPU_RESULT");if(string.IsNullOrEmpty(path))throw new Exception("Missing output path");
        File.WriteAllText(path,JsonUtility.ToJson(new EffectsCpuResult{unity=Application.unityVersion,identicalGeometry=true,trials=trials.ToArray()},true));
        UnityEngine.Debug.Log("EFFECTS CPU BENCHMARK PASSED: identical vertices/UV/colors/indices; "+path);
    }
    static void Equal<T>(T[] a,T[] b){if(a.Length!=b.Length)throw new Exception("Geometry count differs");for(int i=0;i<a.Length;i++)if(!EqualityComparer<T>.Default.Equals(a[i],b[i]))throw new Exception("Geometry differs at "+i);}
}
