using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CorePlayground.Locks
{
    public struct SpinLockSlim
    {
        // ReSharper disable once InconsistentNaming -- just for clarity
        private const MethodImplOptions AggressiveInlining_AggressiveOpts =
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

        private volatile int _acquired; // either 1 or 0

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void Enter(ref bool taken)
        {
            // while acquired == 1, loop, then when it == 0, exit and set it to 1
            while (Interlocked.CompareExchange(ref _acquired, 1, 0) != 0)
            {
                // NOP
            }

            taken = true;
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void TryEnter(ref bool taken)
        {
            // if it acquired == 0, change it to 1 and return true, else return false
            taken = Interlocked.CompareExchange(ref _acquired, 1, 0) == 0;
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void TryEnter(ref bool taken, uint iterations)
        {
            // if it acquired == 0, change it to 1 and return true, else return false
            while (Interlocked.CompareExchange(ref _acquired, 1, 0) != 0)
            {
                if (iterations-- == 0) // postfix decrement, so no issue if iterations == 0 at first
                {
                    taken = false;
                    return;
                }
            }

            taken = true;
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void TryEnter(ref bool taken, TimeSpan timeout)
        {
            Stopwatch watch = Stopwatch.StartNew();
            // if it acquired == 0, change it to 1 and return true, else return false
            while (Interlocked.CompareExchange(ref _acquired, 1, 0) != 0)
            {
                if (watch.Elapsed >= timeout)
                {
                    taken = false;
                    return;
                }
            }

            taken = true;
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void Exit()
        {
            // release the lock - int32 write will always be atomic
            _acquired = 0;
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void Exit(bool memBarrier)
        {
            // release the lock - int32 write will always be atomic
            Exit();

            if (memBarrier)
                Thread.MemoryBarrier();
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void ExitWithBarrier()
        {
            // release the lock - int32 write will always be atomic
            Exit();

            Thread.MemoryBarrier();
        }
    }
}