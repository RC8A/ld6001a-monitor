namespace RadarTest {
using System;
public static class Tests {
    static void Assert(bool ok,string name) { if(!ok) throw new Exception("FAIL: "+name); Console.WriteLine("PASS: "+name); }
    static byte[] Hex(string s) { return Array.ConvertAll(s.Split(' '),v=>Convert.ToByte(v,16)); }
    public static void Run() {
        var p=new Parser(); Frame last=null; p.Received+=f=>last=f;
        byte[] sample=Hex("01 02 03 04 05 06 07 08 40 00 00 00 A3 01 00 00 01 00 00 00 00 00 00 00 02 00 00 00 20 00 00 00 00 00 00 00 00 00 00 00 21 28 96 BF CB 85 20 40 9A AB A3 3E 8A BD C1 3D 50 98 99 BD 40 52 C3 3A CC");
        foreach(byte b in sample) p.Feed(new byte[]{b});
        Assert(p.Good==1 && last.Number==419 && last.Count==1 && Math.Abs(last.Targets[0].X+1.1731)<0.001,"manufacturer example, one byte at a time");
        byte[] simple=Hex("55 AA 0A 04 00 00 00 00 00 0E");
        p.Feed(simple); Assert(p.Good==2 && !last.Detailed && last.Count==0,"simple zero count");
        byte[] corrupt=(byte[])sample.Clone(); corrupt[64]^=1;
        p.Feed(corrupt); p.Feed(new byte[]{99,88,1,2,55}); p.Feed(simple);
        Assert(p.Good==3 && p.Bad>=1,"corrupt checksum and noise recovery");
        byte[] both=new byte[130]; Array.Copy(sample,0,both,0,65); Array.Copy(sample,0,both,65,65); p.Feed(both);
        Assert(p.Good==5,"concatenated frames");
        sample[8]=65; p.Feed(sample); Assert(p.Good==6,"length including checksum");
        byte[] empty=new byte[33]; Array.Copy(sample,empty,32); empty[8]=32; empty[28]=0; empty[32]=0xa2;
        p.Feed(empty); Assert(p.Good==7 && last.Count==0,"empty detailed frame");
        byte[] invalid=(byte[])sample.Clone(); invalid[28]=255; p.Feed(invalid); p.Feed(simple);
        Assert(p.Good==8,"invalid length recovery");

        SettingsTests.Run();

        ZoneTests.Run();
        var trail=new PhosphorTrail();
        var targetFrame=new Frame {Detailed=true}; targetFrame.Targets.Add(new Target {Id=1,X=2,Y=3});
        trail.Add(targetFrame,0); trail.Add(targetFrame,0.01);
        Assert(trail.Marks.Count==1,"trail sample rate bounded");
        trail.Add(targetFrame,1);
        Assert(PhosphorTrail.Alpha(0,10)>PhosphorTrail.Alpha(5,10) && PhosphorTrail.Alpha(10,10)==0,"phosphor fade by elapsed time");
        trail.Prune(10,10); Assert(trail.Marks.Count==1,"old marks expire independently");
        trail.Prune(11,10); Assert(trail.Marks.Count==0,"trail expires without new frames");
        trail.Add(targetFrame,12); trail.Clear(); Assert(trail.Marks.Count==0,"session reset clears trails");
        Console.WriteLine("All parser tests passed. No hardware accessed.");
    }
}
}
