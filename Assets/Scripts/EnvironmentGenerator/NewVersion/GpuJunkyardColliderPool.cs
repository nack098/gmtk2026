using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GpuJunkyardColliderPool : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GpuJunkyardPopulator _populator;
    [SerializeField] private Transform _targetPlayer;
    [SerializeField] private Transform _targetCart;

    [Header("Proximity Settings")]
    [SerializeField, Range(5f, 100f)] private float _proximityRadius = 25.0f;
    [SerializeField, Range(10, 300)] private int _maxActiveColliders = 150;
    [SerializeField, Range(0.05f, 0.5f)] private float _updateInterval = 0.1f;

    private readonly List<GameObject> _colliderPool = new List<GameObject>();
    private Transform _poolContainer;
    private float _nextUpdateTime;

    private void Awake()
    {
        if (_populator == null) _populator = GetComponent<GpuJunkyardPopulator>();
    }

    private void Start()
    {
        EnsurePoolContainer();
    }

    private void EnsurePoolContainer()
    {
        if (_poolContainer == null)
        {
            GameObject container = new GameObject("Junkyard_Collider_Pool");
            container.transform.SetParent(transform);
            _poolContainer = container.transform;
        }
    }

    private void Update()
    {
        if (_populator == null || !_populator.IsDataReady) return;
        if (Time.time < _nextUpdateTime) return;
        _nextUpdateTime = Time.time + _updateInterval;

        UpdateProximityColliders();
    }

    public void UpdateProximityColliders()
    {
        if (_targetPlayer == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                _targetPlayer = playerObj.transform;
            else if (Camera.main != null)
                _targetPlayer = Camera.main.transform;
            else
                return;
        }

        if (_targetCart == null)
        {
            if (TrashCount.Gameplay.TrashSystem.PushCart.Instance != null)
            {
                _targetCart = TrashCount.Gameplay.TrashSystem.PushCart.Instance.transform;
            }
            else
            {
                GameObject cartObj = GameObject.Find("Cart");
                if (cartObj != null) _targetCart = cartObj.transform;
            }
        }

        var instances = _populator.CachedInstances;
        var scrapTypes = _populator.ScrapTypes;
        if (instances == null || instances.Length == 0 || scrapTypes == null || scrapTypes.Length == 0) return;

        EnsurePoolContainer();
        Vector3 playerPos = _targetPlayer.position;
        Vector3 cartPos = _targetCart != null ? _targetCart.position : new Vector3(99999f, 99999f, 99999f);

        float radiusSq = _proximityRadius * _proximityRadius;
        int poolIndex = 0;

        for (int i = 0; i < instances.Length; i++)
        {
            ref var inst = ref instances[i];
            if (inst.scale.x <= 0.001f) continue; // Invalid/empty instance

            float distPlayerSq = (inst.position - playerPos).sqrMagnitude;
            float distCartSq = (_targetCart != null) ? (inst.position - cartPos).sqrMagnitude : float.MaxValue;

            // Generate junk colliders within proximity of EITHER Player OR PushCart!
            if (distPlayerSq <= radiusSq || distCartSq <= radiusSq)
            {
                if (poolIndex >= _maxActiveColliders) break;

                int typeIdx = Mathf.Clamp((int)inst.id, 0, scrapTypes.Length - 1);
                Mesh scrapMesh = scrapTypes[typeIdx].mesh;

                GameObject colObj = GetOrCreatePooledObject(poolIndex, scrapMesh);
                colObj.transform.position = inst.position;
                colObj.transform.rotation = Quaternion.Euler(inst.rotation);
                colObj.transform.localScale = new Vector3(inst.scale.x, inst.scale.y, inst.scale.x);
                colObj.SetActive(true);

                poolIndex++;
            }
        }

        // Disable extra unused pooled colliders
        for (int i = poolIndex; i < _colliderPool.Count; i++)
        {
            if (_colliderPool[i].activeSelf)
                _colliderPool[i].SetActive(false);
        }
    }

    private GameObject GetOrCreatePooledObject(int index, Mesh mesh)
    {
        while (_colliderPool.Count <= index)
        {
            GameObject obj = new GameObject($"JunkCollider_{_colliderPool.Count}");
            obj.transform.SetParent(_poolContainer);
            obj.layer = gameObject.layer;

            BoxCollider boxCol = obj.AddComponent<BoxCollider>();
            _colliderPool.Add(obj);
        }

        GameObject targetObj = _colliderPool[index];
        if (mesh != null)
        {
            BoxCollider boxCol = targetObj.GetComponent<BoxCollider>();
            if (boxCol != null)
            {
                boxCol.center = mesh.bounds.center;
                boxCol.size = mesh.bounds.size;
            }
        }
        return targetObj;
    }

    public void ClearPool()
    {
        foreach (var col in _colliderPool)
        {
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }
        _colliderPool.Clear();
    }

    private void OnDisable() => ClearPool();
    private void OnDestroy() => ClearPool();
}
