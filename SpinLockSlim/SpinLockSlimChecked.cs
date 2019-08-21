using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

// ReSharper disable RedundantAssignment

namespace Locks
{
    /// <summary>
    /// Provided a checked, debug, lightweight spin lock for synchronization in high performance
    /// scenarios with a low hold time, with additional safety and checks over <see cref="SpinLockSlim"/>
    /// </summary>
    [DebuggerDisplay("Acquired = {" + nameof(_acquired) + " != 0}, OwnerThreadId = {" + nameof(_acquired) + "} ")]
    public struct SpinLockSlimChecked
    {
        static unsafe SpinLockSlimChecked()
        {
            if (sizeof(SpinLockSlim) != sizeof(SpinLockSlimChecked))
                throw new InvalidOperationException(
                    $"{nameof(SpinLockSlimChecked)} (size {sizeof(SpinLockSlimChecked)}) does not match size of {nameof(SpinLockSlim)} (size {sizeof(SpinLockSlim)})");
        }

        private static int True => Thread.CurrentThread.ManagedThreadId;
        private static int False => 0;

        // ReSharper disable once InconsistentNaming -- just for clarity
        private const MethodImplOptions AggressiveInlining_AggressiveOpts =
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

        private volatile int _acquired; // used either to be 0 for not acquired or the managed thread ID

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public static SpinLockSlim Create() => new SpinLockSlim();

        /// <summary>
        /// Returns <c>true</c> if the lock is acquired, else <c>false</c>
        /// </summary>
        public bool IsAcquired => _acquired != False;

        /// <summary>
        /// Safely enter the lock. If this method returns, <paramref name="taken"/>
        /// will be <c>true</c>. If an exception occurs, <paramref name="taken"/> will indicate
        /// whether the lock was taken and needs to be released using <see cref="Exit()"/>
        /// This method may never exit
        /// </summary>
        /// <param name="taken">A reference to a bool that indicates whether the lock is taken.
        /// If the method returns, this is guaranteed to be <c>true</c></param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="taken"/> is <c>true</c></exception>
        /// <exception cref="LockRecursionException">Thrown if the current thread already owns this lock</exception>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void Enter(ref bool taken)
        {
            EnsureFalseAndNotRecursiveEntry(taken);

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
        /// <param name="taken">A reference to a bool that indicates whether the lock is taken</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="taken"/> is <c>true</c></exception>
        /// <exception cref="LockRecursionException">Thrown if the current thread already owns this lock</exception>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void TryEnter(ref bool taken)
        {
            EnsureFalseAndNotRecursiveEntry(taken);

            // if acquired == 0 (the lock is not taken), change it to 1 (take the lock)
            // and return true, else return false
            taken = TryAcquire();
        }

        /// <summary>
        /// Try to safely enter the lock a certain number of times (<paramref name="iterations"/>).
        /// <paramref name="taken"/> will be <c>true</c> if the lock was taken, else <c>false</c>.
        /// If <paramref name="taken"/> is <c>true</c>, <see cref="Exit()"/> must be called to release
        /// it, else, it must not be called
        /// </summary>
        /// <param name="taken">A reference to a bool that indicates whether the lock is taken</param>
        /// <param name="iterations">The number of attempts to acquire the lock before returning
        /// without the lock</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="taken"/> is <c>true</c></exception>
        /// <exception cref="LockRecursionException">Thrown if the current thread already owns this lock</exception>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void TryEnter(ref bool taken, uint iterations)
        {
            EnsureFalseAndNotRecursiveEntry(taken);

            // if acquired == 0 (the lock is not taken), change it to 1 (take the lock)
            // and return true, else retry until it we run out of iterations
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
        /// <param name="taken">A reference to a bool that indicates whether the lock is taken</param>
        /// <param name="timeout">The <see cref="TimeSpan"/> to attempt to acquire the lock for before
        /// returning without the lock. A negative <see cref="TimeSpan"/>will cause an exception</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="taken"/> is <c>true</c></exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="timeout"/> is negative</exception>
        /// <exception cref="LockRecursionException">Thrown if the current thread already owns this lock</exception>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void TryEnter(ref bool taken, TimeSpan timeout)
        {
            EnsurePositiveTimeSpan(timeout);
            EnsureFalseAndNotRecursiveEntry(taken);

            unchecked
            {
                long start = Stopwatch.GetTimestamp();
                var end = unchecked((long)((timeout.TotalMilliseconds * Stopwatch.Frequency) + start));

                // if it acquired == 0, change it to 1 and return true, else return false
                while (TryAcquire())
                {
                    if (Stopwatch.GetTimestamp() >= end)
                    {
                        return;
                    }
                }
            }

            taken = true;
        }

        /// <summary>
        /// Exit the lock safely. Ensure the lock was taken before calling this method, else it will throw
        /// </summary>
        /// <exception cref="SynchronizationLockException">Thrown if the current thread does not own the lock</exception>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void Exit()
        {
            EnsureOwnedAndOwnedByCurrentThread();

            // release the lock - int32 write will always be atomic
            _acquired = False;
        }

        /// <summary>
        /// Exit the lock with an optional post-release memory barrier safely. Ensure the lock was taken before calling this method, else it will throw
        /// </summary>
        /// <exception cref="SynchronizationLockException">Thrown if the current thread does not own the lock</exception>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void Exit(bool insertMemBarrier)
        {
            Exit();

            if (insertMemBarrier)
                Thread.MemoryBarrier();
        }

        /// <summary>
        /// Exit the lock with a post-release memory barrier safely. Ensure the lock was taken before calling this method, else it will throw
        /// </summary>
        /// <exception cref="SynchronizationLockException">Thrown if the current thread does not own the lock</exception>
        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        public void ExitWithBarrier()
        {
            Exit();

            Thread.MemoryBarrier();
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        [DebuggerHidden]
        private static void ValidateRefBoolAsFalse(in bool value)
        {
            if (value)
                ThrowHelper.ThrowArgumentException("Bool must be false");
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        [DebuggerHidden]
        private void EnsureNotRecursiveEntry()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            if (threadId == _acquired)
                ThrowHelper.ThrowLockRecursionException(
                    $"Lock is owned by current thread (ThreadId {threadId}), yet the current thread is attempting to acquire the lock");
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        [DebuggerHidden]
        private void EnsureFalseAndNotRecursiveEntry(in bool value)
        {
            ValidateRefBoolAsFalse(in value);
            EnsureNotRecursiveEntry();
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        [DebuggerHidden]
        private void EnsurePositiveTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan < TimeSpan.Zero)
                ThrowHelper.ThrowArgumentException(
                    $"Must be greater than or equal to {nameof(TimeSpan)}.{nameof(TimeSpan.Zero)}", nameof(timeSpan));
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        [DebuggerHidden]
        private void EnsureOwnedAndOwnedByCurrentThread()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            if (threadId != _acquired)
                ThrowHelper.ThrowSynchronizationLockException(
                    $"Lock is not owned by current thread - lock is owned by thread with ThreadId {_acquired}, but thread trying to exit has ThreadId {threadId}");
        }

        [MethodImpl(AggressiveInlining_AggressiveOpts)]
        private bool TryAcquire() => Interlocked.CompareExchange(ref _acquired, True, False) != False;
    }
}