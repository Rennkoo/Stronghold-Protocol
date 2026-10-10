using System;

// Keep original slot allocation and ascending draw order while visiting only live particles.
public sealed class ParticleSlots {
    readonly bool[] used;
    readonly int[] next,previous;
    int cursor,tail=-1;
    public int Head {get;private set;}=-1;
    public int Count {get;private set;}
    public ParticleSlots(int capacity) {
        if(capacity<1)throw new ArgumentOutOfRangeException(nameof(capacity));
        used=new bool[capacity];next=new int[capacity];previous=new int[capacity];
    }
    public int Next(int slot) {return next[slot];}
    public int Acquire() {
        if(Count==used.Length)return -1;
        int slot=cursor;
        while(used[slot])slot=(slot+1)%used.Length;
        cursor=(slot+1)%used.Length;used[slot]=true;Count++;
        int before=-1,after=Head;
        if(tail>=0 && tail<slot){before=tail;after=-1;}
        else while(after>=0 && after<slot){before=after;after=next[after];}
        previous[slot]=before;next[slot]=after;
        if(before>=0)next[before]=slot;else Head=slot;
        if(after>=0)previous[after]=slot;else tail=slot;
        return slot;
    }
    public void Release(int slot) {
        if(slot<0 || slot>=used.Length || !used[slot])throw new InvalidOperationException("Particle slot is not active");
        int before=previous[slot],after=next[slot];
        if(before>=0)next[before]=after;else Head=after;
        if(after>=0)previous[after]=before;else tail=before;
        used[slot]=false;Count--;
    }
}
