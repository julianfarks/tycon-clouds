using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.Optimization
{
    /// <summary>Simple GameObject pool to avoid allocations for frequent short-lived objects (popups, puffs).</summary>
    public sealed class GameObjectPool
    {
        readonly Func<GameObject> factory;
        readonly Stack<GameObject> free = new Stack<GameObject>();
        readonly Transform root;

        public int CreatedCount { get; private set; }
        public int FreeCount { get { return free.Count; } }

        public GameObjectPool(Func<GameObject> factory, Transform root, int prewarm)
        {
            this.factory = factory;
            this.root = root;
            for (int i = 0; i < prewarm; i++)
            {
                GameObject go = Create();
                Release(go);
            }
        }

        GameObject Create()
        {
            GameObject go = factory();
            CreatedCount++;
            if (root != null) go.transform.SetParent(root, false);
            return go;
        }

        public GameObject Get()
        {
            GameObject go = null;
            while (free.Count > 0 && go == null) go = free.Pop();
            if (go == null) go = Create();
            go.SetActive(true);
            return go;
        }

        public void Release(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            if (root != null) go.transform.SetParent(root, false);
            free.Push(go);
        }
    }
}
