using handy;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace ncserver
{
    public class Program
    {
        public static dynamic cfg { get; set; }
        static void Main(string[] args)
        {
            log.Regist(new ConsoleLogWriter());
            log.w($"Launched from:{Environment.CurrentDirectory}");
            Settings.Instance.Regist(new FileSettingsReader());
            RouteManager rm = new RouteManager();
            rm.Regist(typeof(WebBizUnit), true, false);
            var configfile = Path.Combine(Environment.CurrentDirectory, "server.json");
            if (!File.Exists(configfile))
            {
                log.e($"Missing config file: {configfile}");
                return;
            }
            cfg = EmrInstance.LoadFile(configfile);
            var basedir = cfg.basedir;
            if (!string.IsNullOrWhiteSpace(basedir))
            {
                Environment.CurrentDirectory = basedir;
            }
            if (cfg == null)
            {
                log.e($"Failed to load config file: {configfile}");
                return;
            }
            if (cfg.tasks != null)
            {
                EmrInstance.Each((EmrInstance)cfg.tasks, (i, n, k) =>
                {
                    if (i.Type == InstanceType.String)
                    {
                        Cmd.RegistTask(k, i.val);
                    }
                });
            }
            if (cfg.onstart != null)
            {
                EmrInstance.Each((EmrInstance)cfg.onstart, (i, n, k) =>
                {
                    if (i.Type == InstanceType.String)
                    {
                        bool isasync = false;
                        var cm = (string)i.val;
                        Cmd.Run(cm, isasync);
                    }
                });
            }
            var wss = new WebSocketStep();
            StreamListener.Steps.Regist(new HttpReadStep(), new HttpRouteStep(rm), wss);
            var listener = new SocketListener("httpread", "httproute");
            listener.Listen((int)cfg.port).WaitOne();

            var m = new FileMonitor();
            if (cfg.monitor != null)
            {
                EmrInstance.Each((EmrInstance)cfg.monitor, (i, n, k) =>
                {
                    var tar = i.Get<string>("target");
                    var tas = i.Get<string>("task");
                    var file = i.Get<string>("file");
                    if (!string.IsNullOrWhiteSpace(file))
                    {
                        m.AddFile(file, tas);
                    }
                    else
                    {
                        m.Add(tas, tar);
                    }
                });
                m.Start(()=>
                {
                    var frame = new WebSocketFrame();
                    frame.Take("reload");
                    wss.Reply(frame.ToBytes());
                });
            }
            Console.WriteLine("Press ESC to exit ...");
            while (true)
            {
                var ch = Console.ReadKey();
                if (ch.Key == ConsoleKey.Escape)
                {
                    m.Stop();
                    break;
                }
            }
        }
    }
    public class WebBizUnit : BizUnit
    {
        const string K_Upgrade = "UPGRADE";
        const string K_SKey = "Sec-WebSocket-Key";
        const string K_SAccept = "Sec-WebSocket-Accept";
        const string K_Connection = "Connection";
        static EmrInstance cfg => Program.cfg;
        public WebBizUnit(HttpStreamContext ctx) : base(ctx) { }
        public object Default()
        {
            var rooturl = cfg.Get("rooturl", "/");
            var rootdir = cfg.Get("rootdir", Environment.CurrentDirectory);
            if ("/".Equals(rootdir) || "\\".Equals(rootdir))
            {
                rootdir = string.Empty;
            }
            rootdir = rootdir.FullPath(Environment.CurrentDirectory);
            var r = string.Join("/",Request.Routes);
            if (r.StartsWith(rooturl))
            {
                r = r.Replace(rooturl, string.Empty);
            }
            else if (!"/".Equals(rooturl))
            {
                Response.HttpStatusCode = 404;
                return $"404 - not found - {r}";
            }
            if (!r.StartsWith('/'))
            {
                r = '/' + r;
            }
            var p = $"{rootdir.Replace('/', '\\')}{r.Replace('/', '\\')}".Replace("..", string.Empty);
            var entrydoc = cfg.Get<string>("entrydoc");
            if (!File.Exists(p) && !string.IsNullOrWhiteSpace(entrydoc))
            {
                p = Path.Combine(p, entrydoc.Replace("..", ""));
            }
            if (File.Exists(p))
            {
                Response.HttpStatusCode = 200;
                Response.Socket.WriteFile(p);
                return null;
            }
            Response.HttpStatusCode = 404;
            return $"404 not found - {p}";
        }
        public ExecuteResult WebSocket()
        {
            var rlt = new WebSocketResult();
            return rlt;
        }
    }
}
