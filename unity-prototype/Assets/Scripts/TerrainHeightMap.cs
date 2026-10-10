using UnityEngine;

// Dense stage grid: one lookup replaces a scan of every tile for every moving unit.
public sealed class TerrainHeightMap {
    readonly int x0,y0,width,height;
    readonly float[] values;
    public TerrainHeightMap(StressTerrain[] tiles) {
        if(tiles==null || tiles.Length==0){values=new float[0];return;}
        int x1=int.MinValue,y1=int.MinValue; x0=int.MaxValue;y0=int.MaxValue;
        foreach(var tile in tiles){int x=Mathf.RoundToInt(tile.x),y=Mathf.RoundToInt(tile.y);x0=Mathf.Min(x0,x);y0=Mathf.Min(y0,y);x1=Mathf.Max(x1,x);y1=Mathf.Max(y1,y);}
        width=x1-x0+1;height=y1-y0+1;values=new float[width*height];
        foreach(var tile in tiles)values[(Mathf.RoundToInt(tile.y)-y0)*width+Mathf.RoundToInt(tile.x)-x0]=tile.z;
    }
    public float Get(float x,float y) {
        int col=Mathf.RoundToInt(x)-x0,row=Mathf.RoundToInt(y)-y0;
        return col>=0 && col<width && row>=0 && row<height ? values[row*width+col] : 0;
    }
}
