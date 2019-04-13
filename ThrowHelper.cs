using System;
using System.Diagnostics;
using System.Threading;

namespace Locks
{
    internal static class ThrowHelper
    {
        [DebuggerHidden]
        public static void ThrowArgumentException(string message = null, string paramName = null, Exception inner = null)
        { 
            throw new ArgumentException(message, paramName, inner);
        }

        [DebuggerHidden]
        public static void ThrowLockRecursionException(string message = null, Exception inner = null)
        {
            throw new LockRecursionException(message, inner);
        }

        [DebuggerHidden]
        public static void ThrowSynchronizationLockException(string message = null, Exception inner = null)
        {
            throw new SynchronizationLockException(message, inner);
        }
    }
}