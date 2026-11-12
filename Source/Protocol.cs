using System;
using System.Collections.Generic;

namespace RadarTest {
public sealed class Target {
    public uint Id; public float X, Y, Z, Vx, Vy, Vz;
}
public sealed class Frame {
    public uint Number; public int Count; public bool Detailed;
    public List<Target> Targets = new List<Target>();
}
// Streaming parser: preserves partial headers and recovers after corrupt data.
public sealed class Parser {
    readonly List<byte> buffer = new List<byte>();
    public long Good, Bad, Discarded;
    public event Action<Frame> Received;
    public event Action<byte> TextByte;
    static readonly byte[] Header = {1,2,3,4,5,6,7,8};
    uint U(int p) { return (uint)(buffer[p] | buffer[p+1]<<8 | buffer[p+2]<<16 | buffer[p+3]<<24); }
    float F(int p) { return BitConverter.ToSingle(buffer.GetRange(p,4).ToArray(),0); }
    void Drop() { byte b=buffer[0]; buffer.RemoveAt(0); Discarded++; if(TextByte!=null) TextByte(b); }
    public void Feed(byte[] bytes) {
        buffer.AddRange(bytes);
        while (buffer.Count > 0) {
            if (buffer[0] == 0x55) {
                if (buffer.Count < 2) return;
                if (buffer[1] != 0xaa) { Drop(); continue; }
                if (buffer.Count < 4) return;
                if (buffer[2] != 10 || buffer[3] != 4) { Bad++; Drop(); continue; }
                if (buffer.Count < 10) return;
                byte check=0; for(int i=2;i<9;i++) check ^= buffer[i];
                if(check != buffer[9]) { Bad++; Drop(); continue; }
                Frame frame = new Frame { Count=buffer[8] };
                buffer.RemoveRange(0,10); Publish(frame); continue;
            }
            if (buffer[0] != 1) { Drop(); continue; }
            bool match=true;
            for(int i=0;i<Math.Min(8,buffer.Count);i++) if(buffer[i]!=Header[i]) match=false;
            if(!match) { Drop(); continue; }
            if(buffer.Count<32) return;
            uint tracks=U(28), length=U(8);
            // V1.2 example LENGTH excludes the final XOR; accept either convention.
            if(U(16)!=1 || U(20)!=0 || U(24)!=2 || tracks>320 || tracks%32!=0 ||
                (length!=32+tracks && length!=33+tracks)) { Bad++; Drop(); continue; }
            int size=33+(int)tracks;
            if(buffer.Count<size) return;
            byte xor=0;
            for(int i=12;i<16;i++) xor ^= buffer[i];
            for(int i=32;i<size-1;i++) xor ^= buffer[i];
            if(xor!=buffer[size-1]) { Bad++; Drop(); continue; }
            Frame result=new Frame { Detailed=true, Number=U(12), Count=(int)tracks/32 };
            bool finite=true;
            for(int p=32;p<size-1;p+=32) {
                Target t=new Target { Id=U(p+4), X=F(p+8), Y=F(p+12), Z=F(p+16), Vx=F(p+20), Vy=F(p+24), Vz=F(p+28) };
                foreach(float value in new float[]{t.X,t.Y,t.Z,t.Vx,t.Vy,t.Vz})
                    if(float.IsNaN(value)||float.IsInfinity(value)) finite=false;
                result.Targets.Add(t);
            }
            if(!finite) { Bad++; buffer.RemoveRange(0,size); continue; }
            buffer.RemoveRange(0,size); Publish(result);
        }
    }
    void Publish(Frame f) { Good++; if(Received!=null) Received(f); }
}
}
