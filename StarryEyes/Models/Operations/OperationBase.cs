using System;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace StarryEyes.Models.Operations
{
    public abstract class OperationBase<T> : IRunnerQueueable
    {
        private const int RetryCount = 3;
        private const double RetryDelaySec = 3.0;

        private readonly Subject<T> _resultHandler = new Subject<T>();
        protected Subject<T> ResultHandler
        {
            get { return _resultHandler; }
        }

        /// <summary>
        /// Run operation via operation queue.
        /// </summary>
        // ReSharper disable MethodOverloadWithOptionalParameter
        internal IObservable<T> Run(OperationPriority priority = OperationPriority.Middle)
        {
            return Observable.Defer(() =>
            {
                OperationQueueRunner.Enqueue(this, priority);
                return _resultHandler;
            });
        }
        // ReSharper restore MethodOverloadWithOptionalParameter

        /// <summary>
        /// Run operation without operation queue
        /// </summary>
        public IObservable<T> RunImmediate()
        {
            return Observable.Defer(() => Observable.Start(() => RunCore()))
                .Retry(RetryCount, TimeSpan.FromSeconds(RetryDelaySec))
                .SelectMany(_ => _);
        }

        /// <summary>
        /// Core operation(Synchronously)
        /// </summary>
        protected abstract IObservable<T> RunCore();

        protected IObservable<string> GetExceptionDetail(Exception ex)
        {
            return Observable.Defer(() =>
                {
                    var wex = ex as WebException;
                    if (wex != null && wex.Response != null)
                    {
                        return Observable.Return(wex.Response)
                                         .SelectMany(r => r.DownloadStringAsync())
                                         .Select(ParseErrorMessage);
                    }
                    return Observable.Return(ex.Message);
                });
        }

        private string ParseErrorMessage(string error)
        {
            if (error.StartsWith("{\"errors\":") && error.EndsWith("}"))
                return error.Substring(9, error.Length - 10);
            return error;
        }

        IObservable<Unit> IRunnerQueueable.Run()
        {
            return Observable.Defer(RunCore)
                .Retry(RetryCount, TimeSpan.FromSeconds(RetryDelaySec))
                .Publish(connectable =>
                {
                    connectable.Subscribe(_resultHandler);
                    return connectable;
                })
                .Select(_ => new Unit());
        }
    }
}
