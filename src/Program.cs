using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace SpecScope
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            bool tray = args.Any(a => a.Equals("/tray", StringComparison.OrdinalIgnoreCase));
            bool elevated = args.Any(a => a.Equals("/elevated", StringComparison.OrdinalIgnoreCase));

            // Single instance: a second launch just asks the first one to show its window.
            var mutex = new Mutex(false, @"Local\SpecScope.SingleInstance");
            bool owned = false;
            try { owned = mutex.WaitOne(elevated ? 8000 : 0); } catch (AbandonedMutexException) { owned = true; }
            var showEvt = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\SpecScope.Show");
            if (!owned)
            {
                if (!tray) showEvt.Set();
                return 0;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => { };
            var app = new TrayApp(tray);
            var t = new Thread(() => { while (showEvt.WaitOne()) app.RequestShow(); }) { IsBackground = true };
            t.Start();
            Application.Run(app);
            try { mutex.ReleaseMutex(); } catch { }
            return 0;
        }
    }
}
