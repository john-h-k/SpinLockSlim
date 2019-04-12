namespace Locks
{
    public class StrongLockBoxChecked
    {
        public class StrongLockBox
        {
            public SpinLockSlimChecked Lock { get; } = new SpinLockSlimChecked();
        }
    }
}