using System;
using System.Threading;
using System.IO;
using System.Reflection;

public class AssemblyLoader
{
    public interface IAssemblyLoader
    {
        void Load(byte[] bytes);
    }

    public class AssemblyLoaderProxy : MarshalByRefObject, IAssemblyLoader
    {
        public void Load(byte[] bytes)
        {
            var assembly = AppDomain.CurrentDomain.Load(bytes);
            var type = assembly.GetType("DemoAssembly.DemoClass");
            var method = type.GetMethod("HelloWorld");
            var instance = Activator.CreateInstance(type, null);
            Console.WriteLine("--- Executed from {0}: {1}", AppDomain.CurrentDomain.FriendlyName, method.Invoke(instance, null));
        }
    }

    public static int StartTimer(string gargoyleDllContentsInBase64)
    {
        Console.WriteLine("Start timer function called");
        byte[] dllByteArray = Convert.FromBase64String(gargoyleDllContentsInBase64);
        Timer t = new Timer(new TimerCallback(TimerProcAssemblyLoad), dllByteArray, 0, 0);
        return 0;
    }

    private static void TimerProcAssemblyLoad(object state)
    {
        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        Console.WriteLine("Hello from timer!");

        String appDomainName = "TemporaryApplicationDomain";
        AppDomain applicationDomain = System.AppDomain.CreateDomain(appDomainName);
        var assmblyLoaderType = typeof(AssemblyLoaderProxy);
        var assemblyLoader = (IAssemblyLoader)applicationDomain.CreateInstanceFromAndUnwrap(assmblyLoaderType.Assembly.Location, assmblyLoaderType.FullName);
        assemblyLoader.Load((byte[])state);
        Console.WriteLine("Dynamic assembly has been loaded in new AppDomain " + appDomainName);

        AppDomain.Unload(applicationDomain);
        Console.WriteLine("New AppDomain has been unloaded");

        Timer t = new Timer(new TimerCallback(TimerProcAssemblyLoad), state, 1000, 0);
    }

    private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
        try
        {
            Assembly assembly = System.Reflection.Assembly.Load(args.Name);
            if (assembly != null)
                return assembly;
        }
        catch
        { // ignore load error }

            // *** Try to load by filename - split out the filename of the full assembly name
            // *** and append the base path of the original assembly (ie. look in the same dir)
            // *** NOTE: this doesn't account for special search paths but then that never
            //           worked before either.
            string[] Parts = args.Name.Split(',');
            string File = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\" + Parts[0].Trim() + ".dll";

            return System.Reflection.Assembly.LoadFrom(File);
        }
        return null;
    }
}