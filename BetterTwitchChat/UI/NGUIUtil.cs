using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BetterTwitchChat.UI {
    internal class NGUIUtil {
        private const string INDENT_STRING = " ";

        private static StringBuilder sb;

        private string getTag(Component co, int n) {
            return this.getTag(co.gameObject, n);
        }

        private string getTag(GameObject go, int n) {
            if (go.name.Split(new char[]
            {
                ':'
            }) == null) {
                return "";
            }
            return go.name.Split(new char[]
            {
                ':'
            })[n];
        }

        static NGUIUtil() {
            NGUIUtil.sb = new StringBuilder();
        }

        internal static void SetChild(GameObject parent, GameObject child) {
            child.layer = parent.layer;
            child.transform.parent = parent.transform;
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale = Vector3.one;
            child.transform.rotation = Quaternion.identity;
        }

        internal static GameObject SetCloneChild(GameObject parent, GameObject orignal, string name) {
            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(orignal);
            if (gameObject == null) {
                return null;
            }
            gameObject.name = name;
            NGUIUtil.SetChild(parent, gameObject);
            return gameObject;
        }

        internal static void ReleaseChild(GameObject child) {
            child.transform.parent = null;
            child.SetActive(false);
        }

        internal static void DestroyChild(GameObject parent, string name) {
            GameObject gameObject = NGUIUtil.FindChild(parent, name);
            if (gameObject) {
                gameObject.transform.parent = null;
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        internal static Transform FindChild(Transform tr, string s) {
            return NGUIUtil.FindChild(tr.gameObject, s).transform;
        }

        internal static GameObject FindChild(GameObject go, string s) {
            if (go == null) {
                return null;
            }
            foreach (Transform transform in go.transform) {
                if (transform.gameObject.name == s) {
                    GameObject result = transform.gameObject;
                    return result;
                }
                GameObject gameObject = NGUIUtil.FindChild(transform.gameObject, s);
                if (gameObject) {
                    GameObject result = gameObject;
                    return result;
                }
            }
            return null;
        }
    }
}