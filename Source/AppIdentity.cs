namespace RadarTest {
using System;
using System.Reflection;

static class AppIdentity {
    public const string Name="LD6001A Monitor";
    public const string Author="Dmitry Popov";
    public const string License="PolyForm Noncommercial 1.0.0";
    public const string Credit="Разработано при участии OpenAI Codex";
    public static string Version {
        get {var info=(AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(),typeof(AssemblyInformationalVersionAttribute));return info==null?"1.0.0":info.InformationalVersion.Split('+')[0];}
    }
    public static string Build {get {return Assembly.GetExecutingAssembly().GetName().Version.Revision.ToString();}}
}
}
