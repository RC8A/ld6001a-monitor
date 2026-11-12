namespace RadarTest {
using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

sealed class MonitorController : IDisposable {
    public readonly Window Window;
    readonly bool persist;
    readonly LocalSettings saved;
    readonly string baseDir=AppDomain.CurrentDomain.BaseDirectory;
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly DispatcherTimer timer=new DispatcherTimer();
    readonly CommandQueue commands=new CommandQueue();
    readonly Dictionary<string,int> desired=new Dictionary<string,int>(),pending=new Dictionary<string,int>();
    readonly HashSet<string> known=new HashSet<string>();
    Dictionary<string,int> actual=new Dictionary<string,int>();
    readonly ObservableCollection<TargetRow> targetRows=new ObservableCollection<TargetRow>();
    readonly ObservableCollection<ZoneRow> zoneRows=new ObservableCollection<ZoneRow>();
    readonly ObservableCollection<ParameterRow> parameterRows=new ObservableCollection<ParameterRow>();
    readonly RadarCanvas map=new RadarCanvas {Focusable=true,ClipToBounds=true};
    Parser parser;ReplyDecoder decoder;SerialPort serial;FileStream raw;StreamWriter journal,csv;
    readonly ComboBox ports,baud;
    readonly CheckBox hide,phosphor,mx,my,editZone;
    readonly NumberField fade;
    readonly TextBox custom,log;
    readonly TextBlock commandStatus,status,zoneInfo;
    readonly DataGrid targets,zones;
    Window parameters;DataGrid parameterGrid;TextBlock parameterStatus;
    bool loading=true,dirty,frameDirty,demo,editingZone,editingParameter,disposed,updatingInputs;
    long bytes;int mode=3;int? confirmedMode;int heartbeat=60;
    double applyAt,saveAt,nextUi,lastFrameAt=double.NegativeInfinity,modeAt;
    Frame frame;
    bool? running;
    bool runPending,sensorReady,initializing;
    double initDeadline,nextInitRead;
    string lastReport="При подключении параметры считываются из датчика.";
    readonly StringBuilder pendingLog=new StringBuilder();
    public T Get<T>(string name) where T:class {return Window.FindName(name) as T;}
    public MonitorController(bool persistSettings) : this(persistSettings,persistSettings) {}
    public MonitorController(bool persistSettings,bool loadSaved) {
        persist=persistSettings;
        using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("RadarTest.Main.xaml"))Window=(Window)XamlReader.Load(input);
        using(var icon=Assembly.GetExecutingAssembly().GetManifestResourceStream("RadarTest.AppIcon.ico")) {
            Window.Icon=System.Windows.Media.Imaging.BitmapFrame.Create(icon,System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        }
        saved=loadSaved?LocalSettings.Load(Path.Combine(baseDir,"data","settings.ini")):new LocalSettings();
        foreach(var p in ParameterCatalog.All) {
            desired[p.Key]=(int)saved.Number(p.Command,p.Initial,p.Min,p.Max);
            if(saved.Values.ContainsKey(p.Command))known.Add(p.Key);
        }
        string[] zoneKeys={"XboundaryN","XboundaryP","YboundaryN","YboundaryP"};string[] localKeys={"ZoneXMin","ZoneXMax","ZoneYMin","ZoneYMax"};
        for(int i=0;i<4;i++)if(saved.Values.ContainsKey(localKeys[i])) {var p=Definition(zoneKeys[i]);desired[p.Key]=(int)saved.Number(localKeys[i],p.Initial,p.Min,p.Max);known.Add(p.Key);}
        mode=(int)saved.Number("OutputMode",3,0,3);
        Window.Width=(double)saved.Number("Width",1120,960,5000);Window.Height=(double)saved.Number("Height",780,650,3000);
        ports=Get<ComboBox>("Ports");baud=Get<ComboBox>("Baud");
        hide=Get<CheckBox>("HideStream");phosphor=Get<CheckBox>("Phosphor");mx=Get<CheckBox>("MirrorX");my=Get<CheckBox>("MirrorY");editZone=Get<CheckBox>("EditZone");
        fade=Get<NumberField>("Fade");custom=Get<TextBox>("CustomCommand");log=Get<TextBox>("Log");
        commandStatus=Get<TextBlock>("CommandStatus");status=Get<TextBlock>("Status");zoneInfo=Get<TextBlock>("ZoneInfo");
        targets=Get<DataGrid>("Targets");zones=Get<DataGrid>("Zones");targets.ItemsSource=targetRows;targets.Height=28;
        zoneRows.Add(new ZoneRow{Name="Датчик"});zoneRows.Add(new ZoneRow{Name="Проект"});zones.ItemsSource=zoneRows;
        Get<Grid>("MapHost").Children.Insert(0,map);
        baud.ItemsSource=new string[]{"115200","921600","9600"};baud.SelectedItem=saved.Get("Baud","115200");if(baud.SelectedItem==null)baud.SelectedIndex=0;
        RefreshPorts(saved.Get("Port","COM7"));
        hide.IsChecked=saved.Flag("HideStream",true);phosphor.IsChecked=saved.Flag("Phosphor",true);mx.IsChecked=saved.Flag("MirrorX",false);my.IsChecked=saved.Flag("MirrorY",false);
        fade.Value=saved.Number("FadeSeconds",10,0.5m,60);custom.Text=saved.Get("Command","AT+READ");
        RefreshMainInputs();
        foreach(string key in new string[]{"Range","detectionHeight","Sen"}) {
            string parameterKey=key;var input=MainInput(key);var definition=Definition(key);
            input.ValueEdited+=delegate {if(!loading&&!updatingInputs)EditParameter(Definition(parameterKey),(int)input.Value);};
            input.InvalidInput+=delegate {pending.Remove(definition.Command);};
        }
        map.Draft=DetectionZone.FromValues(desired)??new DetectionZone();map.ShowDraft=known.Contains("XboundaryN");zoneRows[1].Update(map.Draft);
        map.ZoneChanged=delegate(DetectionZone zone) {map.Draft=zone;zoneRows[1].Update(zone);foreach(var pair in zone.Values())EditParameter(Definition(pair.Key),pair.Value);};
        zones.BeginningEdit+=delegate(object s,DataGridBeginningEditEventArgs e) {if(e.Row.Item==zoneRows[0]||commands.Busy)e.Cancel=true;else editingZone=true;};
        zones.CellEditEnding+=delegate(object s,DataGridCellEditEndingEventArgs e) {
            if(e.EditAction!=DataGridEditAction.Commit){editingZone=false;return;}
            var input=e.EditingElement as TextBox;int number;int column=e.Column.DisplayIndex;
            bool negative=column==1||column==3;
            if(input==null||!int.TryParse(input.Text,out number)||(negative?(number< -500||number> -20):(number<20||number>500))) {
                e.Cancel=true;zoneInfo.Text=Language.T(negative?"Допустимо −500…−20 см":"Допустимо 20…500 см");zoneInfo.Foreground=Brushes.Firebrick;return;
            }
            Window.Dispatcher.BeginInvoke(new Action(delegate {
                editingZone=false;var zone=zoneRows[1].Parse();if(zone==null)return;
                map.Draft=zone;map.ShowDraft=true;foreach(var pair in zone.Values())EditParameter(Definition(pair.Key),pair.Value);UpdateZoneInfo();map.InvalidateVisual();
            }),DispatcherPriority.Background);
        };
        commands.Write=delegate(string text) {
            if(text.StartsWith("AT+DEBUG=",StringComparison.OrdinalIgnoreCase))Highlight(null);
            if(text.Equals("AT+START",StringComparison.OrdinalIgnoreCase)||text.Equals("AT+STOP",StringComparison.OrdinalIgnoreCase)) {runPending=true;HighlightRun(null);}
            serial.Write(text+"\n");Note("TX "+text+" <LF>");
        };
        commands.Accepted=delegate(string text) {
            if(text.Equals("AT+START",StringComparison.OrdinalIgnoreCase)) {runPending=false;HighlightRun(true);}
            if(text.Equals("AT+STOP",StringComparison.OrdinalIgnoreCase)) {runPending=false;HighlightRun(false);frame=null;lastFrameAt=double.NegativeInfinity;map.Trail.Clear();UpdateTargets();}
        };
        commands.Report=Report;commands.Readback=Readback;SetupParser();
        Click("Connect",delegate {if(serial==null)Connect();else Disconnect();});Click("Refresh",delegate {RefreshPorts(Convert.ToString(ports.SelectedItem));});
        Click("Demo",delegate {if(serial!=null)return;demo=!demo;frame=null;lastFrameAt=double.NegativeInfinity;map.Trail.Clear();Highlight(null);UpdateTargets();Report(demo?"Демо: синтетические цели":"Демо выключено");});
        Click("Read",delegate {Send("AT+READ");});Click("Start",delegate {Send("AT+START");});Click("Stop",delegate {Send("AT+STOP");});
        Click("Debug0",delegate {Send("AT+DEBUG=0");});Click("Debug3",delegate {Send("AT+DEBUG=3");});Click("Send",delegate {Send(custom.Text);});Click("ReadZone",delegate {Send("AT+READ");});
        Get<MenuItem>("ParametersMenu").Click+=delegate {OpenParameters();};Get<MenuItem>("ReadMenu").Click+=delegate {Send("AT+READ");};
        Get<MenuItem>("AboutMenu").Click+=delegate {CreateAbout().ShowDialog();};
        Get<MenuItem>("RussianMenu").Click+=delegate {ChangeLanguage("ru",true);};Get<MenuItem>("EnglishMenu").Click+=delegate {ChangeLanguage("en",true);};
        foreach(var box in new CheckBox[]{hide,phosphor,mx,my,editZone})box.Click+=delegate {if(box==phosphor)map.Trail.Clear();ApplyView();MarkDirty();};
        fade.ValueEdited+=delegate {ApplyView();MarkDirty();};custom.TextChanged+=delegate {MarkDirty();};
        ports.SelectionChanged+=delegate {MarkDirty();};baud.SelectionChanged+=delegate {MarkDirty();};Window.SizeChanged+=delegate {MarkDirty();};
        Window.Closing+=delegate {Save();Dispose();};
        ChangeLanguage(saved.Get("Language","ru"),false);ApplyView();Highlight(null);HighlightRun(null);loading=false;
        timer.Interval=TimeSpan.FromMilliseconds(50);timer.Tick+=Poll;timer.Start();
    }
    static ParameterDefinition Definition(string key) {foreach(var p in ParameterCatalog.All)if(p.Key==key)return p;return null;}
    NumberField MainInput(string key) {return Get<NumberField>(key=="Range"?"RangeInput":key=="Sen"?"ThresholdInput":"HeightInput");}
    void RefreshMainInputs() {
        updatingInputs=true;try {foreach(string key in new string[]{"Range","detectionHeight","Sen"}) {var input=MainInput(key);if(input.Value!=desired[key])input.Value=desired[key];}}finally {updatingInputs=false;}
    }
    void ChangeLanguage(string code,bool save) {
        Language.Set(code);Language.Apply(Window);
        Get<MenuItem>("RussianMenu").IsChecked=Language.Code=="ru";Get<MenuItem>("EnglishMenu").IsChecked=Language.Code=="en";
        Get<Button>("Connect").Content=Language.T(serial==null?"Подключить":"Отключить");
        fade.Value=fade.Value;commandStatus.Text=Language.T(lastReport);
        if(serial==null)status.Text=Language.T("Отключено · UART 8N1");
        foreach(var row in zoneRows)row.RefreshLanguage();foreach(var row in parameterRows)row.RefreshLanguage();
        if(parameters!=null)Language.Apply(parameters);RefreshParameters();UpdateZoneInfo();HighlightRun(running);ApplyView();
        if(save)MarkDirty();
    }
    void Click(string name,Action action) {Get<Button>(name).Click+=delegate {action();};}
    static bool On(CheckBox box) {return box.IsChecked==true;}
    void RefreshPorts(string preferred) {var list=new List<string>(SerialPort.GetPortNames());if(!list.Contains("COM7"))list.Add("COM7");if(!string.IsNullOrEmpty(preferred)&&!list.Contains(preferred))list.Add(preferred);list.Sort();ports.ItemsSource=list;ports.SelectedItem=string.IsNullOrEmpty(preferred)?"COM7":preferred;}
    void ApplyView() {
        map.Fade=(double)fade.Value;
        map.MirrorX=On(mx);map.MirrorY=On(my);map.Phosphor=On(phosphor);map.Editing=On(editZone)&&!commands.Busy;
        map.Cursor=On(editZone)?Cursors.Cross:Cursors.Arrow;
        Get<RowDefinition>("LogRow").Height=On(hide)?new GridLength(0):new GridLength(170);
        Get<TextBlock>("Axes").Text=Language.T("X "+(On(mx)?"←":"→")+"   Y "+(On(my)?"↓":"↑")+"   шаг сетки: 1");
        map.InvalidateVisual();
    }
    void SetupParser() {
        parser=new Parser();decoder=new ReplyDecoder();parser.Received+=OnFrame;parser.TextByte+=decoder.Feed;
        decoder.Line+=delegate(string line){commands.OnLine(line,clock.Elapsed.TotalSeconds);};decoder.Settings+=delegate(Dictionary<string,int> values){commands.OnSettings(values,clock.Elapsed.TotalSeconds);};
    }
    void Connect() {
        try {
            demo=false;commands.Cancel();pending.Clear();SetupParser();frame=null;lastFrameAt=double.NegativeInfinity;map.Trail.Clear();Highlight(null);HighlightRun(null);bytes=0;
            sensorReady=false;runPending=false;initializing=true;initDeadline=clock.Elapsed.TotalSeconds+30;nextInitRead=clock.Elapsed.TotalSeconds+5;
            Get<Button>("Start").IsEnabled=Get<Button>("Stop").IsEnabled=false;
            actual.Clear();map.Actual=null;zoneRows[0].Update(null);RefreshParameters();
            string folder=Path.Combine(baseDir,"logs",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(folder);
            raw=new FileStream(Path.Combine(folder,"rx.bin"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
            journal=new StreamWriter(Path.Combine(folder,"session.txt"),false,Encoding.UTF8);csv=new StreamWriter(Path.Combine(folder,"targets.csv"),false,Encoding.UTF8);csv.WriteLine("timestamp,mode,frame,count,id,x,y,z,vx,vy,vz");
            serial=new SerialPort(Convert.ToString(ports.SelectedItem),int.Parse(Convert.ToString(baud.SelectedItem)),Parity.None,8,StopBits.One);
            serial.Handshake=Handshake.None;serial.DtrEnable=false;serial.RtsEnable=false;serial.ReadTimeout=100;serial.WriteTimeout=500;serial.Open();
            ports.IsEnabled=baud.IsEnabled=false;Get<Button>("Connect").Content=Language.T("Отключить");Note("OPEN "+serial.PortName+" "+serial.BaudRate+" 8N1");
            commands.Enqueue("AT+READ",null);
            Report("Ожидание готовности датчика: чтение параметров, до 30 секунд.");
            Save();UpdateTargets();
        } catch(Exception ex) {Disconnect();Report("Ошибка подключения: "+ex.Message);}
    }
    void Disconnect() {
        sensorReady=false;initializing=false;runPending=false;HighlightRun(null);Get<Button>("Start").IsEnabled=Get<Button>("Stop").IsEnabled=true;
        commands.Cancel();pending.Clear();if(serial!=null){serial.Dispose();serial=null;}
        if(raw!=null){raw.Dispose();raw=null;}if(journal!=null){journal.Dispose();journal=null;}if(csv!=null){csv.Dispose();csv=null;}
        ports.IsEnabled=baud.IsEnabled=true;Get<Button>("Connect").Content=Language.T("Подключить");Highlight(null);
        actual.Clear();map.Actual=null;zoneRows[0].Update(null);frame=null;map.Trail.Clear();UpdateTargets();RefreshParameters();status.Text=Language.T("Отключено · UART 8N1");ApplyView();
    }
    void Send(string command) {
        if(serial==null){Report("Сначала подключите датчик.");return;}
        command=command.Trim();if(!command.StartsWith("AT+")||command.IndexOfAny(new char[]{'\r','\n'})>=0){Report("Введите одну AT-команду без перевода строки.");return;}
        if(commands.Busy){Report("Дождитесь ответа на предыдущую команду.");return;}
        commands.Enqueue(command,null);int eq=command.IndexOf('=');int value;
        if(eq>=0&&int.TryParse(command.Substring(eq+1),out value)) {
            var p=ParameterCatalog.ByCommand(command.Substring(0,eq));string key=p==null?(command.StartsWith("AT+DEBUG=",StringComparison.OrdinalIgnoreCase)?"Debug":null):p.Key;
            if(key!=null)commands.Enqueue("AT+READ",new Dictionary<string,int>{{key,value}});
        }
    }
    void EditParameter(ParameterDefinition p,int value) {
        desired[p.Key]=value;known.Add(p.Key);MarkDirty();map.Radius=desired["Range"];
        if(serial!=null) {pending[p.Command]=value;applyAt=clock.Elapsed.TotalSeconds+1;Report("Изменения будут отправлены через 1 с после последней правки.");}
        else Report("Сохранено локально. Датчик не подключён.");
        map.Draft=DetectionZone.FromValues(desired)??map.Draft;map.ShowDraft=!map.Draft.Same(map.Actual);zoneRows[1].Update(map.Draft);RefreshMainInputs();RefreshParameters();map.InvalidateVisual();
    }
    void Readback(Dictionary<string,int> values) {
        sensorReady=true;initializing=false;Get<Button>("Start").IsEnabled=Get<Button>("Stop").IsEnabled=true;
        actual=new Dictionary<string,int>(values);
        foreach(var p in ParameterCatalog.All) {
            int value;if(values.TryGetValue(p.Key,out value)&&value>=p.Min&&value<=p.Max&&!pending.ContainsKey(p.Command)) {desired[p.Key]=value;known.Add(p.Key);}
        }
        int returned=values["Debug"];mode=returned;
        if(confirmedMode!=returned){frame=null;lastFrameAt=double.NegativeInfinity;map.Trail.Clear();modeAt=clock.Elapsed.TotalSeconds;}
        int beat;if(values.TryGetValue("Heart_Time",out beat)&&beat>=10&&beat<=999)heartbeat=beat;
        Highlight(returned);map.Heartbeat=heartbeat;map.Simple=returned==0;
        map.Actual=DetectionZone.FromValues(values);map.ActualRadius=values["Range"];map.Radius=desired["Range"];
        if(!editingZone&&!map.IsDragging) {map.Draft=DetectionZone.FromValues(desired)??map.Draft;zoneRows[1].Update(map.Draft);}
        zoneRows[0].Update(map.Actual);map.ShowDraft=!map.Draft.Same(map.Actual);UpdateZoneInfo();RefreshMainInputs();RefreshParameters();MarkDirty();UpdateTargets();
    }
    void Highlight(int? value) {
        confirmedMode=value;foreach(int number in new int[]{0,3})Get<Button>("Debug"+number).Background=value==number?new SolidColorBrush(Color.FromRgb(209,238,220)):Brushes.White;
        if(!value.HasValue)map.Simple=false;
    }
    void HighlightRun(bool? value) {
        running=value;
        foreach(string name in new string[]{"Start","Stop"}) {
            var button=Get<Button>(name);bool selected=value.HasValue&&value.Value==(name=="Start");
            button.Background=selected?new SolidColorBrush(Color.FromRgb(209,238,220)):Brushes.White;
            button.ToolTip=Language.T(value.HasValue?(selected?"Подтверждённое состояние датчика":"Изменить состояние датчика"):"Состояние неизвестно. START подтверждается пакетами или ответом; STOP — ответом датчика.");
        }
    }
    void UpdateZoneInfo() {zoneInfo.Foreground=Brushes.Black;zoneInfo.Text=Language.T(map.Actual==null?"Границы не считаны. Проект сохранён локально.":map.Draft.Same(map.Actual)?"Зелёная зона считана из датчика. Ограничена радиусом.":"Оранжевая — проект. Автоприменение через 1 с.");}
    void OnFrame(Frame value) {
        if(serial!=null&&!demo&&!running.HasValue&&!runPending)HighlightRun(true);
        frame=value;lastFrameAt=clock.Elapsed.TotalSeconds;frameDirty=true;if(On(phosphor))map.Trail.Add(value,lastFrameAt);
        if(csv!=null) {
            string prefix=DateTime.Now.ToString("O")+","+(value.Detailed?"detailed":"simple")+","+value.Number+","+value.Count;
            foreach(var t in value.Targets)csv.WriteLine(prefix+","+t.Id+","+string.Join(",",Array.ConvertAll(new float[]{t.X,t.Y,t.Z,t.Vx,t.Vy,t.Vz},n=>n.ToString("R",CultureInfo.InvariantCulture))));
            if(value.Targets.Count==0)csv.WriteLine(prefix+",,,,,,,");csv.Flush();
        }
    }
    void UpdateTargets() {
        int count=frame==null?0:frame.Targets.Count;
        while(targetRows.Count>count)targetRows.RemoveAt(targetRows.Count-1);
        while(targetRows.Count<count)targetRows.Add(new TargetRow());
        for(int i=0;i<count;i++)targetRows[i].Update(frame.Targets[i]);
        targets.Height=28+count*24;map.Frame=frame;map.InvalidateVisual();
    }
    void Report(string text) {
        if(text.StartsWith("Не подтверждено"))runPending=false;
        lastReport=text;commandStatus.Text=Language.T(text);commandStatus.Foreground=text.StartsWith("Не подтверждено")||text.StartsWith("Ошибка")?Brushes.Firebrick:text.StartsWith("Подтверждено")||text.StartsWith("Настройки прочитаны")?Brushes.DarkGreen:new SolidColorBrush(Color.FromRgb(74,89,108));
        if(parameterStatus!=null){parameterStatus.Text=Language.T(text);parameterStatus.Foreground=commandStatus.Foreground;}Note("CMD "+text);
    }
    void Note(string text) {
        string line=DateTime.Now.ToString("O")+" "+text;if(journal!=null){journal.WriteLine(line);journal.Flush();}
        if(On(hide)&&text.StartsWith("RX "))return;pendingLog.AppendLine(line);if(pendingLog.Length>30000)pendingLog.Remove(0,pendingLog.Length-30000);
    }
    void Poll(object sender,EventArgs e) {
        if(disposed)return;
        try {
            double now=clock.Elapsed.TotalSeconds;if(dirty&&now>=saveAt)Save();
            if(demo&&now>=nextUi) {
                var f=new Frame{Detailed=true,Number=(uint)(now*10),Count=1};f.Targets.Add(new Target{Id=1,X=(float)(2*Math.Sin(now)),Y=(float)(2*Math.Cos(now)),Z=1.2f});OnFrame(f);
            }
            if(serial!=null) {
                int count=Math.Min(serial.BytesToRead,16384);if(count>0) {
                    var data=new byte[count];int n=serial.Read(data,0,count);if(n!=count)Array.Resize(ref data,n);
                    raw.Write(data,0,n);raw.Flush();bytes+=n;Note("RX "+BitConverter.ToString(data).Replace('-',' ')+" | "+Encoding.ASCII.GetString(data).Replace('\r','.').Replace('\n','↵'));parser.Feed(data);
                }
                if(initializing&&!sensorReady&&!commands.Busy) {
                    if(now>=initDeadline) {initializing=false;Report("Датчик не ответил за 30 с. Проверьте питание и порт; повторите AT+READ.");}
                    else if(now>=nextInitRead) {commands.Enqueue("AT+READ",null);nextInitRead=now+5;}
                }
                if(pending.Count>0&&!commands.Busy&&!editingZone&&!editingParameter&&!map.IsDragging&&now>=applyAt) {
                    var expected=new Dictionary<string,int>();foreach(var pair in pending){commands.Enqueue(pair.Key+"="+pair.Value,null);expected[ParameterCatalog.ByCommand(pair.Key).Key]=pair.Value;}
                    pending.Clear();commands.Enqueue("AT+READ",expected);
                }
                commands.Tick(now);
            }
            if(now<nextUi)return;nextUi=now+0.15;
            if(pendingLog.Length>0){if(log.Text.Length>90000)log.Text=log.Text.Substring(log.Text.Length-60000);log.AppendText(pendingLog.ToString());pendingLog.Length=0;if(!On(hide))log.ScrollToEnd();}
            if(serial!=null||demo) {
                string detail;
                if(confirmedMode==0&&!demo) {double age=double.IsNegativeInfinity(lastFrameAt)?now-modeAt:now-lastFrameAt;detail="DEBUG 0 · "+(frame==null?"ожидание первого счётчика":"последнее число людей: "+frame.Count)+" · heartbeat "+heartbeat+" с"+(age>heartbeat+5?" · счётчик не обновляется":"");}
                else {double age=now-lastFrameAt;detail=double.IsNegativeInfinity(lastFrameAt)?"Ожидание пакетов":age>2?"Нет свежих данных: "+age.ToString("F1")+" с":"Людей: "+(frame==null?"—":frame.Count.ToString())+" · координаты";if(age>2&&frame!=null){frame=null;frameDirty=true;}}
                if(!demo&&initializing)detail="Инициализация датчика · повторяем чтение параметров";
                else if(!demo&&running==false)detail="STOP · датчик остановлен";
                else if(!demo&&!running.HasValue)detail="START/STOP неизвестно · "+detail;
                else if(!demo)detail="START · "+detail;
                status.Text=Language.T((demo?"ДЕМО":"RX "+bytes+" байт · пакеты "+parser.Good+" · ошибки "+parser.Bad)+" | "+detail);
            }
            if(frameDirty){UpdateTargets();frameDirty=false;}
            map.Now=now;map.Editing=On(editZone)&&!commands.Busy;
            if(map.Trail.Marks.Count>0){map.Trail.Prune(now,map.Fade);map.InvalidateVisual();}
        } catch(Exception ex){try{Note("ERROR "+ex.Message);}catch{}Disconnect();Report("Ошибка обмена: "+ex.Message);}
    }
    void MarkDirty() {if(loading||!persist)return;dirty=true;saveAt=clock.Elapsed.TotalSeconds+0.7;}
    void Save() {
        if(!persist)return;
        try {
            var s=new LocalSettings();s.Put("Port",Convert.ToString(ports.SelectedItem));s.Put("Baud",Convert.ToString(baud.SelectedItem));s.Put("HideStream",On(hide));s.Put("Phosphor",On(phosphor));s.Put("FadeSeconds",map.Fade);
            s.Put("Language",Language.Code);
            s.Put("MirrorX",On(mx));s.Put("MirrorY",On(my));s.Put("OutputMode",mode);s.Put("Command",custom.Text);
            foreach(var p in ParameterCatalog.All)if(known.Contains(p.Key))s.Put(p.Command,desired[p.Key]);
            if(known.Contains("XboundaryN")){s.Put("ZoneXMin",desired["XboundaryN"]);s.Put("ZoneXMax",desired["XboundaryP"]);s.Put("ZoneYMin",desired["YboundaryN"]);s.Put("ZoneYMax",desired["YboundaryP"]);}
            s.Put("Width",Window.WindowState==WindowState.Normal?Window.Width:Window.RestoreBounds.Width);s.Put("Height",Window.WindowState==WindowState.Normal?Window.Height:Window.RestoreBounds.Height);
            Directory.CreateDirectory(Path.Combine(baseDir,"data"));s.Save(Path.Combine(baseDir,"data","settings.ini"));dirty=false;
        } catch(Exception ex){dirty=false;Report("Ошибка сохранения: "+ex.Message);}
    }
    void OpenParameters(bool preview=false) {
        if(parameters!=null){parameters.Activate();return;}
        parameters=new Window{Title="Параметры датчика · DEBUG 0 / DEBUG 3",Width=1140,Height=530,MinWidth=920,MinHeight=400,Owner=Window,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=Window.FontFamily,FontSize=12,Background=Window.Background,Resources=Window.Resources};
        var dock=new DockPanel{Margin=new Thickness(6)};
        var hint=new TextBlock{Text="Параметры общие для DEBUG 0 и 3. Редактируйте «Значение» и нажмите Enter: отправка через 1 секунду, затем проверка чтением.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,6)};DockPanel.SetDock(hint,Dock.Top);dock.Children.Add(hint);
        var read=new Button{Content="Считать из датчика",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,6)};read.Click+=delegate{Send("AT+READ");};DockPanel.SetDock(read,Dock.Top);dock.Children.Add(read);
        parameterStatus=new TextBlock{Text=commandStatus.Text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};DockPanel.SetDock(parameterStatus,Dock.Bottom);dock.Children.Add(parameterStatus);
        parameterGrid=new DataGrid{CanUserAddRows=false,AutoGenerateColumns=false,HeadersVisibility=DataGridHeadersVisibility.Column,RowHeight=double.NaN,MinRowHeight=32};
        string[] headers={"Параметр","Значение","В датчике","Ед.","Диапазон","DEBUG 0","DEBUG 3","Назначение"};string[] props={"Name","Value","Actual","Unit","Range","Mode0","Mode3","Description"};double[] widths={180,80,80,35,90,65,65,500};
        for(int i=0;i<headers.Length;i++) {
            var column=new DataGridTextColumn{Header=headers[i],Binding=new Binding(props[i]),IsReadOnly=i!=1,Width=new DataGridLength(widths[i]),CanUserSort=false};
            if(i==headers.Length-1) {var style=new Style(typeof(TextBlock));style.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));style.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(4)));column.ElementStyle=style;}
            parameterGrid.Columns.Add(column);
        }
        parameterRows.Clear();foreach(var p in ParameterCatalog.All)parameterRows.Add(new ParameterRow{Definition=p});parameterGrid.ItemsSource=parameterRows;
        parameterGrid.BeginningEdit+=delegate {editingParameter=true;};
        parameterGrid.CellEditEnding+=delegate(object s,DataGridCellEditEndingEventArgs e) {
            if(e.EditAction!=DataGridEditAction.Commit){editingParameter=false;return;}
            var row=e.Row.Item as ParameterRow;var input=e.EditingElement as TextBox;int value;
            if(row==null||input==null)return;
            if(!row.Definition.TryValue(input.Text,out value)){e.Cancel=true;parameterStatus.Text=Language.T("Допустимо ")+row.Range+" "+row.Unit;parameterStatus.Foreground=Brushes.Firebrick;return;}
            var p=row.Definition;Window.Dispatcher.BeginInvoke(new Action(delegate {editingParameter=false;EditParameter(p,value);}),DispatcherPriority.Background);
        };
        dock.Children.Add(parameterGrid);parameters.Content=dock;parameters.Closed+=delegate {editingParameter=false;parameters=null;parameterGrid=null;parameterStatus=null;};Language.Apply(parameters);RefreshParameters();if(preview){parameters.Opacity=0;parameters.ShowInTaskbar=false;parameters.ShowActivated=false;}parameters.Show();
    }
    void RefreshParameters() {
        if(parameterGrid==null)return;
        foreach(var row in parameterRows) {
            if(editingParameter&&parameterGrid.CurrentItem==row)continue;
            row.Value=row.Definition.Display(desired[row.Definition.Key]);int value;row.Actual=actual.TryGetValue(row.Definition.Key,out value)?row.Definition.Display(value):"—";
        }
    }
    Window CreateAbout() {
        var about=new Window{Title="О программе — "+AppIdentity.Name,Width=540,Height=400,ResizeMode=ResizeMode.NoResize,Owner=Window,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=Window.FontFamily,FontSize=13,Background=Window.Background,Icon=Window.Icon,Resources=Window.Resources,UseLayoutRounding=true};
        var root=new Grid{Margin=new Thickness(24)};
        root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var brand=new Image{Height=82,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left};
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("RadarTest.Logo.png"))brand.Source=System.Windows.Media.Imaging.BitmapFrame.Create(stream,System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        root.Children.Add(brand);
        var details=new StackPanel{Margin=new Thickness(0,12,0,12)};Grid.SetRow(details,1);
        details.Children.Add(new TextBlock{Text="Версия "+AppIdentity.Version+" · сборка "+AppIdentity.Build,FontSize=16,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
        details.Children.Add(new TextBlock{Text="Автор: "+AppIdentity.Author,Margin=new Thickness(0,0,0,5)});
        details.Children.Add(new TextBlock{Text=AppIdentity.Credit,Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,10)});
        details.Children.Add(new TextBlock{Text="Лицензия: "+AppIdentity.License,Margin=new Thickness(0,0,0,8)});
        details.Children.Add(new TextBlock{Text="Тестирование HLK-LD6001A-60G через USB–UART.\nНезависимый проект, не программа производителя.",TextWrapping=TextWrapping.Wrap});
        root.Children.Add(details);var close=new Button{Content="Закрыть",HorizontalAlignment=HorizontalAlignment.Right,IsCancel=true};close.Click+=delegate{about.Close();};Grid.SetRow(close,2);root.Children.Add(close);about.Content=root;Language.Apply(about);return about;
    }
    public void Dispose() {if(disposed)return;disposed=true;timer.Stop();if(parameters!=null)parameters.Close();Disconnect();}
    public void VerifyLayout(bool bilingual=true) {
        string prefix=Language.Code=="en"?"en-":"";
        var measurements=new StringBuilder();
        foreach(double width in new double[]{960,1120,1600,2560}) {
            Window.Width=width;Window.Measure(new Size(width,780));Window.Arrange(new Rect(0,0,width,780));Window.UpdateLayout();
            foreach(string name in new string[]{"ConnectionRow","CommandsRow","ViewRow"}) {
                var row=Get<WrapPanel>(name);double height=0;var textLines=new Dictionary<int,List<double>>();
                foreach(FrameworkElement child in row.Children) {
                    if(child.ActualHeight<27.9)throw new Exception("WPF header height: "+name);
                    var p=child.TranslatePoint(new Point(),row);
                    if(p.X< -0.1||p.Y< -0.1||p.X+child.ActualWidth>row.ActualWidth+0.5||p.Y+child.ActualHeight>row.ActualHeight+0.5)throw new Exception("WPF header clipping: "+name);
                    height=Math.Max(height,p.Y+child.ActualHeight);
                    var button=child as Button;if(button!=null){button.ApplyTemplate();var presenter=FindVisual<ContentPresenter>(button);if(presenter==null||presenter.ActualHeight<14)throw new Exception("WPF button text not laid out");}
                    string expected=child is Button?Convert.ToString(((Button)child).Content):child is CheckBox?Convert.ToString(((CheckBox)child).Content):child is ComboBox?Convert.ToString(((ComboBox)child).SelectedItem):child is TextBlock?((TextBlock)child).Text:null;
                    double textTop=double.NaN;
                    var entry=child as TextBox;
                    var numeric=child as NumberField;if(numeric!=null)entry=numeric.Editor;
                    if(entry!=null&&!string.IsNullOrEmpty(entry.Text)) {var rect=entry.GetRectFromCharacterIndex(0);if(!rect.IsEmpty)textTop=entry.TranslatePoint(rect.TopLeft,row).Y;}
                    else if(expected!=null) {
                        var block=FindText(child,expected);
                        if(block!=null) {var pos=block.ContentStart.GetNextInsertionPosition(LogicalDirection.Forward);if(pos!=null){var rect=pos.GetCharacterRect(LogicalDirection.Forward);if(!rect.IsEmpty)textTop=block.TranslatePoint(rect.TopLeft,row).Y;}}
                    }
                    if(!double.IsNaN(textTop)) {
                        int line=(int)Math.Round(p.Y/28);List<double> values;if(!textLines.TryGetValue(line,out values)){values=new List<double>();textLines[line]=values;}values.Add(textTop);
                        measurements.AppendLine(width+" "+name+" "+child.GetType().Name+" "+expected+" text-top="+textTop.ToString("F2",CultureInfo.InvariantCulture));
                    }
                }
                if(height>row.ActualHeight+0.5)throw new Exception("WPF row overlap");
                foreach(var values in textLines.Values) {
                    double min=double.MaxValue,max=double.MinValue;foreach(double value in values){min=Math.Min(min,value);max=Math.Max(max,value);}
                    if(max-min>1.1) {File.WriteAllText(Path.Combine(baseDir,"Source","text-alignment-"+Language.Code+".txt"),measurements.ToString());throw new Exception("WPF text alignment differs by "+(max-min).ToString("F2")+" DIP in "+name);}
                }
            }
            SavePreview(Path.Combine(baseDir,"Source","Previews",prefix+"wpf-"+((int)width)+".png"));
        }
        File.WriteAllText(Path.Combine(baseDir,"Source","text-alignment-"+Language.Code+".txt"),measurements.ToString());
        Window.Width=1120;Window.UpdateLayout();
        HighlightRun(true);SavePreview(Path.Combine(baseDir,"Source","Previews",prefix+"wpf-start.png"));
        if(Get<Button>("Start").Background==Brushes.White||Get<Button>("Stop").Background!=Brushes.White)throw new Exception("START highlight failed");
        HighlightRun(false);SavePreview(Path.Combine(baseDir,"Source","Previews",prefix+"wpf-stop.png"));
        if(Get<Button>("Stop").Background==Brushes.White||Get<Button>("Start").Background!=Brushes.White)throw new Exception("STOP highlight failed");
        HighlightRun(null);if(Get<Button>("Start").Background!=Brushes.White||Get<Button>("Stop").Background!=Brushes.White)throw new Exception("Unknown START/STOP state must not be selected");
        var about=CreateAbout();about.Opacity=0;about.ShowInTaskbar=false;about.ShowActivated=false;about.Show();about.UpdateLayout();SaveWindowPreview(about,Path.Combine(baseDir,"Source","Previews",prefix+"about.png"));about.Close();
        if(bilingual) {
            string original=Language.Code;ChangeLanguage("en",false);VerifyLayout(false);
            OpenParameters(true);parameters.UpdateLayout();
            if(parameterGrid.Columns[parameterGrid.Columns.Count-1].ActualWidth<300)throw new Exception("Parameter description column too narrow");
            SaveWindowPreview(parameters,Path.Combine(baseDir,"Source","Previews","en-parameters.png"));parameters.Close();
            ChangeLanguage(original,false);
        }
        frame=new Frame{Detailed=true,Count=3};frame.Targets.Add(new Target{Id=7,X=2.244f,Y=1.629f,Z=1.463f});frame.Targets.Add(new Target{Id=8,X=1.712f,Y=-1.731f,Z=1.727f});frame.Targets.Add(new Target{Id=9,X=1.231f,Y=0.485f,Z=1.900f});UpdateTargets();Window.UpdateLayout();
        double occupied=0;foreach(var column in targets.Columns)occupied+=column.ActualWidth;
        if(targets.CanUserResizeColumns||targets.CanUserReorderColumns||Math.Abs(occupied-(targets.ActualWidth-targets.BorderThickness.Left-targets.BorderThickness.Right))>2)throw new Exception("Target table must fill its width and have locked columns");
        SavePreview(Path.Combine(baseDir,"Source","Previews",prefix+"targets-table.png"));frame=null;UpdateTargets();
    }
    public void SavePreview(string path) {
        SaveWindowPreview(Window,path);
    }
    static void SaveWindowPreview(Window window,string path) {
        var visual=window.Content as FrameworkElement;visual.UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(visual.ActualWidth)),Math.Max(1,(int)Math.Ceiling(visual.ActualHeight)),96,96,PixelFormats.Pbgra32);
        var composed=new DrawingVisual();using(var dc=composed.RenderOpen()) {
            dc.DrawRectangle(window.Background,null,new Rect(0,0,visual.ActualWidth,visual.ActualHeight));
            dc.DrawRectangle(new VisualBrush(visual),null,new Rect(0,0,visual.ActualWidth,visual.ActualHeight));
        }
        bitmap.Render(composed);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path));using(var file=File.Create(path))encoder.Save(file);
    }
    static TextBlock FindText(DependencyObject root,string text) {var own=root as TextBlock;if(own!=null&&own.Text==text&&own.IsVisible)return own;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindText(VisualTreeHelper.GetChild(root,i),text);if(found!=null)return found;}return null;}
    static T FindVisual<T>(DependencyObject root) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T)return (T)child;var found=FindVisual<T>(child);if(found!=null)return found;}return null;}
}
}
