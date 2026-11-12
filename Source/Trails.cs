namespace RadarTest {
using System;
using System.Collections.Generic;
sealed class TrailMark {
    public uint Id; public float X,Y; public double Time;
}
sealed class PhosphorTrail {
    public readonly List<TrailMark> Marks=new List<TrailMark>();
    double sampledAt=double.NegativeInfinity;
    public void Clear() { Marks.Clear(); sampledAt=double.NegativeInfinity; }
    public void Add(Frame frame,double now) {
        if(!frame.Detailed || now-sampledAt<0.08) return;
        sampledAt=now;
        foreach(Target t in frame.Targets) Marks.Add(new TrailMark {Id=t.Id,X=t.X,Y=t.Y,Time=now});
        if(Marks.Count>16000) Marks.RemoveRange(0,Marks.Count-16000);
    }
    public void Prune(double now,double duration) { Marks.RemoveAll(p=>now-p.Time>=duration); }
    public static int Alpha(double age,double duration) { return (int)(210*Math.Max(0,Math.Min(1,1-age/duration))); }
}


}