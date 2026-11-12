namespace RadarTest {
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

static class WpfProgram {
    [STAThread]
    static int Main(string[] args) {
        bool test=args.Length>0;
        try {
            if(test&&args[0]=="--self-test") {
                Tests.Run();Language.Verify();var p=ParameterCatalog.ByCommand("AT+Static");int value;
                if(!p.TryValue("15,5",out value)||value!=155||p.TryValue("0.4",out value))throw new Exception("Timer validation failed");
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Source","tests-core.txt"),"PASS: parser, replies, ACK/readback, persistence, zone geometry, mirrors, phosphor, timer conversion"+Environment.NewLine);
                return 0;
            }
            var application=new Application();application.ShutdownMode=ShutdownMode.OnMainWindowClose;
            using(var controller=new MonitorController(!test,!test||args[0]=="--preview")) {
                if(test&&args[0]=="--preview") {
                    controller.Window.Opacity=0;controller.Window.ShowInTaskbar=false;controller.Window.ShowActivated=false;controller.Window.Show();
                    controller.Window.Dispatcher.Invoke(DispatcherPriority.Loaded,new Action(delegate{}));controller.Window.UpdateLayout();
                    controller.SavePreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Source","Previews","wpf-current-settings.png"));controller.Window.Close();return 0;
                }
                if(test&&args[0]=="--smoke-test") {
                    controller.Window.Opacity=0;controller.Window.ShowInTaskbar=false;controller.Window.ShowActivated=false;
                    controller.Window.Show();controller.Window.Dispatcher.Invoke(DispatcherPriority.Loaded,new Action(delegate{}));
                    NumberField.Verify();
                    controller.VerifyLayout();controller.Window.Close();
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Source","tests-layout.txt"),"PASS: WPF templates and text presenters, 960/1120/1600/2560 widths. Rendered previews saved in Source/Previews. No serial port opened."+Environment.NewLine);
                    return 0;
                }
                application.Run(controller.Window);
            }
            return 0;
        } catch(Exception ex) {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"error.txt"),ex.ToString());
            if(!test)MessageBox.Show(ex.Message,Language.T(AppIdentity.Name+" — ошибка"));return 1;
        }
    }
}
}
