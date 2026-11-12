namespace RadarTest {
using System;
using System.ComponentModel;

class NotifyRow : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    protected void Changed(string name) {if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs(name));}
}
sealed class TargetRow : NotifyRow {
    uint id;
    public uint Id {get{return id;}set{if(id!=value){id=value;Changed("Id");}}}
    float x,y,z,vx,vy,vz;
    public float X {get{return x;}set{if(x!=value){x=value;Changed("X");}}}
    public float Y {get{return y;}set{if(y!=value){y=value;Changed("Y");}}}
    public float Z {get{return z;}set{if(z!=value){z=value;Changed("Z");}}}
    public float Vx {get{return vx;}set{if(vx!=value){vx=value;Changed("Vx");}}}
    public float Vy {get{return vy;}set{if(vy!=value){vy=value;Changed("Vy");}}}
    public float Vz {get{return vz;}set{if(vz!=value){vz=value;Changed("Vz");}}}
    public void Update(Target t) {Id=t.Id;X=t.X;Y=t.Y;Z=t.Z;Vx=t.Vx;Vy=t.Vy;Vz=t.Vz;}
}
sealed class ZoneRow : NotifyRow {
    string name;
    public string Name {get{return Language.T(name);}set{name=value;Changed("Name");}}
    public bool IsSensor {get{return name=="Датчик";}}
    public void RefreshLanguage() {Changed("Name");}
    string xmin="—",xmax="—",ymin="—",ymax="—";
    public string XMin {get{return xmin;}set{xmin=value;Changed("XMin");}}
    public string XMax {get{return xmax;}set{xmax=value;Changed("XMax");}}
    public string YMin {get{return ymin;}set{ymin=value;Changed("YMin");}}
    public string YMax {get{return ymax;}set{ymax=value;Changed("YMax");}}
    public void Update(DetectionZone zone) {XMin=zone==null?"—":zone.XMin.ToString();XMax=zone==null?"—":zone.XMax.ToString();YMin=zone==null?"—":zone.YMin.ToString();YMax=zone==null?"—":zone.YMax.ToString();}
    public DetectionZone Parse() {int a,b,c,d;if(!int.TryParse(XMin,out a)||!int.TryParse(XMax,out b)||!int.TryParse(YMin,out c)||!int.TryParse(YMax,out d))return null;var z=new DetectionZone{XMin=a,XMax=b,YMin=c,YMax=d};return z.Valid?z:null;}
}
sealed class ParameterRow : NotifyRow {
    public ParameterDefinition Definition;
    public string Name {get{return Language.T(Definition.Name);}}
    public string Unit {get{return Language.T(Definition.Unit);}}
    public string Range {get{return Definition.Display(Definition.Min)+"…"+Definition.Display(Definition.Max);}}
    public string Mode0 {get{return Language.T("Да");}}
    public string Mode3 {get{return Language.T("Да");}}
    public string Description {get{return Language.T(Definition.Description);}}
    public void RefreshLanguage() {foreach(string name in new string[]{"Name","Unit","Range","Mode0","Mode3","Description"})Changed(name);}
    string value,actual;
    public string Value {get{return value;}set{this.value=value;Changed("Value");}}
    public string Actual {get{return actual;}set{actual=value;Changed("Actual");}}
}
}
