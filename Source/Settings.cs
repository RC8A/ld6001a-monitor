namespace RadarTest {
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

// Key/value format intentionally tolerates unknown future options and individual bad values.
sealed class LocalSettings {
    public readonly Dictionary<string,string> Values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    public static LocalSettings Load(string path) {
        var s=new LocalSettings();
        if(File.Exists(path)) foreach(string line in File.ReadAllLines(path,Encoding.UTF8)) {
            int p=line.IndexOf('='); if(p>0) s.Values[line.Substring(0,p)]=line.Substring(p+1);
        }
        return s;
    }
    public string Get(string key,string fallback) {string v; return Values.TryGetValue(key,out v)?v:fallback;}
    public decimal Number(string key,decimal fallback,decimal min,decimal max) {
        decimal n; return decimal.TryParse(Get(key,""),NumberStyles.Number,CultureInfo.InvariantCulture,out n)&&n>=min&&n<=max?n:fallback;
    }
    public bool Flag(string key,bool fallback) { bool v; return bool.TryParse(Get(key,""),out v)?v:fallback; }
    public void Put(string key,object value) { Values[key]=Convert.ToString(value,CultureInfo.InvariantCulture); }
    public void Save(string path) {
        string temp=path+".tmp"; var lines=new List<string>();
        foreach(var pair in Values) lines.Add(pair.Key+"="+pair.Value);
        File.WriteAllLines(temp,lines.ToArray(),new UTF8Encoding(true));
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
    }
}

// Only receives bytes outside complete validated radar frames.
sealed class ReplyDecoder {
    readonly StringBuilder line=new StringBuilder(), block=new StringBuilder();
    bool inBlock;
    public event Action<string> Line;
    public event Action<Dictionary<string,int>> Settings;
    public void Feed(byte b) {
        if(b=='{') {inBlock=true;block.Length=0;line.Length=0;}
        if(inBlock) {
            if(b==9||b==10||b==13||b>=32&&b<127) block.Append((char)b);
            else {inBlock=false;block.Length=0;}
            if(block.Length>8192) {inBlock=false;block.Length=0;}
            if(b=='}' && inBlock) {
                inBlock=false;
                var values=Parse(block.ToString()); block.Length=0;
                if(Settings!=null) Settings(values);
            }
            return;
        }
        if(b==10 || b==13) {
            if(line.Length>0) {string text=line.ToString().Trim();line.Length=0;if(Line!=null) Line(text);}
        } else if(b>=32&&b<127) {line.Append((char)b);if(line.Length>1024)line.Length=0;}
        else if(b!=9) line.Length=0;
    }
    public static Dictionary<string,int> Parse(string text) {
        var values=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach(Match match in Regex.Matches(text,"\"(Range|Sen|detectionHeight|Debug|Heart_Time|Moving target|Static target|Target exit|XboundaryN|XboundaryP|YboundaryN|YboundaryP)\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)\\s*[,}]",RegexOptions.IgnoreCase)) {
            string key=match.Groups[1].Value;decimal value;
            if(!decimal.TryParse(match.Groups[2].Value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value))continue;
            if(key.Equals("Moving target",StringComparison.OrdinalIgnoreCase)||key.Equals("Static target",StringComparison.OrdinalIgnoreCase)||key.Equals("Target exit",StringComparison.OrdinalIgnoreCase))value*=10;
            if(value==decimal.Truncate(value)&&value>=int.MinValue&&value<=int.MaxValue)values[key]=(int)value;
        }
        return values;
    }
}

sealed class CommandStep {
    public string Text;
    public Dictionary<string,int> Expected;
    public bool Read {get {return Text=="AT+READ";}}
}
sealed class CommandQueue {
    readonly Queue<CommandStep> queue=new Queue<CommandStep>();
    CommandStep pending;
    bool completed;
    double deadline,quietUntil;
    public Action<string> Write,Report;
    public Action<string> Accepted;
    public Action<Dictionary<string,int>> Readback;
    public bool Busy {get {return pending!=null||queue.Count>0;}}
    public void Cancel() {queue.Clear();pending=null;completed=false;quietUntil=0;}
    public void Enqueue(string command,Dictionary<string,int> expected) {queue.Enqueue(new CommandStep {Text=command,Expected=expected});}
    void Say(string text) {if(Report!=null)Report(text);}
    public void Tick(double now) {
        if(pending!=null && completed) {pending=null;completed=false;quietUntil=now+0.2;}
        if(pending!=null && now>=deadline) {Fail("Нет ответа: "+pending.Text,now);return;}
        if(pending==null&&queue.Count>0&&now>=quietUntil) {
            pending=queue.Dequeue(); deadline=now+4;
            if(Write!=null)Write(pending.Text);
            Say("Ожидание датчика: "+pending.Text);
        }
    }
    void Fail(string message,double now) {queue.Clear();pending=null;completed=false;quietUntil=now+2;Say("Не подтверждено. "+message);}
    public void OnLine(string text,double now) {
        if(pending==null||completed)return;
        if(text.IndexOf("Fail",StringComparison.OrdinalIgnoreCase)>=0 || text.StartsWith("AT+ERROR",StringComparison.OrdinalIgnoreCase) || text=="ERROR") {
            Fail("Ошибка датчика: "+text,now);return;
        }
        if(pending.Read)return;
        if(!Regex.IsMatch(text,"^AT\\+OK(?:=-?\\d+(?:\\.\\d+)?)?$",RegexOptions.IgnoreCase))return;
        int eq=text.IndexOf('=');
        if(eq>=0) {
            int sent=pending.Text.IndexOf('=');
            decimal expected,received;var definition=ParameterCatalog.ByCommand(pending.Text.Split('=')[0]);
            if(sent>=0&&decimal.TryParse(pending.Text.Substring(sent+1),NumberStyles.Number,CultureInfo.InvariantCulture,out expected)&&decimal.TryParse(text.Substring(eq+1),NumberStyles.Number,CultureInfo.InvariantCulture,out received)) {
                if(expected!=received && !(definition!=null&&definition.Scale==10&&expected==received*10)) {Fail("Значение в ACK не совпало: "+text,now);return;}
            }
        }
        completed=true;if(Accepted!=null)Accepted(pending.Text);Say("Датчик принял: "+pending.Text+" ("+text+")");
    }
    public void OnSettings(Dictionary<string,int> values,double now) {
        if(pending==null||!pending.Read||completed)return;
        foreach(string key in new string[]{"Range","Sen","detectionHeight","Debug"})
            if(!values.ContainsKey(key)) {Fail("Неполный ответ AT+READ",now);return;}
        if(values["Range"]<100||values["Range"]>500||values["Sen"]<1||values["Sen"]>9||values["detectionHeight"]<50||values["detectionHeight"]>500||values["Debug"]<0||values["Debug"]>3) {
            Fail("Ответ содержит значения вне поддерживаемого диапазона",now);return;
        }
        if(pending.Expected!=null) foreach(var pair in pending.Expected) if(!values.ContainsKey(pair.Key)||values[pair.Key]!=pair.Value) {
            if(Readback!=null)Readback(values);
            Fail("Не совпало "+pair.Key+": ожидалось "+pair.Value+", прочитано "+(values.ContainsKey(pair.Key)?values[pair.Key].ToString():"нет в ответе"),now);return;
        }
        completed=true;
        if(Readback!=null)Readback(values);
        Say(pending.Expected==null?"Настройки прочитаны из датчика":"Подтверждено чтением настроек из датчика");
    }
}
static class SettingsTests {
    static void Check(bool ok,string label) {if(!ok)throw new Exception("Settings test: "+label);Console.WriteLine("PASS: "+label);}
    public static void Run() {
        string config=Path.Combine(Path.GetTempPath(),"LDMonitor-test-"+Guid.NewGuid().ToString("N")+".ini");
        try {
            var s=new LocalSettings();s.Put("MirrorX",true);s.Put("FadeSeconds",2.5m);s.Put("Port","COM7");s.Save(config);
            s.Put("FadeSeconds",8.5m);s.Save(config);
            var loaded=LocalSettings.Load(config);
            Check(loaded.Flag("MirrorX",false)&&loaded.Number("FadeSeconds",1,0.5m,60)==8.5m&&loaded.Get("Port","")=="COM7","settings round trip and atomic replacement");
            loaded.Put("FadeSeconds","bad");Check(loaded.Number("FadeSeconds",10,0.5m,60)==10,"invalid local option falls back");
        } finally {if(File.Exists(config))File.Delete(config);}
        var q=new CommandQueue();var sent=new List<string>();string report="";int snapshots=0;
        q.Write=sent.Add;q.Report=v=>report=v;q.Readback=v=>snapshots++;
        var expected=new Dictionary<string,int>{{"Range",300},{"detectionHeight",240},{"Sen",4},{"Debug",3}};
        q.Enqueue("AT+RANGE=300",null);q.Enqueue("AT+READ",expected);q.Tick(0);
        q.OnLine("AT+RANGE=300",0.1);Check(sent.Count==1&&q.Busy,"command echo does not confirm");
        q.OnLine("AT+OK=300",0.2);q.Tick(0.2);q.Tick(0.5);
        Check(sent.Count==2&&sent[1]=="AT+READ","serialized ACK then readback");
        q.OnLine("AT+OK",0.6);Check(snapshots==0,"read requires settings, not generic ACK");
        q.OnSettings(expected,0.7);q.Tick(0.8);Check(snapshots==1&&!q.Busy,"matching readback confirms");
        q.Enqueue("AT+RANGE=300",null);q.Enqueue("AT+READ",expected);q.Tick(1.2);q.OnLine("AT+OK=250",1.3);
        Check(!q.Busy&&report.Contains("Не подтверждено"),"mismatched ACK aborts queue");
        q.Enqueue("AT+RANGE=300",null);q.Enqueue("AT+READ",expected);q.Tick(4);q.Tick(9);
        Check(!q.Busy&&report.Contains("Нет ответа"),"timeout aborts without blind retry");
        q.Cancel();q.Enqueue("AT+READ",expected);q.Tick(10);
        var different=new Dictionary<string,int>(expected);different["Range"]=250;q.OnSettings(different,10.1);
        Check(!q.Busy&&report.Contains("Не совпало"),"wrong readback not confirmed");
        q.Cancel();q.Enqueue("AT+RANGE=300",null);q.Tick(11);q.OnLine("Save Para Fail",11.1);
        Check(!q.Busy&&report.Contains("Ошибка датчика"),"firmware save failure reported");
        var decoder=new ReplyDecoder();int received=0;decoder.Settings+=v=>{if(v["Range"]==300&&v["detectionHeight"]==240)received++;};
        foreach(byte b in Encoding.ASCII.GetBytes("{\n\"PeopleCntSoftVerison\":NOP_1.07-02,\n\"Range\":300,\"Sen\":4,\"Debug\":3,\"detectionHeight\":240,\n}"))decoder.Feed(b);
        Check(received==1,"real firmware settings syntax and fragmented response");
        var times=ReplyDecoder.Parse("{\"Moving target\":11.00,\"Static target\":10.00,\"Target exit\":0.50,\"Heart_Time\":60}");
        Check(times["Moving target"]==110&&times["Static target"]==100&&times["Target exit"]==5&&times["Heart_Time"]==60,"timer readback converted to command units");
        q.Cancel();q.Enqueue("AT+Moving=110",null);q.Tick(12);q.OnLine("AT+OK=11.00",12.1);q.Tick(12.2);
        Check(!q.Busy&&report.Contains("Датчик принял"),"timer ACK in seconds accepted for readback verification");
        var boundaries=ReplyDecoder.Parse("{\"Range\":300,\"Sen\":4,\"Debug\":3,\"detectionHeight\":240,\"XboundaryN\":-200,\"XboundaryP\":300,\"YboundaryN\":-150,\"YboundaryP\":250}");
        var zone=DetectionZone.FromValues(boundaries);
        Check(zone!=null&&zone.XMin==-200&&zone.YMax==250,"firmware boundary fields decoded");
        q.Cancel();q.Enqueue("AT+READ",zone.Values());q.Tick(20);q.OnSettings(boundaries,20.1);q.Tick(20.2);
        Check(!q.Busy&&report.Contains("Подтверждено"),"four boundaries verified by readback");
        q.Cancel();q.Enqueue("AT+READ",zone.Values());q.Tick(21);q.OnSettings(expected,21.1);
        Check(!q.Busy&&report.Contains("нет в ответе"),"missing boundary not accepted as success");
        var parser=new Parser();decoder=new ReplyDecoder();int acks=0;decoder.Line+=line=>{if(line=="AT+OK=240")acks++;};parser.TextByte+=decoder.Feed;
        byte[] frame=new byte[65];for(int i=0;i<8;i++)frame[i]=(byte)(i+1);frame[8]=64;frame[16]=1;frame[24]=2;frame[28]=32;
        Array.Copy(Encoding.ASCII.GetBytes("AT+OK=240\n"),0,frame,32,10);
        for(int i=12;i<16;i++)frame[64]^=frame[i];for(int i=32;i<64;i++)frame[64]^=frame[i];
        parser.Feed(frame);Check(acks==0,"binary frame payload cannot fake ACK");
        foreach(byte b in Encoding.ASCII.GetBytes("AT+OK=240\r\n"))parser.Feed(new byte[]{b});
        Check(acks==1,"ACK separated from radar stream");
        q.Cancel();var accepted=new List<string>();q.Accepted=accepted.Add;
        q.Enqueue("AT+START",null);q.Tick(30);Check(accepted.Count==0,"START not accepted on send");
        q.OnLine("AT+OK",30.1);Check(accepted.Count==1&&accepted[0]=="AT+START","START accepted only after ACK");
        q.Tick(30.2);q.Enqueue("AT+STOP",null);q.Tick(31);q.OnLine("ERROR",31.1);
        Check(accepted.Count==1,"failed STOP does not confirm stopped state");
        q.Enqueue("AT+STOP",null);q.Tick(34);q.OnLine("AT+OK",34.1);
        Check(accepted.Count==2&&accepted[1]=="AT+STOP","STOP accepted after ACK");
    }
}
}
