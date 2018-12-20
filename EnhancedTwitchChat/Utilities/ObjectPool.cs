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
        private Dictionary<T, bool> _objects;

        private Action<T> OnAlloc;
        private Action<T> OnFree;

        public ObjectPool(int initialCount = 0, Action<T> OnAlloc = null, Action<T> OnFree = null)
        {
            this.OnAlloc = OnAlloc;
            this.OnFree = OnFree;
            this._objects = new Dictionary<T, bool>();

            while (initialCount > 0)
            {
                _objects[internalAlloc()] = false;
                initialCount--;
            }
        }

        ~ObjectPool()
        {
            foreach(KeyValuePair<T, bool> obj in _objects)
                UnityEngine.Object.Destroy(obj.Key.gameObject);
        }

        private T internalAlloc()
        {
            T newObj = new GameObject().AddComponent<T>();
            UnityEngine.GameObject.DontDestroyOnLoad(newObj.gameObject);
            return newObj;
        }

        public T Alloc()
        {
            T obj = null;
            foreach (KeyValuePair<T, bool> kvp in _objects)
            {
                if (!kvp.Value)
                    obj = kvp.Key;
            }
            if (obj == null) obj = internalAlloc();

            if (OnAlloc != null)
                OnAlloc(obj);

            _objects[obj] = true;

            return obj;
        }

        public void Free(T obj) {
            if (_objects.ContainsKey(obj))
            {
                _objects[obj] = false;

                if (OnFree != null)
                    OnFree(obj);
            }
        }
    }
}
