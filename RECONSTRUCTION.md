# PrimeNumberGenerator2010 â€” Reconstruction Record

**Reconstructed:** June 29, 2026  
**Original written:** circa 2010 (Windows XP, .NET 3.5/4.0 era)  
**Contributor:** Founder

---

## Origin

This project began as a response to a job interview question about multicore threading.

After the interview, the author went home and built a working proof of concept to answer
the question himself â€” not to submit, but because he needed to *know*. He chose prime
number generation as the vehicle: simple enough to implement quickly, CPU-bound enough
to actually stress multiple cores, and concrete enough to measure.

**The prime numbers were never the point. Multicore threading was the point.**
The generator was the simplest problem that would force the solution.

---

## What This Is

This project is a faithful recreation of that program â€” rebuilt from memory in a single
session sixteen years later.

The code was not reverse-engineered, recovered from backup, or regenerated from a
specification. It was reconstructed step by step through directed recollection â€” the author
described each piece as they remembered it, and the code was written to match that
description. Where memory was uncertain, the simplest interpretation consistent with
the recalled behavior was used.

---

## Session Reconstruction Log

The following is the order in which the program was remembered and built.

### 1. Store file discovery
The program looks for existing `PrimeStore*.txt` files in the executable directory.
If none exist, it creates `PrimeStore01.txt`. If multiple exist, it loads the
highest-numbered file (alphabetic sort, zero-padded names) and resumes from there.
All store files are loaded into memory so the prime list is complete on resume.

### 2. Seeds
Rather than letting the trial division loop discover 2, 3, and 5 on its own, they
are written to the store file manually as seeds so the digit filter is safe from
the first candidate onward.

### 3. Trial division loop
The generation loop starts at the next candidate after the last known prime.
For each candidate `n`, it divides by every known prime `p` in order.
**The stopping condition is `p > n / 2`** â€” this is the rule the author recalled from
the original. (The mathematically optimal stop is `p > sqrt(n)`, which is ~650x
fewer tests at scale, but that was not the original implementation.)

### 4. Digit filter (two variants built)
The author recalled a filter that skipped candidates by their last digit.

Two expressions were implemented so both are preserved:
- **Whitelist** (`TestPrimeWhitelist`): only allow last digits 1, 3, 7, 9.
- **Blacklist** (`TestPrimeBlacklist`): exclude last digits 0, 2, 4, 5, 6, 8.

These are mathematically identical. The whitelist short-circuits slightly faster
in practice (fewer comparisons on average). Currently wired to whitelist.

Author's note: the open-source published version will use the blacklist (the
original expression); the whitelist is the optimized form for the running build.

### 5. Display
Each prime is printed with its digit count (length of the decimal string) and
the elapsed time at the moment it was found.

### 6. File rotation
When a store file reaches 1 GB, the program pauses and waits for a keypress before
creating the next file (`PrimeStore02.txt`, etc.). The author recalled this pause â€” it
was intentional, not a bug.

### 7. Ctrl+C summary
A cancel handler prints elapsed time, last prime found, and total count before exit.

### 8. Hardware detection
On startup, before generation begins:
- Machine name and OS version are displayed.
- Total logical processor count is shown (`Environment.ProcessorCount`).
- One core is reserved for the OS; the remainder are reported as worker cores.
- The program pauses for a keypress so the user can read this before generation starts.

### 9. Threading (core binding)
Worker threads are created â€” one per available worker core.
Each thread is bound to a specific virtual core via
`SetThreadAffinityMask` / `GetCurrentThread` (Windows kernel32 P/Invoke),
skipping core 0 which is left for the OS.

The parallel batch model:
- Each generation step fires one candidate per worker thread simultaneously.
- All threads test against the same prime list (read-only during the batch).
- The main thread waits for all threads to complete before collecting results.
- Results are collected **in order** (candidate index, not arrival order) so the
  prime list remains correctly sequenced.
- Only after results are collected and added to the prime list does the next batch
  begin â€” this ensures no thread ever tests against an incomplete list.

---

## What Changed From the Original

| Area | Original (2010) | This Recreation |
|---|---|---|
| Runtime | .NET 3.5 / 4.0 | .NET 8.0 (same C# syntax, identical behavior) |
| OS | Windows XP | Windows 10 / 11 |
| Integer type | Likely `int` (32-bit, max ~2.1B) | `long` (64-bit, max ~9.2 quintillion) |
| File format | `.sln` (VS 2008/2010 style) | `.sln` (classic format, manually written) |
| Threading | Recalled as thread-per-core | Implemented as persistent thread pool with batch dispatch |

The integer type upgrade (`int` â†’ `long`) is the one material behavioral difference.
The original would have hit the 2.1 billion ceiling; this version runs indefinitely.

---

## What Was NOT Added

The following were explicitly considered and left out to preserve fidelity to the
original:

- **Miller-Rabin or any probabilistic test** â€” pure trial division only.
- **`BigInteger`** â€” not how the author recalls implementing it.
- **Any TPL / `Task` / `async`** â€” Thread + AutoResetEvent, 2010 style.

### Post-reconstruction addition: `sqrt(n)` stopping condition

The original used `p > n / 2` as the stopping rule. During this session, after
benchmarking confirmed the performance cost (3â€“7 day estimate to fill 1 GB vs.
8â€“12 hours with sqrt), the stopping condition was updated to `p * p > n`.

This is the one deliberate improvement over the recalled original. It does not
change correctness â€” only speed. It is documented here so the delta from the
pure memory reconstruction is transparent.

---

## Notes on the Whitelist / Blacklist Decision

The digit filter was remembered as a blacklist ("exclude these endings").
The whitelist form was added for comparison and confirmed to be slightly faster.
Both are in the code. Author's ruling: the open-source release will use blacklist
(the original form), the active/personal build uses whitelist.

---

## Status

- **Full prime dataset** â€” all 37,607,912,018 primes under 1 trillion have been generated
  by a separate proprietary engine developed by BeltUp LLC. The dataset is pending public
  release on Zenodo; a download link will be added to the README when available.
- **API** â€” a range-query endpoint (*is N prime? Nth prime? primes in [A, B]*) is planned,
  backed by the full dataset.
- **Papers** â€” a formal write-up of the reconstruction process and threading model is
  in progress separately.

---

## Session Notes

The code was built incrementally from Founder's verbal descriptions of a program
he wrote in 2010. No existing source code was recovered or provided. Each feature was
described from memory, implemented, and confirmed before moving to the next.

This document is an accurate record of what was built and how. Commit history
on GitHub provides the full audit trail of when each piece was added.

---

*Session date: June 29, 2026*
