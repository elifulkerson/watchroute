using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.NetworkInformation;
using System.Net;

using System.Threading;


/*
 * Got some bits from here:
 * http://stackoverflow.com/questions/142614/traceroute-and-ping-in-c-sharp

 * 
 * This is also a "learning C#" type of project, so its going to be sloppy for the forseeable future.
 */


namespace watchroute
{
    static class settings
    {
        public static bool DoWeResolve { get; set; }
        public static int NumberOfHops { get; set; }
        public static int WaitTimeout { get; set; }
        public static int TimesToDo { get; set; }
        public static int Interval { get; set; }
        public static bool THREADS_DIE { get; set; }   // lets fix ye olde sleep loops to be more responsive
        public static int Redact { get; set; }
        public static bool SuppressOutput { get; set; }
    }

    public class tmpNode
    {
        // this is sloppy, but - we aren't making the Nodes until we exit the recursive traceroute, and I want
        // to have a place to stick additional data about the most recent tr behavior.

        public int ttl { get; set; }
        
        public IPStatus status { get; set; }
        public long roundtriptime { get;  set;}
        

        public tmpNode(int _ttl, IPStatus _status, long _rtt)
        {
            ttl = _ttl;
            status = _status;
            roundtriptime = _rtt;
        }
    }

    class Node
    {
        public string hostname;

        public IPAddress ip;
        public long ms = -1;

        public Queue<long> pingq = new Queue<long>();

        public int hopcount = -1;
        public long cycleid = 0;

        public int total_pings = 0;
        public int succesful_pings = 0;


        public Node(IPAddress _ip)
        {
            ip = _ip;
            hostname = "";
            //Console.WriteLine("node created");

            Thread myThread = new Thread(new ThreadStart( this.pingloop ));
            myThread.IsBackground = true;
            myThread.Start();

            pingq.Enqueue(-1);
            pingq.Enqueue(-1);
            pingq.Enqueue(-1);

            Thread resolveThread = new Thread(new ThreadStart(this.resolve));
            resolveThread.IsBackground = true;
            resolveThread.Start();
            //resolve();
        }

        public string getHopCount(long cycle) {
            if (cycleid == cycle)
            {
                return hopcount.ToString();
            }
            else
            {
                return "x";
            }
        }

        
        public void resolve()
        {
            
            if (settings.DoWeResolve == false)
            {
                return;
            }
            
            try
            {
                IPHostEntry entry = Dns.GetHostEntry(ip);
                hostname = entry.HostName;
            }
            catch
            {
                
            }
            
        }

        public void pingloop()
        {
            
            //int max_timeout = 1000;
            int max_timeout = settings.WaitTimeout;
            //Console.WriteLine("Thread started, oh yeah");

            Ping ping = new Ping();
            PingReply pingreply;
            int timeout = max_timeout;

            pingreply = ping.Send(ip, timeout);

            bool done = false;
            while (!done)
            {
                
                total_pings++;
                
                if (pingreply.Status == IPStatus.TimedOut)
                {
                    pingq.Dequeue();
                    pingq.Enqueue(-1);

                    ms = -1;
                }
                else
                {
                    pingq.Dequeue();
                    pingq.Enqueue(pingreply.RoundtripTime);

                    ms = pingreply.RoundtripTime;

                    succesful_pings++;                                        
                }

                int sleepytime = 0;
                while (sleepytime <= settings.Interval * 1000)
                {
                    System.Threading.Thread.Sleep(10);
                    sleepytime += 10;
                    if (settings.THREADS_DIE)
                    {
                       
                        sleepytime += settings.Interval * 1000;
                        done = true;
                    }
                }
                //System.Threading.Thread.Sleep(settings.Interval * 1000);

                if (!done)
                {
                    pingreply = ping.Send(ip, timeout);
                }

            }
            
        }
    }

    class Program
    {
        static void ShowHelp()
        {
            Console.WriteLine("watchroute.exe by Eli Fulkerson");
            Console.WriteLine("Please see http://www.elifulkerson.com/projects/ for updates.");
            Console.WriteLine("");
            Console.WriteLine("Usage: watchroute [-d][-h ttl][-w ms][-n times] target");
            Console.WriteLine("      -d      : Do not resolve DNS names");
            Console.WriteLine("      -h 8    : for instance, use a maximum ttl of 8");
            Console.WriteLine("      -w 2000 : for instance, use a timeout of 2000ms");
            Console.WriteLine("      -v      : print version and exit");
            Console.WriteLine("      -n 5    : for instance, quit after 5 traceroutes");
            Console.WriteLine("      -r 3    : for instance, redact any hop with TTL <= 3");
            Console.WriteLine("      -s      : suppress extra output");
            Console.WriteLine("");
        }

