// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    public const int Pass = 100;
    public const int Fail = -1;

    static int Main()
    {
        SimpleReadWriteThreadStaticTest.Run(42, "SimpleReadWriteThreadStatic");

        // TODO: After issue https://github.com/dotnet/corert/issues/2695 is fixed, move FinalizeTest to run at the end
        if (FinalizeTest.Run() != Pass)
            return Fail;

        ThreadStaticsTestWithTasks.Run();

        if (ThreadTest.Run() != Pass)
            return Fail;

        return Pass;
    }
}

class FinalizeTest
{
    public static bool visited = false;
    public class Dummy
    {
        ~Dummy()
        {
            FinalizeTest.visited = true;
        }
    }

    public static int Run()
    {
        int iterationCount = 0;
        while (!visited && iterationCount++ < 10000)
        {
            GC.KeepAlive(new Dummy());
            GC.Collect();
        }

        if (visited)
        {
            Console.WriteLine("FinalizeTest passed");
            return Program.Pass;
        }
        else
        {
            Console.WriteLine("FinalizeTest failed");
            return Program.Fail;
        }
    }
}

class SimpleReadWriteThreadStaticTest
{
    public static void Run(int intValue, string stringValue)
    {
        NonGenericReadWriteThreadStaticsTest(intValue, "NonGeneric" + stringValue);
        GenericReadWriteThreadStaticsTest(intValue + 1, "Generic" + stringValue);
    }

    class NonGenericType
    {
        [ThreadStatic]
        public static int IntValue;

        [ThreadStatic]
        public static string StringValue;
    }

    class GenericType<T, V>
    {
        [ThreadStatic]
        public static T ValueT;

        [ThreadStatic]
        public static V ValueV;
    }

    static void NonGenericReadWriteThreadStaticsTest(int intValue, string stringValue)
    {
        NonGenericType.IntValue = intValue;
        NonGenericType.StringValue = stringValue;

        if (NonGenericType.IntValue != intValue)
        {
            throw new Exception("SimpleReadWriteThreadStaticsTest: wrong integer value: " + NonGenericType.IntValue.ToString());
        }

        if (NonGenericType.StringValue != stringValue)
        {
            throw new Exception("SimpleReadWriteThreadStaticsTest: wrong string value: " + NonGenericType.StringValue);
        }
    }

    static void GenericReadWriteThreadStaticsTest(int intValue, string stringValue)
    {
        GenericType<int, string>.ValueT = intValue;
        GenericType<int, string>.ValueV = stringValue;

        if (GenericType<int, string>.ValueT != intValue)
        {
            throw new Exception("GenericReadWriteThreadStaticsTest1a: wrong integer value: " + GenericType<int, string>.ValueT.ToString());
        }

        if (GenericType<int, string>.ValueV != stringValue)
        {
            throw new Exception("GenericReadWriteThreadStaticsTest1b: wrong string value: " + GenericType<int, string>.ValueV);
        }

        intValue++;
        GenericType<int, int>.ValueT = intValue;
        GenericType<int, int>.ValueV = intValue + 1;

        if (GenericType<int, int>.ValueT != intValue)
        {
            throw new Exception("GenericReadWriteThreadStaticsTest2a: wrong integer value: " + GenericType<int, string>.ValueT.ToString());
        }

        if (GenericType<int, int>.ValueV != (intValue + 1))
        {
            throw new Exception("GenericReadWriteThreadStaticsTest2b: wrong integer value: " + GenericType<int, string>.ValueV.ToString());
        }

        GenericType<string, string>.ValueT = stringValue + "a";
        GenericType<string, string>.ValueV = stringValue + "b";

        if (GenericType<string, string>.ValueT != (stringValue + "a"))
        {
            throw new Exception("GenericReadWriteThreadStaticsTest3a: wrong string value: " + GenericType<string, string>.ValueT);
        }

        if (GenericType<string, string>.ValueV != (stringValue + "b"))
        {
            throw new Exception("GenericReadWriteThreadStaticsTest3b: wrong string value: " + GenericType<string, string>.ValueV);
        }
    }
}

class ThreadStaticsTestWithTasks
{
    static object lockObject = new object();
    const int TotalTaskCount = 32;

