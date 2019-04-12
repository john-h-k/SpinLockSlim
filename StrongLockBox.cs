namespace Locks
{
    public class StrongLockBox
    {
        public SpinLockSlim Lock { get; } = new SpinLockSlim();
    }
}