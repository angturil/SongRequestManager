using System;
using System.Collections.Generic;
using System.Linq;

namespace ChatSharp
{
    internal class RequestManager
    {
        public RequestManager()
        {
            PendingOperations = new Dictionary<string, RequestOperation>();
        }

        internal Dictionary<string, RequestOperation> PendingOperations { get; private set; }

        public void QueueOperation(string key, RequestOperation operation)
        {
            if (PendingOperations.ContainsKey(key))
                throw new InvalidOperationException("Operation is already pending.");
            PendingOperations.Add(key, operation);
        }

        public RequestOperation PeekOperation(string key)
        {
            var realKey = PendingOperations.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            return PendingOperations[realKey];
        }

        public RequestOperation DequeueOperation(string key)
        {
            var operation = PendingOperations[key];
            PendingOperations.Remove(key);
            return operation;
        }
    }

    internal class RequestOperation
    {
        public object State { get; set; }
        public Action<RequestOperation> Callback { get; set; }

        public RequestOperation(object state, Action<RequestOperation> callback)
        {
            State = state;
            Callback = callback;
        }
    }
}
