// =====================================================
// ObjectPool.cs
// 범용 Object Pool (제네릭, T : MonoBehaviour)
// 부족 시 자동 확장, 초기 워밍업 지원
// =====================================================

using UnityEngine;
using System.Collections.Generic;

public class ObjectPool<T> where T : MonoBehaviour
{
    private readonly T _prefab;
    private readonly Transform _parent;
    private readonly Stack<T> _pool = new Stack<T>();

    public ObjectPool(T prefab, Transform parent, int initialSize = 0)
    {
        _prefab = prefab;
        _parent = parent;
        Warmup(initialSize);
    }

    void Warmup(int count)
    {
        for (int i = 0; i < count; i++)
            _pool.Push(CreateNew());
    }

    T CreateNew()
    {
        T obj = Object.Instantiate(_prefab, _parent);
        obj.gameObject.SetActive(false);
        return obj;
    }

    public T Get()
    {
        T obj = _pool.Count > 0 ? _pool.Pop() : CreateNew();
        obj.gameObject.SetActive(true);
        return obj;
    }

    public void Return(T obj)
    {
        if (obj == null) return;
        obj.gameObject.SetActive(false);
        obj.transform.SetParent(_parent, false);
        _pool.Push(obj);
    }

    public int PooledCount => _pool.Count;
}

// ── GameObject 전용 풀 (Prefab이 MonoBehaviour가 없는 경우) ──
public class GameObjectPool
{
    private readonly GameObject _prefab;
    private readonly Transform _parent;
    private readonly Stack<GameObject> _pool = new Stack<GameObject>();

    public GameObjectPool(GameObject prefab, Transform parent, int initialSize = 0)
    {
        _prefab = prefab;
        _parent = parent;
        Warmup(initialSize);
    }

    void Warmup(int count)
    {
        for (int i = 0; i < count; i++)
            _pool.Push(CreateNew());
    }

    GameObject CreateNew()
    {
        GameObject obj = Object.Instantiate(_prefab, _parent);
        obj.SetActive(false);
        return obj;
    }

    public GameObject Get(Transform parent = null)
    {
        GameObject obj = _pool.Count > 0 ? _pool.Pop() : CreateNew();
        if (parent != null) obj.transform.SetParent(parent, false);
        obj.SetActive(true);
        return obj;
    }

    public void Return(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        obj.transform.SetParent(_parent, false);
        _pool.Push(obj);
    }

    public int PooledCount => _pool.Count;
}
