namespace Locks
{
    public class StrongLockBoxChecked
    {
        public SpinLockSlimChecked Lock { get; } = new SpinLockSlimChecked();

    }
}