        static void ShowVersion()
        {
            Console.WriteLine("Version 0.5 - Mar/22/2015");
        }

        static void Main(string[] args)
        {
            settings.WaitTimeout = 1000;
            settings.Interval = 2;          // seconds
            settings.NumberOfHops = 32;
            settings.DoWeResolve = true;
            settings.TimesToDo = -1;
            settings.Redact = -1;
            settings.SuppressOutput = false;

            if (args == null || args.Count() == 0)
            {
                ShowHelp();
                return;
            }
            
            
            // parse args
            //int offset = 0;
            for (int x = 0; x < args.Count(); x++)
            {
                if (args[x] == "-d")
                {
                    //Console.WriteLine("* DNS Resolution disabled");
                    settings.DoWeResolve = false;
                }

                if (args[x] == "-r")
                {
                    int redact;
                    try
                    {
                        redact = int.Parse(args[x + 1]);
                    }
                    catch
                    {
                        redact = 0;
                    }

                    if (redact < 1)
                    {
                        Console.WriteLine("Redact TTL must be be an integer > 0.");
                        return;
                    }
                    //Console.WriteLine("* Redacting if TTL < : " + redact.ToString());
                    settings.Redact = redact;
                    x++;
                }

                if (args[x] == "-h")
                {
                    int hops;
                    try
                    {
                        hops = int.Parse(args[x + 1]);
                    }
                    catch
                    {
                        hops = 0;
                    }

                    if (hops < 1 )
                    {
                        Console.WriteLine("Maximum TTL must be be an integer > 0.");
                        return;
                    }
                    //Console.WriteLine("* Max TTL set: " + hops.ToString());
                    settings.NumberOfHops = hops;
                    x++;
                }

                if (args[x] == "-w")
                {
                    int timeout;
                    try
                    {
                        timeout = int.Parse(args[x + 1]);
                    }
                    catch
                    {
                        timeout = 0;
                    }
                    if (timeout < 1)
                    {
                        Console.WriteLine("Timeout must be an integer > 0.");
                        return;
                    }
                    //Console.WriteLine("* Timeout set: " + timeout.ToString() + " ms");
                    settings.WaitTimeout = timeout;
                    x++;
                }

                if (args[x] == "-n")
                {
                    int times_to_do;
                    try
                    {
                        times_to_do = int.Parse(args[x + 1]);
                    }
                    catch
                    {
                        times_to_do = 0;
                    }
                    if (times_to_do < 1)
                    {
                        Console.WriteLine("Number of cycles must be an integer > 0.");
                        return;
                    }
                    
                    settings.TimesToDo = times_to_do;
                    x++;
                }

                if (args[x] == "-i")
                {
                    int interval;
                    try
                    {
                        interval = int.Parse(args[x + 1]);
                    }
                    catch
                    {
                        interval = 0;
                    }
                    if (interval < 1)
                    {
                        Console.WriteLine("Interval must be an integer.");
                        return;
                    }

                    settings.Interval = interval;
                    x++;
                }

                if (args[x] == "-help" || args[x] == "?" || args[x] == "/?" || args[x] == "-?")
                {
                    ShowHelp();
                    return;
                }

                if (args[x] == "-v")
                {
                    ShowVersion();
                    return;
                }

                if (args[x] == "-s")
                {
                    settings.SuppressOutput = true;
                }
            }

            IEnumerable<IPAddress> route = default(IEnumerable<IPAddress>);

            List<IPAddress> omniroute = new List<IPAddress>();
            Dictionary<IPAddress, Node> nodedata = new Dictionary<IPAddress, Node>();
            Dictionary<IPAddress, tmpNode> trdata = new Dictionary<IPAddress, tmpNode>();

            IPAddress prevAddress = null;

            int hopcount;
            long cycleid = 0;
            int times_done = 0;
            while (true )
            {

                hopcount = 0;
                cycleid += 1;
                trdata.Clear();    // we don't care about previous cycles
                
                route = TraceRoute.GetTraceRoute(args[args.Count()-1], ref trdata);

                // loop through our new traceroute.  If there are new elements, append them after the instance of their predecessor
                // to keep everything roughly in line even if the number of hops changes

                if (settings.SuppressOutput == false)
                {
                    Console.WriteLine();
                    Console.WriteLine(); 
                }
                

                prevAddress = null;
                foreach (IPAddress a in route)
                {
                    hopcount += 1;

                    if (omniroute.Contains(a))
                    {
                        nodedata[a].hopcount = hopcount;
                        nodedata[a].cycleid = cycleid;
                        prevAddress = a;
                        continue;
                    }

                    if (omniroute.Count == 0) 
                    {
                        omniroute.Insert(0, a);
                    }
                    else
                    {
                        omniroute.Insert(omniroute.IndexOf(prevAddress) + 1, a);
                    }

                    nodedata.Add(a, new Node(a));
                    nodedata[a].hopcount = hopcount;
                    nodedata[a].cycleid = cycleid;
                    prevAddress = a;
                }

                if (settings.SuppressOutput == false)
                {
                    Console.WriteLine("Watching route to " + String.Join(" ", args[args.Count() - 1]));
                    Console.WriteLine("------------------" + String.Concat(Enumerable.Repeat("-", String.Join(" ", args[args.Count() - 1]).Length)));
                    
                }
                Console.WriteLine("");          
                
                foreach (IPAddress a in omniroute)
                {
                    bool appenddone = false;
                    List<long> pinglist = new List<long>();
                    while (!appenddone)  // doing this try/catch because the lock() didn't appear to do enough to prevent a crash in a case where data was
                                         // being modified in the other thread.  It was an intermittent error, but annoying.
                    {
                        try
                        {
                            pinglist = new List<long>();
                                foreach (long v in nodedata[a].pingq)
                                {
                                    pinglist.Add(v);
                                }
                                appenddone = true;
                        }
                        catch {
                        }

                    }
                    string display_ttl = "?";
                    
                    string p0 = pinglist[0].ToString() + " ms";
                    string p1 = pinglist[1].ToString() + " ms";
                    string p2 = pinglist[2].ToString() + " ms";

                    if (pinglist[0] == 0) { p0 = "<1 ms"; }
                    if (pinglist[1] == 0) { p1 = "<1 ms"; }
                    if (pinglist[2] == 0) { p2 = "<1 ms"; }

                    if (pinglist[0] == -1) { p0 = "*"; }
                    if (pinglist[1] == -1) { p1 = "*"; }
                    if (pinglist[2] == -1) { p2 = "*"; }

                    if (nodedata[a].getHopCount(cycleid) == "x")
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                    }
                    else
                    {
                        Console.ResetColor();
                    }

                    // this should be more accurate than getting the hopCount via iterating through the traceroute- 
                    // in case we ever have missed or duplicates or something.
                    if (nodedata[a].getHopCount(cycleid) == "x")
                    {
                        display_ttl = "x";
                    }
                    else
                    {
                        display_ttl = trdata[a].ttl.ToString();
                    }

                    if (nodedata[a].hostname == "")
                    {
                        if (nodedata[a].hopcount <= settings.Redact) {
                            Console.WriteLine("{0,3} {1,8} {2,8} {3,8}  {5}/{6} {4}", display_ttl, p0, p1, p2, "(redacted)", nodedata[a].succesful_pings, nodedata[a].total_pings);
                        }
                        else
                        {
                            Console.WriteLine("{0,3} {1,8} {2,8} {3,8}  {5}/{6} {4}", display_ttl, p0, p1, p2, a.ToString(), nodedata[a].succesful_pings, nodedata[a].total_pings);
                        }
                    }
                    else
                    {
                        if (nodedata[a].hopcount <= settings.Redact) {
                            Console.WriteLine("{0,3} {1,8} {2,8} {3,8}  {6}/{7} {4} {5}", display_ttl, p0, p1, p2, "(redacted)" , "", nodedata[a].succesful_pings, nodedata[a].total_pings);
                        }
                        else
                        {
                            Console.WriteLine("{0,3} {1,8} {2,8} {3,8}  {6}/{7} {4} [{5}]", display_ttl, p0, p1, p2, nodedata[a].hostname, a.ToString(), nodedata[a].succesful_pings, nodedata[a].total_pings);
                        }
                    }
                }

                
                times_done += 1;
                // cut out for -n
                if (times_done >= settings.TimesToDo && settings.TimesToDo != -1)
                {
                    settings.THREADS_DIE = true;
                    Environment.Exit(0);
                }
                
                int sleepytime = 0;
                while (sleepytime <= settings.Interval * 1000)
                {
                    System.Threading.Thread.Sleep(10);
                    sleepytime += 10;
                    if (settings.THREADS_DIE)
                    {
                        sleepytime += settings.Interval * 1000;

                    }
                }
            }
            
        }
    }

    public class TraceRoute
    {
        private const string Data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public static IEnumerable<IPAddress> GetTraceRoute(string hostNameOrAddress, ref Dictionary<IPAddress, tmpNode> trdata)
        {    
            return GetTraceRoute(hostNameOrAddress, 1, ref trdata);
        }

        private static IEnumerable<IPAddress> GetTraceRoute(string hostNameOrAddress, int ttl, ref Dictionary<IPAddress, tmpNode> trdata)
        {

            
            Ping pinger = new Ping();
            PingOptions pingerOptions = new PingOptions(ttl, true);
            int timeout = settings.WaitTimeout;
            byte[] buffer = Encoding.ASCII.GetBytes(Data);
            PingReply reply = default(PingReply);

            //int max_ttl = 32; // fix this to be an option
            int max_ttl = settings.NumberOfHops;

            try
            {
                reply = pinger.Send(hostNameOrAddress, timeout, buffer, pingerOptions);
            }
            catch
            {
                Console.WriteLine("Ping error.  Did you specify a target?");
                Environment.Exit(1);
                
            }

            tmpNode tr_reply_data = new tmpNode(ttl, reply.Status, reply.RoundtripTime);
            try
            {
                trdata.Add(reply.Address, tr_reply_data);
            }
            catch
            {
            }

            if (settings.SuppressOutput == false)
            {
                Console.Write(".");
            }

            List<IPAddress> result = new List<IPAddress>();

            switch (reply.Status)
            {
                // packet succeeded!
                case IPStatus.Success:
                    result.Add(reply.Address);
                    break;

                // packet failed in a don't-recurse way
                //case IPStatus.DestinationProhibited:
                case IPStatus.DestinationProtocolUnreachable:
                case IPStatus.DestinationHostUnreachable:
                case IPStatus.DestinationNetworkUnreachable:
                case IPStatus.DestinationPortUnreachable:
                case IPStatus.DestinationUnreachable:
                    result.Add(reply.Address);
                    break;

                // packet failed due to ttl, good!
                case IPStatus.TtlExpired:
                case IPStatus.TimeExceeded:

                    result.Add(reply.Address);
                    if (ttl < max_ttl)
                    {
                        IEnumerable<IPAddress> tempResult = default(IEnumerable<IPAddress>);
                        tempResult = GetTraceRoute(hostNameOrAddress, ttl + 1, ref trdata);
                        result.AddRange(tempResult);
                    }
                    break;

                
                // packet is otherwise screwed up.  Probably nicer to stop the tr on some of these that are purely local rather
                // than going all the way to max_ttl but I don't know off the top of my head which could be returned remotely
                case IPStatus.BadDestination:
                case IPStatus.BadHeader:
                case IPStatus.BadOption:
                case IPStatus.BadRoute:
                case IPStatus.HardwareError:
                case IPStatus.DestinationScopeMismatch:
                case IPStatus.IcmpError:
                case IPStatus.NoResources:
                case IPStatus.PacketTooBig:
                case IPStatus.ParameterProblem:
                case IPStatus.SourceQuench:
                case IPStatus.TimedOut:
                case IPStatus.TtlReassemblyTimeExceeded:
                case IPStatus.Unknown:
                case IPStatus.UnrecognizedNextHeader:
                default:
                    if (ttl < max_ttl)
                    {
                        IEnumerable<IPAddress> tempResult = default(IEnumerable<IPAddress>);
                        tempResult = GetTraceRoute(hostNameOrAddress, ttl + 1, ref trdata);
                        result.AddRange(tempResult);
                    }
                    break;
            }

            
            return result;
        }
    }
}
