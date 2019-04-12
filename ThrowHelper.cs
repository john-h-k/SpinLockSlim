using System;
using System.Threading;

namespace CorePlayground.Locks
{
    public static class ThrowHelper
    {
        public static void ThrowArgumentException(string message = null, string paramName = null, Exception inner = null)
        { 
            throw new ArgumentException(message, paramName, inner);
        }

        public static void ThrowLockRecursionException(string message = null, Exception inner = null)
        {
            throw new LockRecursionException(message, inner);
        }

        public static void ThrowSynchronizationLockException(string message = null, Exception inner = null)
        {
            throw new SynchronizationLockException(message, inner);
        }
    }
}