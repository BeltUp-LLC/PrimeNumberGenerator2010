# PrimeNumberGenerator2010

A C# multicore prime number generator — open source community edition.

Run it to generate your own prime numbers on your own machine. This is a proof-of-concept
trial division engine, originally written in 2010 and reconstructed from memory in 2026.

---

## Download and Run

**Requirements:** .NET 8 SDK, Windows

```
git clone https://github.com/BeltUp-LLC/PrimeNumberGenerator2010.git
cd PrimeNumberGenerator2010
dotnet run
```

Or open `PrimeNumberGenerator2010.csproj` directly in Visual Studio and press **F5**.

On first run it seeds 2, 3, 5 and begins generating from 6 onward. Results are written
to rotating 1 GB text files (`PrimeStore01.txt`, `PrimeStore02.txt`, ...) in the output
directory. Restart anytime — it resumes from the last known prime automatically.

Press **Ctrl+C** to stop and print a summary.

---

## Benchmarks

All runs measured fresh from zero on the same machine (32-core, workers capped per test).
Rate is range covered per minute at the 0–10M starting band.

| Workers | Range @ 3 min | Rate (M/min) | Notes |
|---|---|---|---|
| 6 | 9.1M | 3.03 | common mainstream CPU (Ryzen 5 / Core i5) |
| **8** | **10.0M** | **3.33** | **sweet spot — best throughput at low range** |
| 12 | 8.6M | 2.88 | serial bottleneck limits gain; slower than 8 |

The serial bottleneck is the main thread: collecting results, appending to the prime list,
and writing to the store file. Throughput increases significantly at higher ranges (50M+)
as prime density decreases and the main thread has less work per unit range.

---

## Sample Run

8 workers, measured fresh start.

```
No store found. Created: PrimeStore01.txt

Machine      : WORKSTATION-01
OS           : Microsoft Windows NT 10.0.26200.0
Total cores  : 16
Reserved (OS): 1
Worker cores : 8

Ready: 8 worker cores available for generation.

Press any key to begin generation...

Starting 8 worker threads...
  Thread 0 -> core 1
  Thread 1 -> core 2
  ...
  Thread 7 -> core 8

2  [1]  0.02s
3  [1]  0.02s
5  [1]  0.02s
...
10000103  [8]  2m 59s
10000121  [8]  2m 59s
^C
----------------------------------------
Stopped.
Elapsed   : 3m 02s
Last prime: 10000141
Count     : 664,579
----------------------------------------
```

**Built-in correctness check:** π(10,000,000) = **664,579** exactly — a known mathematical
constant. The engine hits it on a fresh run with no tuning.

---

## How It Works

Trial division against a growing list of known primes. Each candidate `n` is divided by
every prime `p`; if `p * p > n` with no divisor found, `n` is prime.

Candidates are dispatched in parallel — one per CPU core (core 0 reserved for the OS).
Each worker thread is pinned to a specific core via Windows core affinity. Results are
collected in order so the prime list stays correctly sequenced.

A last-digit filter eliminates ~60% of candidates before any division begins.

---

## Project Structure

| File | Purpose |
|---|---|
| `Program.cs` | Entry point, store management, primality tests, generation loop |
| `WorkerPool.cs` | Thread creation, core affinity binding, batch dispatch |
| `RECONSTRUCTION.md` | Full record of the 2026 reconstruction session |
| `CONTRIBUTORS.md` | Contributor list |

---

## Origin

Originally written in 2010 as a proof of concept after a job interview question about
multicore threading. Prime generation was the vehicle — threading was the point.

Reconstructed from memory in 2026 and released here as a contribution to the community.
See [RECONSTRUCTION.md](RECONSTRUCTION.md) for the full record.

---

## Related

→ **[prime-data](https://github.com/BeltUp-LLC/prime-data)** — the full prime number
dataset (37.6 billion primes under 1 trillion) and decoder, produced by a separate
proprietary engine developed by BeltUp LLC.

---

## License

MIT — see [LICENSE](LICENSE) for full text.  
Copyright © 2026 BeltUp LLC
