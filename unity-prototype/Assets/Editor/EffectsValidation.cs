using System;
using UnityEngine;

public static class EffectsValidation {
    public static void Run() {
        foreach(int value in new[]{0,1,9,10,99,100,149,9999,10000,int.MaxValue,int.MinValue}){
            long magnitude=DamageDigits.Magnitude(value);string expected=magnitude.ToString();
            Require(DamageDigits.Count(magnitude)==expected.Length,"Digit count including zero and integer limits");
            long divisor=DamageDigits.Divisor(expected.Length);
            ulong packed=DamageDigits.Pack(magnitude);
            for(int i=0;i<expected.Length;i++)Require((int)((packed>>((expected.Length-1-i)*4))&15)==expected[i]-'0',"Packed glyphs preserve decimal order");
            for(int i=0;i<expected.Length;i++){Require(magnitude/divisor%10==expected[i]-'0',"Numeric digits preserve decimal order");divisor/=10;}
        }
        const int capacity=32;
        var slots=new ParticleSlots(capacity);
        var occupied=new bool[capacity];int cursor=0,count=0;
        var random=new System.Random(17);
        for(int step=0;step<4000;step++){
            if(step<capacity || random.Next(3)!=0){
                int expected=-1;
                for(int n=0;n<capacity;n++){int i=(cursor+n)%capacity;if(!occupied[i]){expected=i;break;}}
                int actual=slots.Acquire();Require(actual==expected,"Original slot allocation preserved");
                if(actual>=0){occupied[actual]=true;cursor=(actual+1)%capacity;count++;}
            }else{
                int slot=random.Next(capacity);
                if(occupied[slot]){slots.Release(slot);occupied[slot]=false;count--;}
            }
            Require(slots.Count==count,"Live count agrees after saturation/reuse");
            int link=slots.Head;
            for(int i=0;i<capacity;i++)if(occupied[i]){Require(link==i,"Ascending alpha draw order preserved");link=slots.Next(link);}
            Require(link==-1,"No orphan or cyclic slot links");
        }
        var go=new GameObject("Effects validation");var cameraGo=new GameObject("Effects test camera");
        try{
            var effects=go.AddComponent<StressEffects>();effects.Initialize(cameraGo.AddComponent<Camera>());
            var mesh=go.GetComponent<MeshFilter>().sharedMesh;
            effects.Emit("damage",Vector3.zero,Vector3.zero,149,null,"phys");effects.AdvanceAndRender(.1f);
            Require(mesh.vertexCount==12 && mesh.GetIndexCount(0)==18,"Three digits retain their geometry");
            effects.Emit("skill",Vector3.zero,Vector3.zero,0,null,null);effects.AdvanceAndRender(.1f);
            Require(mesh.vertexCount==92 && mesh.GetIndexCount(0)==138,"Ring retains twenty segments");
            effects.AdvanceAndRender(1);
            Require(effects.active==0 && mesh.vertexCount==0 && mesh.GetIndexCount(0)==0,"Expired particles do not leave stale triangles");
            effects.Emit("attack",Vector3.zero,Vector3.one,0,"chain",null);effects.AdvanceAndRender(.01f);
            Require(mesh.vertexCount==48 && mesh.GetIndexCount(0)==72,"Reused index buffer uploads only active prefix");
            foreach(int index in mesh.triangles)Require(index<mesh.vertexCount,"All indices reference live vertices");
            var bounds=mesh.bounds;bounds.Expand(.0001f);
            foreach(var vertex in mesh.vertices)Require(bounds.Contains(vertex),"Bounds contain all effect vertices within float precision");
            Debug.Log("EFFECTS VALIDATION PASSED: slot reuse/order, expiry, index prefixes, geometry and bounds");
        }finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(cameraGo);}
    }
    static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
}
