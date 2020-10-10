using System.Collections.Generic;
using System.Threading;
using System;
using UnityEngine;
using System.Collections;

namespace SongRequestManager
{
    internal class Dispatcher : MonoBehaviour
    {
        private static Dispatcher _instance;
        private static volatile bool _queued = false;
        private static List<Action> _backlog = new List<Action>(8);
        private static List<Action> _actions = new List<Action>(8);

        public static void RunAsync(Action action)
        {
            ThreadPool.QueueUserWorkItem(o => action());
        }

        public static void RunAsync(Action<object> action, object state)
        {
            ThreadPool.QueueUserWorkItem(o => action(o), state);
        }

        public static void RunCoroutine(IEnumerator enumerator)
        {
            _instance.StartCoroutine(enumerator);
        }

        public static void RunOnMainThread(Action action)
        {
            lock (_backlog)
            {
                _backlog.Add(action);
                _queued = true;
            }
        }

        public static void Initialize()
        {
            if (_instance == null)
            {
                _instance = new GameObject("Dispatcher").AddComponent<Dispatcher>();
                DontDestroyOnLoad(_instance.gameObject);
            }
        }

        private void Update()
        {
            if (_queued)
            {
                lock (_backlog)
                {
                    var tmp = _actions;
                    _actions = _backlog;
                    _backlog = tmp;
                    _queued = false;
                }

                foreach (var action in _actions)
                    action();

                _actions.Clear();
            }
        }
    }
}
