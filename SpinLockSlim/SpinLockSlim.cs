using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

// ReSharper disable RedundantAssignment

namespace Locks
{
    /// <summary>
    /// Provided a lightweight spin lock for synchronization in high performance
    /// scenarios with a low hold time
    /// </summary>
    [DebuggerDisplay("Use " + nameof(SpinLockSlimChecked) + " for debugging")]
    public struct SpinLockSlim
    {
        private static int True => 1;
        private static int False => 0;

        // ReSharper disable once InconsistentNaming -- just for clarity
        private const MethodImplOptions AggressiveInlining_AggressiveOpts =
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

        private volatile int _acquired; // either 1 or 0

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public static SpinLockSlim Create() => new SpinLockSlim();

        /// <summary>
        /// Returns <c>true</c> if the lock is acquired, else <c>false</c>
        /// </summary>
#pragma warning disable 420 // Unsafe.As<,> doesn't read the reference so the lack of volatility is not an issue, but we do need to treat the returned reference as volatile
        public bool IsAcquired => Volatile.Read(ref Unsafe.As<int, bool>(ref _acquired));
#pragma warning restore 420

        /// <summary>
        /// Enter the lock. If this method returns, <paramref name="taken"/>
        /// will be <c>true</c>. If an exception occurs, <paramref name="taken"/> will indicate
        /// whether the lock was taken and needs to be released using <see cref="Exit()"/>.
        /// This method may never exit
        /// </summary>
        /// <param name="taken">A reference to a bool that indicates whether the lock is taken. Must
        /// be <c>false</c> when passed, else the internal state or return state may be corrupted.
        /// If the method returns, this is guaranteed to be <c>true</c></param>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void Enter(ref bool taken)
        {
            // while acquired == 1, loop, then when it == 0, exit and set it to 1
            while (TryAcquire())
            {
                // NOP
            }

            taken = true;
        }

        /// <summary>
        /// Enter the lock if it not acquired, else, do not. <paramref name="taken"/> will be
        /// <c>true</c> if the lock was taken, else <c>false</c>. If <paramref name="taken"/> is
        /// <c>true</c>, <see cref="Exit()"/> must be called to release it, else, it must not be called
        /// </summary>
        /// <param name="taken">A reference to a bool that indicates whether the lock is taken. Must
        /// be <c>false</c> when passed, else the internal state or return state may be corrupted</param>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void TryEnter(ref bool taken)
        {
            // if it acquired == 0, change it to 1 and return true, else return false
            taken = TryAcquire();
        }

        /// <summary>
        /// Try to safely enter the lock a certain number of times (<paramref name="iterations"/>).
        /// <paramref name="taken"/> will be <c>true</c> if the lock was taken, else <c>false</c>.
        /// If <paramref name="taken"/> is <c>true</c>, <see cref="Exit()"/> must be called to release
        /// it, else, it must not be called
        /// </summary>
        /// <param name="taken">A reference to a bool that indicates whether the lock is taken. Must
        /// be <c>false</c> when passed, else the internal state or return state may be corrupted</param>
        /// <param name="iterations">The number of attempts to acquire the lock before returning
        /// without the lock</param>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void TryEnter(ref bool taken, uint iterations)
        {
            // if it acquired == 0, change it to 1 and return true, else return false
            while (TryAcquire())
            {
                if (unchecked(iterations--) == 0) // postfix decrement, so no issue if iterations == 0 at first
                {
                    return;
                }
            }

            taken = true;
        }

        /// <summary>
        /// Try to safely enter the lock for a certain <see cref="TimeSpan"/> (<paramref name="timeout"/>).
        /// <paramref name="taken"/> will be <c>true</c> if the lock was taken, else <c>false</c>.
        /// If <paramref name="taken"/> is <c>true</c>, <see cref="Exit()"/> must be called to release
        /// it, else, it must not be called
        /// </summary>
        /// <param name="taken">A reference to a bool that indicates whether the lock is taken. Must
        /// be <c>false</c> when passed, else the internal state or return state may be corrupted</param>
        /// <param name="timeout">The <see cref="TimeSpan"/> to attempt to acquire the lock for before
        /// returning without the lock. A negative <see cref="TimeSpan"/>will cause undefined behaviour</param>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void TryEnter(ref bool taken, TimeSpan timeout)
        {
            long start = Stopwatch.GetTimestamp();
            long end = unchecked((long)timeout.TotalMilliseconds * Stopwatch.Frequency + start);

            // if it acquired == 0, change it to 1 and return true, else return false
            while (TryAcquire())
            {
                if (Stopwatch.GetTimestamp() >= end)
                {
                    return;
                }
            }

            taken = true;
        }

        /// <summary>
        /// Exit the lock. This method is dangerous and must be called only once the caller is sure they have
        /// ownership of the lock. Use <see cref="SpinLockSlimChecked"/> for debugging to ensure your code
        /// only calls <see cref="Exit()"/> when it has ownership
        /// </summary>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void Exit()
        {
            // release the lock - int32 write will always be atomic
            _acquired = False;
        }

        /// <summary>
        /// Exit the lock with an optional post-release memory barrier. This method is dangerous and must be called only once the caller is sure they have
        /// ownership of the lock. Use <see cref="SpinLockSlimChecked"/> for debugging to ensure your code
        /// only calls <see cref="Exit()"/> when it has ownership
        /// </summary>
        /// <param name="insertMemBarrier">Whether a memory barrier should be inserted after the release</param>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void Exit(bool insertMemBarrier)
        {
            Exit();

            if (insertMemBarrier)
                Thread.MemoryBarrier();
        }

        /// <summary>
        /// Exit the lock with a post-release memory barrier. This method is dangerous and must be called only once the caller is sure they have
        /// ownership of the lock. Use <see cref="SpinLockSlimChecked"/> for debugging to ensure your code
        /// only calls <see cref="Exit()"/> when it has ownership
        /// </summary>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void ExitWithBarrier()
        {
            Exit();

            Thread.MemoryBarrier();
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        private bool TryAcquire() => Interlocked.CompareExchange(ref _acquired, True, False) != False;
    }
}