using System;
using System.Threading;
using System.Windows;

namespace Dhikr
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool isFirst;
            using (new Mutex(true, @"Local\Dhikr.Widget.SingleInstance", out isFirst))
            {
                if (!isFirst) return;

                AppDomain.CurrentDomain.UnhandledException += (s, e) => Storage.Log(e.ExceptionObject);
                System.Windows.Forms.Application.EnableVisualStyles();
                // The bubble is tiny: software rendering skips the Direct3D pipeline and saves a lot of RAM.
                System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.DispatcherUnhandledException += (s, e) => { Storage.Log(e.Exception); e.Handled = true; };
                var controller = new Controller();
                app.Startup += (s, e) => controller.Start(args);
                app.Run();
            }
        }
    }
}
