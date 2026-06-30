using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace PrimeNumberGenerator2010
{
    class Program
    {
        const long MaxFileBytes = 1L * 1024 * 1024 * 1024;   // 1 GB per store file

        static long         _primeCount = 0;
        static long         _lastPrime  = 0;
        static Stopwatch    _sw         = new Stopwatch();
        static bool         _silent     = false;   // --silent: suppress per-prime output
        static long         _stopAt     = 0;       // --to N: auto-stop when last prime >= N
        static StreamWriter? _writer     = null;    // held for Ctrl+C flush

        // ====================================================================
        //  Main
        // ====================================================================
        static void Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--silent") _silent = true;
                if (args[i] == "--to" && i + 1 < args.Length)
                    long.TryParse(args[i + 1], out _stopAt);
            }

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _writer?.Flush();
                _writer?.Close();
                Console.WriteLine();
                Console.WriteLine("----------------------------------------");
                Console.WriteLine("Stopped.");
                Console.WriteLine("Elapsed   : " + FormatElapsed(_sw.Elapsed));
                Console.WriteLine("Last prime: " + _lastPrime);
                Console.WriteLine("Count     : " + _primeCount.ToString("N0"));
                Console.WriteLine("----------------------------------------");
                Environment.Exit(0);
            };

            string folder    = AppDomain.CurrentDomain.BaseDirectory;
            string storeFile = FindOrCreateStore(folder);

            PrintHardwareInfo(out int workerCores);

            Console.Write("Press any key to begin generation...");
            if (!Console.IsInputRedirected) Console.ReadKey(true);
            Console.WriteLine();
            Console.WriteLine();

            _sw.Start();
            GetPrime(storeFile, folder, workerCores);
        }

        // ====================================================================
        //  GetPrime
        // ====================================================================
        static void GetPrime(string storeFile, string folder, int workerCores)
        {
            List<long> primes = LoadAllStores(folder);
            _primeCount = primes.Count;

            int  fileNumber   = ParseFileNumber(storeFile);
            long bytesWritten = new FileInfo(storeFile).Length;
            long start        = SeedOrResume(primes, storeFile, ref bytesWritten);

            WorkerPool pool = new WorkerPool(workerCores, primes, TestPrime);
            pool.Start();
            Thread.Sleep(200);
            Console.WriteLine();

            _writer = new StreamWriter(storeFile, append: true);

            for (long n = start; ; n += workerCores)
            {
                pool.Dispatch(n);
                pool.Wait();
                CollectBatch(pool, workerCores, n, primes,
                    ref _writer, ref storeFile, ref fileNumber, ref bytesWritten, folder);

                if (_stopAt > 0 && _lastPrime >= _stopAt)
                {
                    _writer.Flush();
                    _writer.Close();
                    Console.WriteLine();
                    Console.WriteLine("========================================");
                    Console.WriteLine("Target reached.");
                    Console.WriteLine("Elapsed   : " + FormatElapsed(_sw.Elapsed));
                    Console.WriteLine("Last prime: " + _lastPrime.ToString("N0"));
                    Console.WriteLine("Count     : " + _primeCount.ToString("N0"));
                    Console.WriteLine("========================================");
                    Environment.Exit(0);
                }
            }
        }

        // ====================================================================
        //  Store management
        // ====================================================================
        static string FindOrCreateStore(string folder)
        {
            string[] found = Directory.GetFiles(folder, "PrimeStore*.txt");
            if (found.Length == 0)
            {
                string path = Path.Combine(folder, "PrimeStore01.txt");
                File.WriteAllText(path, string.Empty);
                Console.WriteLine("No store found. Created: " + Path.GetFileName(path));
                return path;
            }
            Array.Sort(found);
            string store = found[found.Length - 1];
            Console.WriteLine("Loaded store: " + Path.GetFileName(store));
            return store;
        }

        static List<long> LoadAllStores(string folder)
        {
            List<long> primes = new List<long>();
            string[] files = Directory.GetFiles(folder, "PrimeStore*.txt");
            Array.Sort(files);
            foreach (string f in files)
            {
                foreach (string line in File.ReadAllLines(f))
                {
                    long p;
                    if (long.TryParse(line.Trim(), out p) && p > 1)
                        primes.Add(p);
                }
            }
            return primes;
        }

        static long SeedOrResume(List<long> primes, string storeFile, ref long bytesWritten)
        {
            if (primes.Count == 0)
            {
                long[] seeds = { 2, 3, 5 };
                foreach (long s in seeds)
                {
                    primes.Add(s);
                    if (!_silent) Display(s, _sw.Elapsed);
                }
                _primeCount = seeds.Length;
                _lastPrime  = 5;

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (long s in seeds) sb.AppendLine(s.ToString());
                File.WriteAllText(storeFile, sb.ToString());
                bytesWritten = new FileInfo(storeFile).Length;
                return 6;
            }

            _lastPrime = primes[primes.Count - 1];
            Console.WriteLine("Resuming after " + _lastPrime + "  (" + primes.Count + " primes loaded)");
            return _lastPrime + 1;
        }

        static void CollectBatch(WorkerPool pool, int workerCores, long n, List<long> primes,
            ref StreamWriter writer, ref string storeFile, ref int fileNumber,
            ref long bytesWritten, string folder)
        {
            for (int i = 0; i < workerCores; i++)
            {
                if (!pool.GetResult(i)) continue;

                long   prime  = n + i;
                string numStr = prime.ToString();

                primes.Add(prime);
                _primeCount++;
                _lastPrime = prime;

                if (_silent)
                {
                    // Milestone output every 1M primes
                    if (_primeCount % 1_000_000 == 0)
                        Console.WriteLine("[" + (_primeCount / 1_000_000) + "M primes]  last: "
                            + prime.ToString("N0") + "  " + FormatElapsed(_sw.Elapsed));
                }
                else
                {
                    Display(prime, _sw.Elapsed);
                }

                writer.WriteLine(numStr);

                // Silent mode: flush every 10K primes; normal mode: flush every prime
                if (!_silent || _primeCount % 10_000 == 0)
                    writer.Flush();

                bytesWritten += numStr.Length + Environment.NewLine.Length;

                if (bytesWritten >= MaxFileBytes)
                    RotateFile(ref writer, ref storeFile, ref fileNumber, ref bytesWritten, folder);
            }
        }

        static void RotateFile(ref StreamWriter writer, ref string storeFile,
            ref int fileNumber, ref long bytesWritten, string folder)
        {
            writer.Flush();
            writer.Close();
            Console.WriteLine();
            Console.WriteLine("--- File limit reached: " + Path.GetFileName(storeFile) + " ---");

            if (!_silent)
            {
                Console.Write("Press any key to create next store file...");
                Console.ReadKey(true);
                Console.WriteLine();
            }

            fileNumber++;
            storeFile = Path.Combine(folder, "PrimeStore" + fileNumber.ToString("D2") + ".txt");
            File.WriteAllText(storeFile, string.Empty);
            Console.WriteLine("Created: " + Path.GetFileName(storeFile));

            writer       = new StreamWriter(storeFile, append: false);
            _writer      = writer;
            bytesWritten = 0;
        }

        // ====================================================================
        //  Primality tests
        // ====================================================================
        static bool TestPrime(long n, List<long> primes)
        {
            return TestPrimeWhitelist(n, primes);
            // return TestPrimeBlacklist(n, primes);
        }

        static bool TestPrimeWhitelist(long n, List<long> primes)
        {
            int d = (int)(n % 10);
            if (d != 1 && d != 3 && d != 7 && d != 9) return false;

            foreach (long p in primes)
            {
                if (n % p == 0) return false;
                if (p * p > n)  break;
            }
            return true;
        }

        static bool TestPrimeBlacklist(long n, List<long> primes)
        {
            int d = (int)(n % 10);
            if (d == 0 || d == 2 || d == 4 || d == 5 || d == 6 || d == 8) return false;

            foreach (long p in primes)
            {
                if (n % p == 0) return false;
                if (p * p > n)  break;
            }
            return true;
        }

        // ====================================================================
        //  Helpers
        // ====================================================================
        static void PrintHardwareInfo(out int workerCores)
        {
            int total = Environment.ProcessorCount;
            workerCores = Math.Min(total - 1, 8);

            Console.WriteLine();
            Console.WriteLine("Machine      : " + Environment.MachineName);
            Console.WriteLine("OS           : " + Environment.OSVersion);
            Console.WriteLine("Total cores  : " + total);
            Console.WriteLine("Reserved (OS): 1");
            Console.WriteLine("Worker cores : " + workerCores);
            Console.WriteLine();

            if (_silent && _stopAt > 0)
                Console.WriteLine("Mode         : silent, target " + _stopAt.ToString("N0"));

            if (workerCores < 2)
                Console.WriteLine("Note: only " + workerCores + " worker core -- running single-threaded.");
            else
                Console.WriteLine("Ready: " + workerCores + " worker cores available for generation.");

            Console.WriteLine();
        }

        static void Display(long n, TimeSpan elapsed)
        {
            Console.WriteLine(n + "  [" + n.ToString().Length + "]  " + FormatElapsed(elapsed));
        }

        static string FormatElapsed(TimeSpan t)
        {
            if (t.TotalSeconds < 60)  return t.TotalSeconds.ToString("F2") + "s";
            if (t.TotalMinutes < 60)  return ((int)t.TotalMinutes) + "m " + t.Seconds + "s";
            return ((int)t.TotalHours) + "h " + t.Minutes + "m " + t.Seconds + "s";
        }

        static int ParseFileNumber(string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            string num  = name.Replace("PrimeStore", "");
            int result;
            return int.TryParse(num, out result) ? result : 1;
        }
    }
}