    public static void Run()
    {
        Task[] tasks = new Task[TotalTaskCount];
        for (int i = 0; i < tasks.Length; ++i)
        {
            tasks[i] = Task.Factory.StartNew((param) =>
            {
                int index = (int)param;
                int intTestValue = index * 10;
                string stringTestValue = "ThreadStaticsTestWithTasks" + index;

                // Try to run the on every other task
                if ((index % 2) == 0)
                {
                    lock (lockObject)
                    {
                        SimpleReadWriteThreadStaticTest.Run(intTestValue, stringTestValue);
                    }
                }
                else
                {
                    SimpleReadWriteThreadStaticTest.Run(intTestValue, stringTestValue);
                }
            }, i);
        }
        for (int i = 0; i < tasks.Length; ++i)
        {
            tasks[i].Wait();
        }
    }
}

class ThreadTest
{
    private static readonly List<Thread> s_startedThreads = new List<Thread>();

    private static int s_passed;
    private static int s_failed;

    private static void Expect(bool condition, string message)
    {
        if (condition)
        {
            Interlocked.Increment(ref s_passed);
        }
        else
        {
            Interlocked.Increment(ref s_failed);
            Console.WriteLine("ERROR: " + message);
        }
    }

    private static void ExpectException<T>(Action action, string message)
    {
        Exception ex = null;
        try
        {
            action();
        }
        catch (Exception e)
        {
            ex = e;
        }

        if (!(ex is T))
        {
            message += string.Format(" (caught {0})", (ex == null) ? "no exception" : ex.GetType().Name);
        }
        Expect(ex is T, message);
    }

    private static void ExpectPassed(string testName, int expectedPassed)
    {
        // Wait for all started threads to finish execution
        foreach (Thread t in s_startedThreads)
        {
            t.Join();
        }

        s_startedThreads.Clear();

        Expect(s_passed == expectedPassed, string.Format("{0}: Expected s_passed == {1}, got {2}", testName, expectedPassed, s_passed));
        s_passed = 0;
    }

    private static void TestStartMethod()
    {
        // Case 1: new Thread(ThreadStart).Start()
        var t1 = new Thread(() => Expect(true, null));
        t1.Start();
        s_startedThreads.Add(t1);

        // Case 2: new Thread(ThreadStart).Start(parameter)
        var t2 = new Thread(() => Expect(false, "This thread must not be started"));
        // InvalidOperationException: The thread was created with a ThreadStart delegate that does not accept a parameter.
        ExpectException<InvalidOperationException>(() => t2.Start(null), "Expected InvalidOperationException for t2.Start()");

        // Case 3: new Thread(ParameterizedThreadStart).Start()
        var t3 = new Thread(obj => Expect(obj == null, "Expected obj == null"));
        t3.Start();
        s_startedThreads.Add(t3);

        // Case 4: new Thread(ParameterizedThreadStart).Start(parameter)
        var t4 = new Thread(obj => Expect((int)obj == 42, "Expected (int)obj == 42"));
        t4.Start(42);
        s_startedThreads.Add(t4);

        // Start an unstarted resurrected thread.
        // CoreCLR: ThreadStateException, CoreRT: no exception.
        Thread unstartedResurrected = Resurrector.CreateUnstartedResurrected();
        unstartedResurrected.Start();
        s_startedThreads.Add(unstartedResurrected);

        // Threads cannot started more than once
        t1.Join();
        ExpectException<ThreadStateException>(() => t1.Start(), "Expected ThreadStateException for t1.Start()");

        ExpectException<ThreadStateException>(() => Thread.CurrentThread.Start(),
            "Expected ThreadStateException for CurrentThread.Start()");

        Thread stoppedResurrected = Resurrector.CreateStoppedResurrected();
        ExpectException<ThreadStateException>(() => stoppedResurrected.Start(),
            "Expected ThreadStateException for stoppedResurrected.Start()");

        ExpectPassed(nameof(TestStartMethod), 7);
    }

    private static void TestJoinMethod()
    {
        var t = new Thread(() => { });
        ExpectException<InvalidOperationException>(() => t.Start(null), "Expected InvalidOperationException for t.Start()");
        ExpectException<ThreadStateException>(() => t.Join(), "Expected ThreadStateException for t.Join()");

        Expect(!Thread.CurrentThread.Join(1), "CurrentThread.Join(1) must return false");

        ExpectPassed(nameof(TestJoinMethod), 3);
    }

