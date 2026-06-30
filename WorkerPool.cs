using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace PrimeNumberGenerator2010
{
    class WorkerPool
    {
        // ── Core affinity (Windows kernel32) ────────────────────────────────
        [DllImport("kernel32.dll")]
        static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();

        // ── Per-thread work slot ─────────────────────────────────────────────
        class WorkSlot
        {
            public long           Candidate;
            public bool           Result;
            public bool           Running   = true;
            public AutoResetEvent WorkReady = new AutoResetEvent(false);
            public AutoResetEvent WorkDone  = new AutoResetEvent(false);
        }

        readonly int                          _count;
        readonly List<long>                   _primes;
        readonly Func<long, List<long>, bool> _testPrime;
        readonly object                       _consoleLock = new object();
        readonly WorkSlot[]                   _slots;

        public int Count => _count;

        public WorkerPool(int count, List<long> primes, Func<long, List<long>, bool> testPrime)
        {
            _count     = count;
            _primes    = primes;
            _testPrime = testPrime;
            _slots     = new WorkSlot[count];
        }

        public void Start()
        {
            Console.WriteLine("Starting " + _count + " worker threads...");
            for (int i = 0; i < _count; i++)
            {
                _slots[i] = new WorkSlot();
                int      ci   = i;
                WorkSlot slot = _slots[i];
                new Thread(() => Worker(ci, slot)) { IsBackground = true }.Start();
            }
        }

        public void Dispatch(long n)
        {
            for (int i = 0; i < _count; i++)
            {
                _slots[i].Candidate = n + i;
                _slots[i].WorkReady.Set();
            }
        }

        public void Wait()
        {
            for (int i = 0; i < _count; i++)
                _slots[i].WorkDone.WaitOne();
        }

        public bool GetResult(int index)
        {
            return _slots[index].Result;
        }

        void Worker(int coreIndex, WorkSlot slot)
        {
            IntPtr mask = new IntPtr(1L << (coreIndex + 1));
            SetThreadAffinityMask(GetCurrentThread(), mask);

            lock (_consoleLock)
                Console.WriteLine("  Thread " + coreIndex + " -> core " + (coreIndex + 1));

            while (slot.Running)
            {
                slot.WorkReady.WaitOne();
                if (!slot.Running) break;
                slot.Result = _testPrime(slot.Candidate, _primes);
                slot.WorkDone.Set();
            }
        }
    }
}
