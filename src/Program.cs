using handy;
using System;

namespace yserver
{
    class Program
    {
        static void Main(string[] args)
        {
            RouteManager rm = new RouteManager();
            rm.Regist(typeof(WebBizUnit), true);
            Extensions.Socket(8880, (x) =>
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
            var file = "ws.html".SearchFile();
            return "200 OK";
        }
    }
}
