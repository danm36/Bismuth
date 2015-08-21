using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Bismuth
{
    public static class BismuthThreadPool
    {
        const int MAX_THREADS = 32768; //Temp?
        const int DEFAULT_THREAD_COUNT = 200;

        private class BismuthThreadPoolTask
        {
            Action TaskA = null;
            Action<object> TaskP = null;
            object Param = null;

            public BismuthThreadPoolTask(Action task)
            {
                TaskA = task;
            }

            public BismuthThreadPoolTask(Action<object> task, object param = null)
            {
                TaskP = task;
                Param = param;
            }

            public void Invoke()
            {
                if (TaskP != null)
                    TaskP(Param);
                else if (TaskA != null)
                    TaskA();
            }
        }

        static bool Disposing = false, Disposed = false;

        static LinkedList<Thread> WorkerThreads = new LinkedList<Thread>();
        static LinkedList<BismuthThreadPoolTask> Tasks = new LinkedList<BismuthThreadPoolTask>();
        static int ActiveThreads = 0;

        static bool hasSetup = false;
        public static void Setup()
        {
            if(hasSetup)
                return;
            hasSetup = true;

            int maxThreadCount = Math.Min(BismuthConfig.GetConfigValue<int>("ThreadPool.MaxThreads", DEFAULT_THREAD_COUNT), MAX_THREADS);
            if (maxThreadCount <= 0)
            {
                LogManager.Warn("ThreadPool", "Invalid thread pool count '" + maxThreadCount + "', defaulting to " + DEFAULT_THREAD_COUNT + " threads");
                maxThreadCount = DEFAULT_THREAD_COUNT;
            }

            LogManager.Log("ThreadPool", "Starting thread pool with " + maxThreadCount + " threads");

            for(int i = 0; i < maxThreadCount; ++i)
            {
                Thread worker = new Thread(WorkerFunction) { Name = "BismuthThread" + i.ToString().PadLeft(5, '0') };
                worker.Start();
                WorkerThreads.AddLast(worker);
            }

            LogManager.Log("ThreadPool", "Thread pool started");
        }

        public static void Shutdown()
        {
            if (!hasSetup || Disposing || Disposed)
                return;

            Disposing = true;

            lock (Tasks)
            {
                while (Tasks.Count > 0)
                    Monitor.Wait(Tasks);

                Monitor.PulseAll(Tasks);
            }

            foreach (Thread Worker in WorkerThreads)
                Worker.Join();
        }

        public static bool StartThread(Action task)
        {
            lock (Tasks)
            {
                if (Disposing || Disposed)
                    return false;

                Tasks.AddLast(new BismuthThreadPoolTask(task));
                Monitor.PulseAll(Tasks);
            }

            return true;
        }

        public static bool StartThread(Action<object> task, object param = null)
        {
            lock (Tasks)
            {
                if (Disposing || Disposed)
                    return false;

                Tasks.AddLast(new BismuthThreadPoolTask(task, param));
                Monitor.PulseAll(Tasks);
            }

            return true;
        }

        static void WorkerFunction()
        {
            BismuthThreadPoolTask MyTask = null;

            while (true)
            {
                if (Disposed)
                    return;

                lock (Tasks)
                {
                    while (true)
                    {
                        if (WorkerThreads.First != null && Thread.CurrentThread == WorkerThreads.First.Value && Tasks.Count > 0)
                        {
                            MyTask = Tasks.First.Value;
                            Tasks.RemoveFirst();
                            WorkerThreads.RemoveFirst();
                            Monitor.PulseAll(Tasks);
                            break;
                        }
                        Monitor.Wait(Tasks);
                    }
                }
                Interlocked.Increment(ref ActiveThreads);
                MyTask.Invoke();
                Interlocked.Decrement(ref ActiveThreads);
                lock (Tasks)
                {
                    WorkerThreads.AddLast(Thread.CurrentThread);
                }
                MyTask = null;
            }
        }

        public static int GetTotalPossibleThreads() { lock (WorkerThreads) return WorkerThreads.Count; }
        public static int GetActiveThreadCount() { return ActiveThreads; }
        public static int GetPendingTaskCount() { lock (Tasks) return Tasks.Count; }
    }
}
