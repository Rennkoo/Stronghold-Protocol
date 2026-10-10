using System;
using UnityEngine;
[Serializable] public class StressUnit { public int id,model,maxHp; public string defId,assetId; public bool enemy; public float x,y,facing,scale,scaleY; }
[Serializable] public class StressEvent { public string kind,style,type; public int a,b,value; }
[Serializable] public class StressTick { public float gt; public StressEvent[] events; }
[Serializable] public class StressCamera { public float fov,near,far; public float[] position,up,target; }
[Serializable] public class StressBucket { public string name; public float[] position,normal,uv,color; public int[] index; }
[Serializable] public class StressTerrain { public float x,y,z; }
[Serializable] public class ReplayState { public int id,hp,maxHp,flags,anim; public float x,y,sp,spMax; }
[Serializable] public class ReplayFrame { public float gt; public ReplayState[] states; public StressEvent[] events; }
[Serializable] public class StressWorkload {
    public int version; public string stageId; public float modelScale;
    public StressUnit[] units; public StressTick[] ticks; public StressCamera camera;
    public StressBucket[] buckets; public StressTerrain[] terrain;
    public float duration; public int[] initialIds; public ReplayFrame[] replayFrames;
    public static Vector3 World(float x,float y,float z) {return new Vector3(x,z,y);}
    public static Vector3 World(float[] v) {return World(v[0],v[1],v[2]);}
}
