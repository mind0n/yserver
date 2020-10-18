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
            log.Regist(new FileLogWriter(), new ConsoleLogWriter());
            Settings.Instance.Regist(new FileSettingsReader());
            RouteManager rm = new RouteManager();
            rm.Regist(typeof(WebBizUnit), true, true);
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
            Extensions.Socket((int)cfg.port, (x) =>
            {
                var r = x.Request;
                var s = x.Response;
                var txt = r.Raw;
                log.i($"Request -> {txt}");
                var ctx = new SocketHttpContext(x);
                var rlt = rm.Execute(ctx);
                if (rlt != null)
                {
                    if (rlt is string)
                    {
                        var content = (string)rlt;
                        s.Write(content);
                        log.i($"Replied <- {content?.Length}");
                        return new HandleResult(content);
                    }
                    else if (rlt is HandleResult)
                    {
                        return (HandleResult)rlt;
                    }
                }
                else
                {
                    log.i("Replied internally");
                }
                return new HandleResult() { IsContentRequired = false };
            });
            var m = new FileMonitor();
            if (cfg.monitor != null)
            {
                EmrInstance.Each((EmrInstance)cfg.monitor, (i, n, k) =>
                {
                    var tar = i.Get<string>("target");
                    var tas = i.Get<string>("task");
                    m.Add(tas, tar);
                });
                m.Start();
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
        public WebBizUnit(SocketHttpContext ctx) : base(ctx) { }
        public object Default()
        {
            var headers = Request.Headers;
            if (headers.ContainsKey(K_Upgrade) && "websocket".Equals(headers[K_Upgrade]))
            {
                var bk = headers.Get<string>(K_SKey);
                var a = $"{bk}258EAFA5-E914-47DA-95CA-C5AB0DC85B11"; //Encoding.UTF8.GetString(Convert.FromBase64String(bk));
                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                {
                    var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(a));
                    Response.Socket.Headers[K_SAccept] = Convert.ToBase64String(hash);
                    Response.Socket.Headers[K_Connection] = "Upgrade";
                    Response.HttpStatusCode = 101;
                    Response.Socket.Headers[K_Upgrade] = "websocket";
                }
                Response.Socket.Write();
                return new HandleResult() { KeepAlive = true };
            }
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
    }
}
