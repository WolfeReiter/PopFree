using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PopFree.Pop3
{
    /// <summary>
    /// WorkPool allows creating a set of background threads that will block the calling thread when
    /// until the scheduled worker threads have executed. Delegates are added to the pool by calling 
    /// QueueWorkItem&lt;T&gt;(Action, T ). Calling WaitFor() or Dispose() will cause the calling thread to block
    /// until all worker threads have completed.
    /// </summary>
    public sealed class WorkPool : IDisposable
    {
        /// <summary>
        /// Queue a delegate to be executed on a new thread in the pool. T is the type of the argument
        /// to the delegate. The item of type T argument is the actual argument value passed to the 
        /// delegate when it is executed.
        /// </summary>
        /// <typeparam name="T">Type of the method argument invoked by the Action delegate</typeparam>
        /// <param name="action">Delegate which invokes the function (e.g. a Lambda expression)</param>
        /// <param name="item">Value of the argument passed. Can be null if the invoked function is OK with it.</param>
        public void QueueWorkItem<T>(Action<T> action, T item )
        {
            Interlocked.Increment( ref _workers );
            ThreadPool.QueueUserWorkItem( 
                x => { try { action( (T)x ); } finally { Done(); } }
                , item 
                );
        }
        
        private bool Done()
        {
            if( Interlocked.Decrement( ref _workers ) >= 0 )
                return false;

            _finished.Set();
            return true;
        }

        private EventWaitHandle Finished
        { get { return _finished; } }

        public void WaitFor()
        {
            Done();
            _finished.WaitOne();
            Interlocked.Increment( ref _workers );
        }

        public void Dispose()
        {
            WaitFor();
        }

        readonly ManualResetEvent _finished = new ManualResetEvent( false );
        int _workers = 0;
    }
}