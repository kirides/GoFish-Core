using System;

namespace GoFishCore.WpfUI
{
    public static class SynchronizationContextExtensions
    {
        public static SynchronizationContextAwaiter GetAwaiter(this System.Threading.SynchronizationContext context)
        {
            return new SynchronizationContextAwaiter(context);
        }
    }
    public struct SynchronizationContextAwaiter : System.Runtime.CompilerServices.INotifyCompletion
    {
        private static void PostCallback(object state) => ((Action)state)();
        private readonly System.Threading.SynchronizationContext _context;
        public SynchronizationContextAwaiter(System.Threading.SynchronizationContext context)
        {
            _context = context;
        }

        public bool IsCompleted => _context == System.Threading.SynchronizationContext.Current;

        public void OnCompleted(Action continuation) => _context.Post(PostCallback, continuation);

        public void GetResult() { }
    }

}
