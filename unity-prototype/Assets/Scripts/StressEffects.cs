using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Fixed-capacity particles + one retained dynamic mesh: no per-event GameObjects or materials.
public sealed class StressEffects : MonoBehaviour {
    struct Particle {public int kind,glyph;public Vector3 from,to;public float age,life,size;public Color color;public string digits;}
    readonly Particle[] pool=new Particle[2048];
    readonly List<Vector3> vertices=new List<Vector3>(32768);
    readonly List<Vector2> uvs=new List<Vector2>(32768);
    readonly List<Color> colors=new List<Color>(32768);
    readonly List<int> triangles=new List<int>(49152);
    Mesh mesh; Material material; Texture2D atlas; Camera camera; int cursor;
    Transform[] barUnits;int[] hp;StressUnit[] definitions;
    public int active,peak,dropped,emitted;
    public void SetBars(Transform[] units,int[] health,StressUnit[] info){barUnits=units;hp=health;definitions=info;}
    public void Initialize(Camera cam) {
        camera=cam;
        atlas=new Texture2D(128,16,TextureFormat.RGBA32,false);atlas.filterMode=FilterMode.Point;
        var pixels=new Color[128*16];
        string[] digits={"11111100011000110001100011000111111","00100011000010000100001000010001110","11111000010000111111100001000011111","11111000010000101111000010000111111","10001100011000111111000010000100001","11111100001000011111000010000111111","11111100001000011111100011000111111","11111000010001000100010000100001000","11111100011000111111100011000111111","11111100011000111111000010000111111"};
        for(int g=0;g<10;g++)for(int y=0;y<7;y++)for(int x=0;x<5;x++) if(digits[g][y*5+x]=='1')pixels[(9-y)*128+g*8+x+1]=Color.white;
        for(int y=0;y<16;y++)for(int x=80;x<88;x++)pixels[y*128+x]=Color.white;
        for(int y=0;y<16;y++)for(int x=88;x<104;x++){float d=new Vector2((x-95.5f)/7.5f,(y-7.5f)/7.5f).magnitude;pixels[y*128+x]=new Color(1,1,1,Mathf.Clamp01((1-d)*8));}
        atlas.SetPixels(pixels);atlas.Apply();
        material=new Material(Shader.Find("Stronghold/Effects"));material.mainTexture=atlas;
        mesh=new Mesh {name="Pooled stress effects",indexFormat=IndexFormat.UInt32};mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=30000;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    public void Emit(string kind,Vector3 from,Vector3 to,int value,string style,string type) {
        if(kind=="attack" && style=="none")return;
        int slot=-1;
        for(int n=0;n<pool.Length;n++){int i=(cursor+n)%pool.Length;if(pool[i].life<=0){slot=i;cursor=(i+1)%pool.Length;break;}}
        if(slot<0){dropped++;return;}
        var p=new Particle {from=from,to=to,age=0,life=.7f,color=Color.white,size=.16f,glyph=11};
        if(kind=="damage"||kind=="heal"){p.kind=1;p.digits=value.ToString();p.from+=Vector3.up*.8f;p.color=kind=="heal"?new Color(.35f,1,.55f):type=="arts"?new Color(.5f,.75f,1):new Color(1,.85f,.4f);}
        else if(kind=="skill"){p.kind=2;p.life=.5f;p.color=new Color(.35f,.9f,1);}
        else {p.kind=style=="chain"?3:0;p.life=style=="chain"?.12f:style=="bomb"?.45f:.25f;p.size=style=="orb"||style=="bomb"?.18f:.09f;p.color=style=="bolt"?new Color(.4f,.7f,1):new Color(1,.7f,.3f);}
        pool[slot]=p;emitted++;
    }
    void LateUpdate() {
        if(!mesh)return;
        vertices.Clear();uvs.Clear();colors.Clear();triangles.Clear();active=0;
        Vector3 right=camera.transform.right,up=camera.transform.up;
        if(barUnits!=null)for(int i=0;i<barUnits.Length;i++) {
            var pos=barUnits[i].position+Vector3.up*1.2f;
            Quad(pos,right*.28f,up*.022f,10,new Color(.05f,.05f,.05f,.85f));
            float health=Mathf.Clamp01((float)hp[i]/definitions[i].maxHp);
            Quad(pos-right*(1-health)*.27f,right*(.27f*health),up*.016f,10,definitions[i].enemy?new Color(1,.3f,.25f):new Color(.25f,.9f,.5f));
        }
        for(int i=0;i<pool.Length;i++){
            var p=pool[i];if(p.life<=0)continue;p.age+=Time.unscaledDeltaTime;
            if(p.age>=p.life){p.life=0;pool[i]=p;continue;}pool[i]=p;active++;
            float t=p.age/p.life;Color c=p.color;c.a=1-t;
            if(p.kind==1){var pos=p.from+up*t*.6f;for(int k=0;k<p.digits.Length;k++)Quad(pos+right*(k-p.digits.Length*.5f)*.12f,right*.06f,up*.15f,p.digits[k]-'0',c);}
            else if(p.kind==2){float radius=.2f+t*.7f;for(int k=0;k<20;k++){float a=k*Mathf.PI*.1f;Quad(p.from+right*Mathf.Cos(a)*radius+up*Mathf.Sin(a)*radius,right*.025f,up*.025f,10,c);}}
            else if(p.kind==3){var delta=p.to-p.from;for(int k=0;k<12;k++)Quad(p.from+delta*(k/11f),right*.04f,up*.04f,11,c);}
            else {var pos=Vector3.Lerp(p.from,p.to,t)+Vector3.up*(p.life>.4f?Mathf.Sin(t*Mathf.PI)*.7f:0);Quad(pos,right*p.size,up*p.size,11,c);}
        }
        peak=Mathf.Max(peak,active);
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
    }
    void Quad(Vector3 center,Vector3 right,Vector3 up,int glyph,Color color){
        int b=vertices.Count;vertices.Add(center-right-up);vertices.Add(center+right-up);vertices.Add(center+right+up);vertices.Add(center-right+up);
        float x0=(glyph<10?glyph*8:glyph==10?80:88)/128f,x1=(glyph<10?glyph*8+8:glyph==10?88:104)/128f;
        uvs.Add(new Vector2(x0,0));uvs.Add(new Vector2(x1,0));uvs.Add(new Vector2(x1,1));uvs.Add(new Vector2(x0,1));
        for(int k=0;k<4;k++)colors.Add(color);
        triangles.Add(b);triangles.Add(b+1);triangles.Add(b+2);triangles.Add(b);triangles.Add(b+2);triangles.Add(b+3);
    }
    void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);if(atlas)Destroy(atlas);}
}