    private static void TestCurrentThreadProperty()
    {
        Thread t = null;
        t = new Thread(() => Expect(Thread.CurrentThread == t, "Expected CurrentThread == t on thread t"));
        t.Start();
        s_startedThreads.Add(t);

        Expect(Thread.CurrentThread != t, "Expected CurrentThread != t on main thread");

        ExpectPassed(nameof(TestCurrentThreadProperty), 2);
    }

    private static void TestNameProperty()
    {
        var t = new Thread(() => { });

        t.Name = null;
        // It is OK to set the null Name multiple times
        t.Name = null;
        Expect(t.Name == null, "Expected t.Name == null");

        const string ThreadName = "My thread";
        t.Name = ThreadName;
        Expect(t.Name == ThreadName, string.Format("Expected t.Name == \"{0}\"", ThreadName));
        ExpectException<InvalidOperationException>(() => { t.Name = null; },
            "Expected InvalidOperationException setting Thread.Name back to null");

        ExpectPassed(nameof(TestNameProperty), 3);
    }

    private static void TestIsBackgroundProperty()
    {
        // Thread created using Thread.Start
        var t_event = new AutoResetEvent(false);
        var t = new Thread(() => t_event.WaitOne());

        t.Start();
        s_startedThreads.Add(t);

        Expect(!t.IsBackground, "Expected t.IsBackground == false");
        t_event.Set();
        t.Join();
        ExpectException<ThreadStateException>(() => Console.WriteLine(t.IsBackground),
            "Expected ThreadStateException for t.IsBackground");

        // Thread pool thread
        Task.Factory.StartNew(() => Expect(Thread.CurrentThread.IsBackground, "Expected IsBackground == true")).Wait();

        // Resurrected threads
        Thread unstartedResurrected = Resurrector.CreateUnstartedResurrected();
        Expect(unstartedResurrected.IsBackground == false, "Expected unstartedResurrected.IsBackground == false");

        Thread stoppedResurrected = Resurrector.CreateStoppedResurrected();
        ExpectException<ThreadStateException>(() => Console.WriteLine(stoppedResurrected.IsBackground),
            "Expected ThreadStateException for stoppedResurrected.IsBackground");

        // Main thread
        Expect(!Thread.CurrentThread.IsBackground, "Expected CurrentThread.IsBackground == false");

        ExpectPassed(nameof(TestIsBackgroundProperty), 6);
    }

    private static void TestIsThreadPoolThreadProperty()
    {
#if false   // The IsThreadPoolThread property is not in the contract version we compile against at present
        var t = new Thread(() => { });

        Expect(!t.IsThreadPoolThread, "Expected t.IsThreadPoolThread == false");
        Task.Factory.StartNew(() => Expect(Thread.CurrentThread.IsThreadPoolThread, "Expected IsThreadPoolThread == true")).Wait();
        Expect(!Thread.CurrentThread.IsThreadPoolThread, "Expected CurrentThread.IsThreadPoolThread == false");

        ExpectPassed(nameof(TestIsThreadPoolThreadProperty), 3);
#endif
    }

    private static void TestManagedThreadIdProperty()
    {
        int t_id = 0;
        var t = new Thread(() => {
            Expect(Thread.CurrentThread.ManagedThreadId == t_id, "Expected CurrentTread.ManagedThreadId == t_id on thread t");
            Expect(Environment.CurrentManagedThreadId == t_id, "Expected Environment.CurrentManagedThreadId == t_id on thread t");
        });

        t_id = t.ManagedThreadId;
        Expect(t_id != 0, "Expected t_id != 0");
        Expect(Thread.CurrentThread.ManagedThreadId != t_id, "Expected CurrentTread.ManagedThreadId != t_id on main thread");
        Expect(Environment.CurrentManagedThreadId != t_id, "Expected Environment.CurrentManagedThreadId != t_id on main thread");

        t.Start();
        s_startedThreads.Add(t);

        // Resurrected threads
        Thread unstartedResurrected = Resurrector.CreateUnstartedResurrected();
        Expect(unstartedResurrected.ManagedThreadId != 0, "Expected unstartedResurrected.ManagedThreadId != 0");

        Thread stoppedResurrected = Resurrector.CreateStoppedResurrected();
        Expect(stoppedResurrected.ManagedThreadId != 0, "Expected stoppedResurrected.ManagedThreadId != 0");

        ExpectPassed(nameof(TestManagedThreadIdProperty), 7);
    }

