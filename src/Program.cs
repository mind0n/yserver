using handy;
using System;
using System.IO;

namespace yserver
{
    public class Program
    {
        public static dynamic cfg { get; set; }
        static void Main(string[] args)
        {
            log.Regist(new FileLogWriter(), new ConsoleLogWriter());
            RouteManager rm = new RouteManager();
            rm.Regist(typeof(WebBizUnit), true, true);
            var configfile = Path.Combine(Environment.CurrentDirectory, "server.json");
            if (!File.Exists(configfile))
            {
                log.e($"Missing config file: {configfile}");
                return;
            }
            cfg = EmrInstance.LoadFile(configfile);
            if (cfg == null)
            {
                log.e($"Failed to load config file: {configfile}");
                return;
            }
            Extensions.Socket((int)cfg.port, (x) =>
            {
                var r = x.Request;
                var s = x.Response;
                var txt = r.Raw;
                log.i($"Request -> {txt}");
                var ctx = new SocketHttpContext(x);
                //var hs = new HttpStateMachine();
                //hs.Parse(txt);
                //var sr = hs.Result;
                var content = rm.Execute(ctx);
                if (content != null)
                {
                    s.Write(content);
                    log.i($"Replied <- {content?.Length}");
                }
                else
                {
                    log.i("Replied internally");
                }
            });
            Console.WriteLine("Press ESC to exit ...");
            while (true)
            {
                var ch = Console.ReadKey();
                if (ch.Key == ConsoleKey.Escape)
                {
                    break;
                }
            }
        }
    }
    public class WebBizUnit : BizUnit
    {
        public WebBizUnit(SocketHttpContext ctx) : base(ctx) { }
        public string Default()
        {
            //var file = Program.cfg
            var r = string.Join("\\",Request.Routes);
            return $"{r} - {Environment.CurrentDirectory}";
        }
    }
}
