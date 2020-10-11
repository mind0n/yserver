﻿using handy;
using System;
using System.Diagnostics;
using System.IO;

namespace yserver
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
            if (cfg == null)
            {
                log.e($"Failed to load config file: {configfile}");
                return;
            }
            if (cfg.onstart != null)
            {
                EmrInstance.Each((EmrInstance)cfg.onstart, (i, n, k) =>
                {
                    if (i.Type == InstanceType.String)
                    {
                        bool isasync = false;
                        var cm = (string)i.val;
                        if (cm.StartsWith('~'))
                        {
                            cm = cm.Substring(1);
                            isasync = true;
                        }
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
        static EmrInstance cfg => Program.cfg;
        public WebBizUnit(SocketHttpContext ctx) : base(ctx) { }
        public string Default()
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
    }
}
