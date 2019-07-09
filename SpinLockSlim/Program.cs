using System.Runtime.CompilerServices;

namespace Locks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new SpinLockSlim().Exit();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Exit(SpinLockSlim spinLock)
            => spinLock.Exit();
    }
}