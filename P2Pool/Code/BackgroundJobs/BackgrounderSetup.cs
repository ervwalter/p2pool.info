using P2Pool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using WebActivatorEx;
using WebBackgrounder;

[assembly: PostApplicationStartMethod(typeof(BackgrounderSetup), "Start")]
[assembly: ApplicationShutdownMethod(typeof(BackgrounderSetup), "Shutdown")]

namespace P2Pool
{
    public delegate void LogMessageDelegate(string message);

    public static class BackgrounderSetup
    {
        static readonly JobManager _jobManager = CreateJobWorkersManager();
        private static object _lock = new object();
        public static FifoBuffer<string> Messages = new FifoBuffer<string>(300);

        public static void Log(string message)
        {
            lock (_lock)
            {
                Messages.Add(DateTime.UtcNow.ToString() + ": &nbsp;&nbsp; " + message);
            }
        }

        public static string GetMessages()
        {
            lock (_lock)
            {
                return string.Join("<br />", Messages);
            }
        }

        public static void Start()
        {
            _jobManager.Start();
        }

        public static void Shutdown()
        {
            _jobManager.Dispose();
        }

        private static JobManager CreateJobWorkersManager()
        {
            var blockFinder = new BlockFinder();
            var statsUpdater = new StatsUpdater();

            blockFinder.LogMessage += Log;
            statsUpdater.LogMessage += Log;

            var jobs = new IJob[]
            {
                blockFinder, 
                statsUpdater
            };

            var coordinator = new SingleServerJobCoordinator();
            var manager = new JobManager(jobs, coordinator);
            manager.Fail(ex => Log(ex.Message));
            return manager;
        }
    }
}