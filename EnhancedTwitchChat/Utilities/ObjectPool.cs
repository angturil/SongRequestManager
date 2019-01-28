using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedTwitchChat
{
    public class ObjectPool<T> where T : Component
    {
        private Stack<T> _freeObjects;
        private Action<T> FirstAlloc;
        private Action<T> OnAlloc;
        private Action<T> OnFree;

        public ObjectPool(int initialCount = 0, Action<T> FirstAlloc = null, Action<T> OnAlloc = null, Action<T> OnFree = null)
        {
            this.FirstAlloc = FirstAlloc;
            this.OnAlloc = OnAlloc;
            this.OnFree = OnFree;
            this._freeObjects = new Stack<T>();

            while (initialCount > 0)
            {
                _freeObjects.Push(internalAlloc());
                initialCount--;
            }
        }

        ~ObjectPool()
        {
            foreach(T obj in _freeObjects)
                UnityEngine.Object.Destroy(obj.gameObject);
        }

        private T internalAlloc()
        {
            T newObj = new GameObject().AddComponent<T>();
            UnityEngine.GameObject.DontDestroyOnLoad(newObj.gameObject);
            FirstAlloc?.Invoke(newObj);
            return newObj;
        }

        public T Alloc()
        {
            T obj = null;
            if (_freeObjects.Count > 0)
                obj = _freeObjects.Pop();
            if(!obj)
                obj = internalAlloc();
            OnAlloc?.Invoke(obj);
            return obj;
        }

        public void Free(T obj) {
            if (obj == null) return;
            _freeObjects.Push(obj);
            OnFree?.Invoke(obj);
        }
    }
}
