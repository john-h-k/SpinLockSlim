using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CorePlayground.Locks
{
    [DebuggerDisplay("Acquired = {(_acquired & 1) == 1}, OwnerThreadId = {_acquired >> 1}")]
    public struct SpinLockSlimChecked
    {
        // ReSharper disable once InconsistentNaming -- just for clarity
        private const MethodImplOptions AggressiveInlining_AggressiveOpts =
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

        private volatile int _acquired; // either 1 or 0
        // high 31 bits used for Thread::ManagedThreadId - lowest bit is for actual _acquired value

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        private static void ValidateRefBoolAsFalse(bool value)
        {
            if (value)
                ThrowHelper.ThrowArgumentException("Bool must be false");
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        private void EnsureNotRecursiveEntry()
        {
            if (Thread.CurrentThread.ManagedThreadId == (_acquired >> 1))
                ThrowHelper.ThrowLockRecursionException();
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        private void EnsureFalseAndNotRecursiveEntry(bool value)
        {
            ValidateRefBoolAsFalse(value);
            EnsureNotRecursiveEntry();
        }

        // uint conversions important - prevent sign being preserved
        private static int NewAcquiredValue
        {
            [MethodImpl(AggressiveInlining_AggressiveOpts)]
            get => unchecked((int) (1 | ((uint) Thread.CurrentThread.ManagedThreadId << 1)));
        }
        
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void Enter(ref bool taken)
        {
            EnsureFalseAndNotRecursiveEntry(taken);

            // while acquired == 1, loop, then when it == 0, exit and set it to 1
            while (Interlocked.CompareExchange(ref _acquired, NewAcquiredValue, 0) != 0)
            {
                // NOP
            }

            taken = true;
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void TryEnter(ref bool taken)
        {
            EnsureFalseAndNotRecursiveEntry(taken);

            // if it acquired == 0, change it to 1 and return true, else return false
            taken = Interlocked.CompareExchange(ref _acquired, NewAcquiredValue, 0) == 0;
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void TryEnter(ref bool taken, uint iterations)
        {
            EnsureFalseAndNotRecursiveEntry(taken);

            // if it acquired == 0, change it to 1 and return true, else return false
            while (Interlocked.CompareExchange(ref _acquired, NewAcquiredValue, 0) != 0)
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

            EnsureFalseAndNotRecursiveEntry(taken);

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
            EnsureOwnedAndOwnedByCurrentThread();

            // release the lock - int32 write will always be atomic
            _acquired = 0;
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void Exit(bool memBarrier)
        {
            Exit();

            if (memBarrier)
                Thread.MemoryBarrier();
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void ExitWithBarrier()
        {
            Exit();

            Thread.MemoryBarrier();
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void EnsureOwnedAndOwnedByCurrentThread()
        {
            if (_acquired == 0 || Thread.CurrentThread.ManagedThreadId != (_acquired >> 1))
                ThrowHelper.ThrowSynchronizationLockException("Lock is not owned by current thread");
        }
    }
}