    private static void TestThreadStateProperty()
    {
        var t_event = new AutoResetEvent(false);
        var t = new Thread(() => t_event.WaitOne());

        Expect(t.ThreadState == ThreadState.Unstarted, "Expected t.ThreadState == ThreadState.Unstarted");
        t.Start();
        s_startedThreads.Add(t);

        Expect(t.ThreadState == ThreadState.Running || t.ThreadState == ThreadState.WaitSleepJoin,
            "Expected t.ThreadState is either ThreadState.Running or ThreadState.WaitSleepJoin");
        t_event.Set();
        t.Join();
        Expect(t.ThreadState == ThreadState.Stopped, "Expected t.ThreadState == ThreadState.Stopped");

        // Resurrected threads
        Thread unstartedResurrected = Resurrector.CreateUnstartedResurrected();
        Expect(unstartedResurrected.ThreadState == ThreadState.Unstarted,
            "Expected unstartedResurrected.ThreadState == ThreadState.Unstarted");

        Thread stoppedResurrected = Resurrector.CreateStoppedResurrected();
        Expect(stoppedResurrected.ThreadState == ThreadState.Stopped,
            "Expected stoppedResurrected.ThreadState == ThreadState.Stopped");

        ExpectPassed(nameof(TestThreadStateProperty), 5);
    }

    private static unsafe void DoStackAlloc(int size)
    {
        byte* buffer = stackalloc byte[size];
        Volatile.Write(ref buffer[0], 0);
    }

    private static void TestMaxStackSize()
    {
#if false   // The constructors with maxStackSize are not in the contract version we compile against at present
        // Allocate a 3 MiB buffer on the 4 MiB stack
        var t = new Thread(() => DoStackAlloc(3 << 20), 4 << 20);
        t.Start();
        s_startedThreads.Add(t);
#endif

        ExpectPassed(nameof(TestMaxStackSize), 0);
    }

    public static int Run()
    {
        TestStartMethod();
        TestJoinMethod();

        TestCurrentThreadProperty();
        TestNameProperty();
        TestIsBackgroundProperty();
        TestIsThreadPoolThreadProperty();
        TestManagedThreadIdProperty();
        TestThreadStateProperty();

        TestMaxStackSize();

        return (s_failed == 0) ? Program.Pass : Program.Fail;
    }

    /// <summary>
    /// Creates resurrected Thread objects.
    /// </summary>
    class Resurrector
    {
        static Thread s_unstartedResurrected;
        static Thread s_stoppedResurrected;

        bool _unstarted;
        Thread _thread = new Thread(() => { });

        Resurrector(bool unstarted)
        {
            _unstarted = unstarted;
            if (!unstarted)
            {
                _thread.Start();
                _thread.Join();
            }
        }

        ~Resurrector()
        {
            if (_unstarted)
            {
                s_unstartedResurrected = _thread;
            }
            else
            {
                s_stoppedResurrected = _thread;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void CreateInstance(bool unstarted)
        {
            GC.KeepAlive(new Resurrector(unstarted));
        }

        public static Thread CreateUnstartedResurrected()
        {
            s_unstartedResurrected = null;

            while (s_unstartedResurrected == null)
            {
                // Call twice to override the address of the first allocation on the stack (for conservative GC)
                CreateInstance(unstarted: true);
                CreateInstance(unstarted: true);

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            return s_unstartedResurrected;
        }

        public static Thread CreateStoppedResurrected()
        {
            s_stoppedResurrected = null;

            while (s_stoppedResurrected == null)
            {
                // Call twice to override the address of the first allocation on the stack (for conservative GC)
                CreateInstance(unstarted: false);
                CreateInstance(unstarted: false);

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            return s_stoppedResurrected;
        }
    }
}
