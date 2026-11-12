namespace RadarTest {
using System;
using System.Drawing;
using System.Collections.Generic;

sealed class DetectionZone {
    public int XMin=-450,XMax=450,YMin=-450,YMax=450;
    public DetectionZone Copy() {return new DetectionZone {XMin=XMin,XMax=XMax,YMin=YMin,YMax=YMax};}
    public bool Same(DetectionZone b) {return b!=null&&XMin==b.XMin&&XMax==b.XMax&&YMin==b.YMin&&YMax==b.YMax;}
    public bool Valid {get {return XMin>=-500&&XMin<=-20&&XMax>=20&&XMax<=500&&YMin>=-500&&YMin<=-20&&YMax>=20&&YMax<=500;}}
    public Dictionary<string,int> Values() {return new Dictionary<string,int>{{"XboundaryN",XMin},{"XboundaryP",XMax},{"YboundaryN",YMin},{"YboundaryP",YMax}};}
    public static DetectionZone FromValues(Dictionary<string,int> values) {
        foreach(string key in new string[]{"XboundaryN","XboundaryP","YboundaryN","YboundaryP"})if(!values.ContainsKey(key))return null;
        var zone=new DetectionZone {XMin=values["XboundaryN"],XMax=values["XboundaryP"],YMin=values["YboundaryN"],YMax=values["YboundaryP"]};
        return zone.Valid?zone:null;
    }
    static int Bound(float value,bool negative) {
        int cm=(int)Math.Round(value*100,MidpointRounding.AwayFromZero);
        return negative?Math.Max(-500,Math.Min(-20,cm)):Math.Min(500,Math.Max(20,cm));
    }
    public static DetectionZone Between(PointF a,PointF b) {
        return new DetectionZone {XMin=Bound(Math.Min(a.X,b.X),true),XMax=Bound(Math.Max(a.X,b.X),false),YMin=Bound(Math.Min(a.Y,b.Y),true),YMax=Bound(Math.Max(a.Y,b.Y),false)};
    }
    // Handle: 0..3 corners, 4..7 left/right/top/bottom midpoints in sensor coordinates.
    public void Resize(int handle,PointF p) {
        if(handle==0||handle==2||handle==4)XMin=Bound(p.X,true);
        if(handle==1||handle==3||handle==5)XMax=Bound(p.X,false);
        if(handle==0||handle==1||handle==6)YMax=Bound(p.Y,false);
        if(handle==2||handle==3||handle==7)YMin=Bound(p.Y,true);
    }
    public PointF[] Handles() {
        float a=XMin/100f,b=XMax/100f,c=YMin/100f,d=YMax/100f;
        return new PointF[]{new PointF(a,d),new PointF(b,d),new PointF(a,c),new PointF(b,c),new PointF(a,(c+d)/2),new PointF(b,(c+d)/2),new PointF((a+b)/2,d),new PointF((a+b)/2,c)};
    }
}
sealed class MapView {
    public readonly float Cx,Cy,Scale;
    public readonly bool MirrorX,MirrorY;
    public MapView(Size size,bool mirrorX,bool mirrorY) {
        Cx=size.Width/2f;Cy=size.Height/2f;Scale=Math.Max(1,Math.Min(size.Width-8,size.Height-8)/10f);MirrorX=mirrorX;MirrorY=mirrorY;
    }
    public PointF Screen(PointF p) {return new PointF(Cx+p.X*Scale*(MirrorX?-1:1),Cy-p.Y*Scale*(MirrorY?-1:1));}
    public PointF World(PointF p) {return new PointF((p.X-Cx)/Scale*(MirrorX?-1:1),(Cy-p.Y)/Scale*(MirrorY?-1:1));}
    public RectangleF Rectangle(DetectionZone zone) {
        var a=Screen(new PointF(zone.XMin/100f,zone.YMin/100f));var b=Screen(new PointF(zone.XMax/100f,zone.YMax/100f));
        return RectangleF.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y));
    }
}
static class ZoneTests {
    static void Check(bool b,string name) {if(!b)throw new Exception("Zone test: "+name);Console.WriteLine("PASS: "+name);}
    public static void Run() {
        var zone=DetectionZone.Between(new PointF(3,2),new PointF(-2,-1));
        Check(zone.XMin==-200&&zone.XMax==300&&zone.YMin==-100&&zone.YMax==200,"drag direction normalized");
        zone=DetectionZone.Between(new PointF(1,1),new PointF(9,9));
        Check(zone.XMin==-20&&zone.YMin==-20&&zone.XMax==500&&zone.YMax==500&&zone.Valid,"hardware limits and center inclusion");
        zone.Resize(0,new PointF(-2,3));Check(zone.XMin==-200&&zone.YMax==300,"corner resize");
        Check(zone.Same(DetectionZone.FromValues(zone.Values())),"boundary readback round trip");
        for(int i=0;i<4;i++) {
            var view=new MapView(new Size(1600,800),(i&1)!=0,(i&2)!=0);
            var p=new PointF(-1.25f,2.5f);var w=view.World(view.Screen(p));
            Check(Math.Abs(w.X-p.X)<0.0001&&Math.Abs(w.Y-p.Y)<0.0001,"mouse coordinates with mirrors "+i);
            var r=view.Rectangle(zone);Check(r.Width>0&&r.Height>0,"mirrored rectangle bounds "+i);
        }
    }
}
}
