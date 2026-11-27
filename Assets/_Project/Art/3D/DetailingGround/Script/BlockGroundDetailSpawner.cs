using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockGroundDetailSpawner : MonoBehaviour
{
    private struct TopTriangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public float cumulativeArea;
    }

    private struct SpawnedPropInfo
    {
        public Vector3 worldPos;
        public float radius;
        public bool isGrass;
        public bool isFlower;
        public bool isRock;
    }

    [Header("3D Prefab Assets")]
    [Tooltip("Masukkan variasi prefab rumput.")]
    [SerializeField] private GameObject[] _grassPrefabs;
    [Tooltip("Masukkan variasi prefab bunga.")]
    [SerializeField] private GameObject[] _flowerPrefabs;
    [Tooltip("Masukkan variasi prefab batu.")]
    [SerializeField] private GameObject[] _rockPrefabs;

    [Header("Container & Referensi Prefab")]
    [SerializeField] private Transform _detailingContainer;
    [SerializeField] private Transform _blockTileRoot;
    [SerializeField] private Transform _cleaningPoint;
    [SerializeField] private GameObject _dirtyVisualsRoot;

    [Header("Kepadatan & Pola Rumpun (Natural Clustering)")]
    [Tooltip("Jumlah rumpun di area terbuka per blok.")]
    [SerializeField] private Vector2Int _clustersPerBlock = new Vector2Int(4, 6);
    [SerializeField] private Vector2Int _itemsPerCluster = new Vector2Int(2, 4);
    [SerializeField, Min(0.15f)] private float _clusterRadius = 0.58f;
    [SerializeField, Min(0.1f)] private float _minPropSpacing = 0.36f;
    [SerializeField, Range(0.4f, 1f)] private float _grassSpacingMultiplier = 0.80f;

    [Header("Bobot Kemunculan & Batas Batu")]
    [SerializeField, Min(0f)] private float _grassWeight = 65f;
    [SerializeField, Min(0f)] private float _flowerWeight = 23f;
    [SerializeField, Min(0f)] private float _rockWeight = 12f;
    [SerializeField, Min(0)] private int _maxRocksPerBlock = 1;
    [Tooltip("Radius fisik batu agar bunga tidak menembus mesh batu.")]
    [SerializeField, Min(0.2f)] private float _rockBodyRadius = 0.55f;
    [Tooltip("Jarak minimal antar dua batu.")]
    [SerializeField, Min(0.4f)] private float _minRockSpacing = 1.25f;

    [Header("Batas Aman Area (Pohon, Rintangan & Tepi Tebing)")]
    [Tooltip("Radius batang/gundukan tanah pohon yang tidak boleh ditembus prop.")]
    [SerializeField, Min(0.15f)] private float _centerClearRadius = 0.45f;
    [Tooltip("Jarak aman ke dalam dari pinggir luar pulau untuk rumput agar tidak keluar ke laut.")]
    [SerializeField, Min(0.2f)] private float _edgeSafetyMargin = 0.42f;
    [Tooltip("Jarak aman khusus batu dan bunga dari tepi luar pulau agar tidak muncul di pinggiran.")]
    [SerializeField, Min(0.5f)] private float _flowerAndRockEdgeMargin = 1.45f;

    [Header("Variasi Transform Alami (Isometric Readability)")]
    [Tooltip("Skala khusus rumput dan bunga.")]
    [SerializeField] private Vector2 _scaleRange = new Vector2(0.75f, 1.18f);
    [Tooltip("Skala khusus batu.")]
    [SerializeField] private Vector2 _rockScaleRange = new Vector2(0.72f, 1.00f);
    [SerializeField, Range(0f, 15f)] private float _maxTiltAngle = 4.0f;
    [SerializeField] private float _surfaceYOffset = 0.0f;

    [Header("Pengaturan Spawn")]
    [SerializeField] private bool _spawnOnStart = true;
    [SerializeField] private bool _hideWhenDirty = true;

    private readonly List<TopTriangle> _topTriangles = new List<TopTriangle>();
    private readonly List<TopTriangle> _islandWalkableTriangles = new List<TopTriangle>();
    private readonly List<SpawnedPropInfo> _spawnedProps = new List<SpawnedPropInfo>();
    private readonly List<Vector2> _nearbyTreePointsXZ = new List<Vector2>();
    private readonly Collider[] _overlapBuffer = new Collider[16];

    private float _totalSurfaceArea;
    private bool _lastDirtyState;

    private MeshFilter[] _externalSurfaceFilters;
    private readonly List<Vector2> _externalTreePointsXZ = new List<Vector2>();
    private Bounds _islandOuterBounds;
    private bool _hasIslandOuterBounds;

    private void Awake()
    {
        ResolveReferences();
    }

    private IEnumerator Start()
    {
        SyncDirtyVisibility(force: true);

        // Tunggu 1 frame agar seluruh pohon, gundukan tanah, dan blok pulau selesai di-spawn
        yield return null;

        if (_spawnOnStart)
        {
            GenerateDetailing();
        }

        SyncDirtyVisibility(force: true);
    }

    private void Update()
    {
        if (_hideWhenDirty && _dirtyVisualsRoot != null)
        {
            SyncDirtyVisibility(force: false);
        }
    }

    private void SyncDirtyVisibility(bool force)
    {
        if (!_hideWhenDirty || _detailingContainer == null || _dirtyVisualsRoot == null)
            return;

        bool isDirty = _dirtyVisualsRoot.activeSelf;
        if (force || isDirty != _lastDirtyState)
        {
            _lastDirtyState = isDirty;
            _detailingContainer.gameObject.SetActive(!isDirty);
        }
    }

    private void ResolveReferences()
    {
        if (_detailingContainer == transform)
            _detailingContainer = null;

        if (_blockTileRoot == null)
            _blockTileRoot = transform.Find("BlockTile");

        if (_cleaningPoint == null)
            _cleaningPoint = transform.Find("CleaningPoint");

        if (_dirtyVisualsRoot == null)
        {
            Transform dirty = transform.Find("DirtyVisuals");
            if (dirty != null) _dirtyVisualsRoot = dirty.gameObject;
        }

        if (_detailingContainer == null)
        {
            Transform existing = transform.Find("DetailingGround");
            if (existing != null)
            {
                _detailingContainer = existing;
            }
            else
            {
                GameObject containerObj = new GameObject("DetailingGround");
                containerObj.transform.SetParent(transform, false);
                _detailingContainer = containerObj.transform;
            }
        }
    }

    private void CollectNearbyTreeClearZones()
    {
        _nearbyTreePointsXZ.Clear();

        if (_cleaningPoint != null)
            AddClearPoint(_cleaningPoint.position);

        for (int i = 0; i < _externalTreePointsXZ.Count; i++)
        {
            Vector2 pt = _externalTreePointsXZ[i];
            if (!_nearbyTreePointsXZ.Contains(pt))
                _nearbyTreePointsXZ.Add(pt);
        }

        if (transform.parent != null)
        {
            BlockGroundDetailSpawner[] siblingSpawners = transform.parent.GetComponentsInChildren<BlockGroundDetailSpawner>(true);
            for (int i = 0; i < siblingSpawners.Length; i++)
            {
                BlockGroundDetailSpawner other = siblingSpawners[i];
                if (other == null || other == this || other._cleaningPoint == null)
                    continue;

                AddClearPoint(other._cleaningPoint.position);
            }
        }

        if (TreeRegistry.Instance is MonoBehaviour registryBehaviour && registryBehaviour != null)
        {
            Transform regRoot = registryBehaviour.transform;
            for (int i = 0; i < regRoot.childCount; i++)
            {
                Transform child = regRoot.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy)
                    AddClearPoint(child.position);
            }
        }
    }

    private void AddClearPoint(Vector3 worldPos)
    {
        Vector2 pt = new Vector2(worldPos.x, worldPos.z);
        if (!_nearbyTreePointsXZ.Contains(pt))
            _nearbyTreePointsXZ.Add(pt);
    }

    private void CollectTopSurfaceTriangles()
    {
        _topTriangles.Clear();
        _islandWalkableTriangles.Clear();
        _totalSurfaceArea = 0f;

        MeshFilter[] meshFilters = GetSurfaceMeshFilters();
        ExtractTopTrianglesFromFilters(meshFilters, _topTriangles, ref _totalSurfaceArea);

        _islandWalkableTriangles.AddRange(_topTriangles);
        if (transform.parent != null)
        {
            BlockGroundDetailSpawner[] siblings = transform.parent.GetComponentsInChildren<BlockGroundDetailSpawner>(true);
            float dummyArea = 0f;
            for (int i = 0; i < siblings.Length; i++)
            {
                BlockGroundDetailSpawner sibling = siblings[i];
                if (sibling == null || sibling == this) continue;
                sibling.ResolveReferences();
                ExtractTopTrianglesFromFilters(sibling.GetSurfaceMeshFilters(), _islandWalkableTriangles, ref dummyArea);
            }
        }
    }

    private MeshFilter[] GetSurfaceMeshFilters()
    {
        if (_externalSurfaceFilters != null && _externalSurfaceFilters.Length > 0)
            return _externalSurfaceFilters;

        Transform searchRoot = _blockTileRoot != null ? _blockTileRoot : transform;
        return searchRoot.GetComponentsInChildren<MeshFilter>(false);
    }

    private void ExtractTopTrianglesFromFilters(MeshFilter[] meshFilters, List<TopTriangle> targetList, ref float cumulativeArea)
    {
        if (meshFilters == null) return;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            if (mf == null || mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
            if (_detailingContainer != null && mf.transform.IsChildOf(_detailingContainer)) continue;
            if (_dirtyVisualsRoot != null && mf.transform.IsChildOf(_dirtyVisualsRoot.transform)) continue;
            if (_cleaningPoint != null && mf.transform.IsChildOf(_cleaningPoint)) continue;

            string objName = mf.gameObject.name;
            if (objName.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objName.IndexOf("ocean", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objName.IndexOf("foam", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            Mesh mesh = mf.sharedMesh;
            if (!mesh.isReadable)
            {
                AddOrientedTopBoundsTriangles(mf.transform, mesh.bounds, targetList, ref cumulativeArea);
                continue;
            }

            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;

            float maxTopWorldY = float.NegativeInfinity;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 wa = mf.transform.TransformPoint(verts[tris[t]]);
                Vector3 wb = mf.transform.TransformPoint(verts[tris[t + 1]]);
                Vector3 wc = mf.transform.TransformPoint(verts[tris[t + 2]]);

                Vector3 cross = Vector3.Cross(wb - wa, wc - wa);
                float mag = cross.magnitude;
                if (mag <= 0.00001f || (cross.y / mag) < 0.95f) continue;

                float avgY = (wa.y + wb.y + wc.y) / 3f;
                if (avgY > maxTopWorldY)
                    maxTopWorldY = avgY;
            }

            if (float.IsNegativeInfinity(maxTopWorldY))
            {
                AddOrientedTopBoundsTriangles(mf.transform, mesh.bounds, targetList, ref cumulativeArea);
                continue;
            }

            const float topWorldTolerance = 0.02f;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 wa = mf.transform.TransformPoint(verts[tris[t]]);
                Vector3 wb = mf.transform.TransformPoint(verts[tris[t + 1]]);
                Vector3 wc = mf.transform.TransformPoint(verts[tris[t + 2]]);

                Vector3 cross = Vector3.Cross(wb - wa, wc - wa);
                float mag = cross.magnitude;
                if (mag <= 0.00001f || (cross.y / mag) < 0.95f)
                    continue;

                float avgY = (wa.y + wb.y + wc.y) / 3f;
                if (avgY < maxTopWorldY - topWorldTolerance)
                    continue;

                float area = 0.5f * Mathf.Abs((wb.x - wa.x) * (wc.z - wa.z) - (wc.x - wa.x) * (wb.z - wa.z));
                if (area <= 0.00001f)
                    continue;

                cumulativeArea += area;
                targetList.Add(new TopTriangle
                {
                    a = wa,
                    b = wb,
                    c = wc,
                    cumulativeArea = cumulativeArea
                });
            }
        }
    }

    private static void AddOrientedTopBoundsTriangles(Transform t, Bounds b, List<TopTriangle> targetList, ref float cumulativeArea)
    {
        Vector3 upInLocal = t.InverseTransformDirection(Vector3.up);
        int dominantAxis = 1;
        if (Mathf.Abs(upInLocal.x) > Mathf.Abs(upInLocal.y) && Mathf.Abs(upInLocal.x) > Mathf.Abs(upInLocal.z))
            dominantAxis = 0;
        else if (Mathf.Abs(upInLocal.z) > Mathf.Abs(upInLocal.y))
            dominantAxis = 2;

        float topCoord = dominantAxis == 0 ? (upInLocal.x >= 0 ? b.max.x : b.min.x)
                       : dominantAxis == 1 ? (upInLocal.y >= 0 ? b.max.y : b.min.y)
                                           : (upInLocal.z >= 0 ? b.max.z : b.min.z);

        Vector3 wa, wb, wc, wd;
        if (dominantAxis == 0)
        {
            wa = t.TransformPoint(new Vector3(topCoord, b.min.y, b.min.z));
            wb = t.TransformPoint(new Vector3(topCoord, b.min.y, b.max.z));
            wc = t.TransformPoint(new Vector3(topCoord, b.max.y, b.max.z));
            wd = t.TransformPoint(new Vector3(topCoord, b.max.y, b.min.z));
        }
        else if (dominantAxis == 2)
        {
            wa = t.TransformPoint(new Vector3(b.min.x, b.min.y, topCoord));
            wb = t.TransformPoint(new Vector3(b.min.x, b.max.y, topCoord));
            wc = t.TransformPoint(new Vector3(b.max.x, b.max.y, topCoord));
            wd = t.TransformPoint(new Vector3(b.max.x, b.min.y, topCoord));
        }
        else
        {
            wa = t.TransformPoint(new Vector3(b.min.x, topCoord, b.min.z));
            wb = t.TransformPoint(new Vector3(b.min.x, topCoord, b.max.z));
            wc = t.TransformPoint(new Vector3(b.max.x, topCoord, b.max.z));
            wd = t.TransformPoint(new Vector3(b.max.x, topCoord, b.min.z));
        }

        if (Vector3.Cross(wb - wa, wc - wa).y < 0f)
        {
            Vector3 tmp = wb;
            wb = wd;
            wd = tmp;
        }

        float area1 = 0.5f * Mathf.Abs((wb.x - wa.x) * (wc.z - wa.z) - (wc.x - wa.x) * (wb.z - wa.z));
        if (area1 > 0.00001f)
        {
            cumulativeArea += area1;
            targetList.Add(new TopTriangle { a = wa, b = wb, c = wc, cumulativeArea = cumulativeArea });
        }

        float area2 = 0.5f * Mathf.Abs((wc.x - wa.x) * (wd.z - wa.z) - (wd.x - wa.x) * (wc.z - wa.z));
        if (area2 > 0.00001f)
        {
            cumulativeArea += area2;
            targetList.Add(new TopTriangle { a = wa, b = wc, c = wd, cumulativeArea = cumulativeArea });
        }
    }

    [ContextMenu("Generate Detailing Preview")]
    public void GenerateDetailing()
    {
        ResolveReferences();
        ClearDetailing();
        CollectTopSurfaceTriangles();
        CollectNearbyTreeClearZones();

        if (!HasAnyValidPrefab() || _topTriangles.Count == 0)
            return;

        _detailingContainer.gameObject.SetActive(true);
        _spawnedProps.Clear();

        float areaScale = Mathf.Max(1f, _totalSurfaceArea / 24f);
        int minClusters = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(_clustersPerBlock.x, _clustersPerBlock.y) * areaScale));
        int maxClusters = Mathf.Max(minClusters, Mathf.RoundToInt(Mathf.Max(_clustersPerBlock.x, _clustersPerBlock.y) * areaScale));
        int clusterCount = Random.Range(minClusters, maxClusters + 1);

        int maxRocksForPartition = _maxRocksPerBlock > 0
            ? Mathf.Max(_maxRocksPerBlock, Mathf.RoundToInt(_maxRocksPerBlock * areaScale))
            : 0;

        float rockEdgeMargin = Mathf.Max(_edgeSafetyMargin, _flowerAndRockEdgeMargin) + _rockBodyRadius * 0.5f;

        // 1. Tempatkan batu hanya di area dalam pulau (jauh dari tepi pulau)
        if (_rockPrefabs != null && _rockPrefabs.Length > 0 && _rockWeight > 0f && maxRocksForPartition > 0)
        {
            for (int r = 0; r < maxRocksForPartition; r++)
            {
                if (!TryPickSpacedTopPoint(rockEdgeMargin, _rockBodyRadius * 0.65f, out Vector3 rockPos))
                    continue;

                if (IsTooCloseToAnyIslandProp(rockPos, _rockBodyRadius, isGrass: false, isFlower: false, isRock: true))
                    continue;

                GameObject rockPrefab = GetRandomFromArray(_rockPrefabs);
                if (rockPrefab == null) continue;

                SpawnPropInstance(rockPrefab, rockPos, isRock: true, scaleMultiplier: 1f);
                _spawnedProps.Add(new SpawnedPropInfo
                {
                    worldPos = rockPos,
                    radius = _rockBodyRadius,
                    isGrass = false,
                    isFlower = false,
                    isRock = true
                });

                // Tambahkan 2-3 rumput kecil di kaki batu agar menyatu dengan tanah
                if (_grassPrefabs != null && _grassPrefabs.Length > 0)
                {
                    int accentCount = Random.Range(2, 4);
                    float baseAngle = Random.Range(0f, Mathf.PI * 2f);
                    for (int a = 0; a < accentCount; a++)
                    {
                        float ang = baseAngle + a * 2.1f + Random.Range(-0.35f, 0.35f);
                        float dist = _rockBodyRadius * Random.Range(0.85f, 1.2f);
                        Vector3 grassPos = rockPos + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);

                        if (!TryValidatePropSurface(grassPos, _edgeSafetyMargin, out float gSurfaceY))
                            continue;

                        grassPos.y = gSurfaceY + _surfaceYOffset;
                        if (IsBlockedByTreeOrObstacle(grassPos, 0.16f))
                            continue;

                        if (IsTooCloseToAnyIslandProp(grassPos, 0.18f, isGrass: true, isFlower: false, isRock: false))
                            continue;

                        GameObject accentGrass = GetRandomFromArray(_grassPrefabs);
                        SpawnPropInstance(accentGrass, grassPos, isRock: false, scaleMultiplier: Random.Range(0.72f, 0.92f));
                        _spawnedProps.Add(new SpawnedPropInfo
                        {
                            worldPos = grassPos,
                            radius = 0.18f,
                            isGrass = true,
                            isFlower = false,
                            isRock = false
                        });
                    }
                }
            }
        }

        // 2. Tempatkan rumpun di ring luar sekitar pohon/tunas
        for (int t = 0; t < _nearbyTreePointsXZ.Count; t++)
        {
            Vector2 treeXZ = _nearbyTreePointsXZ[t];
            if (!TryGetSurfaceYFromList(_topTriangles, treeXZ.x, treeXZ.y, out _))
                continue;

            int nearTreeClusters = Random.Range(1, 3);
            float baseDir = Random.Range(0f, Mathf.PI * 2f);

            for (int k = 0; k < nearTreeClusters; k++)
            {
                float dir = baseDir + k * Mathf.PI + Random.Range(-0.75f, 0.75f);
                float ringDist = _centerClearRadius + Random.Range(0.35f, 0.95f);
                Vector3 nearCenter = new Vector3(
                    treeXZ.x + Mathf.Cos(dir) * ringDist,
                    0f,
                    treeXZ.y + Mathf.Sin(dir) * ringDist
                );

                if (!TryValidatePropSurface(nearCenter, _edgeSafetyMargin, out float surfaceY))
                    continue;

                nearCenter.y = surfaceY + _surfaceYOffset;
                if (IsBlockedByTreeOrObstacle(nearCenter, 0.18f))
                    continue;

                int count = Random.Range(2, 5);
                SpawnFoliageCluster(nearCenter, count, _clusterRadius * 0.85f);
            }
        }

        // 3. Tempatkan rumpun di area kosong antar-pohon
        for (int c = 0; c < clusterCount; c++)
        {
            if (!TryPickSpacedTopPoint(_edgeSafetyMargin, 0.22f, out Vector3 clusterCenterWS))
                continue;

            int itemsInCluster = Random.Range(
                Mathf.Min(_itemsPerCluster.x, _itemsPerCluster.y),
                Mathf.Max(_itemsPerCluster.x, _itemsPerCluster.y) + 1
            );

            SpawnFoliageCluster(clusterCenterWS, itemsInCluster, _clusterRadius);
        }

        if (Application.isPlaying)
            SyncDirtyVisibility(force: true);
    }

    private void SpawnFoliageCluster(Vector3 clusterCenterWS, int itemsInCluster, float radiusLimit)
    {
        const float goldenAngle = 2.39996323f;
        float flowerEdgeMargin = Mathf.Max(_edgeSafetyMargin, _flowerAndRockEdgeMargin);

        bool centerAllowsFlowers = IsSafeFromOuterEdge(clusterCenterWS, flowerEdgeMargin);
        bool isFlowerPatch = centerAllowsFlowers
            && (_flowerPrefabs != null && _flowerPrefabs.Length > 0)
            && Random.value < (_flowerWeight / Mathf.Max(1f, _grassWeight + _flowerWeight));

        float startAngle = Random.Range(0f, Mathf.PI * 2f);

        for (int itemIdx = 0; itemIdx < itemsInCluster; itemIdx++)
        {
            Vector3 candidateWS = clusterCenterWS;
            float scaleFactor = 1.08f;

            if (itemIdx > 0)
            {
                float t = (float)itemIdx / Mathf.Max(1, itemsInCluster - 1);
                float angle = startAngle + itemIdx * goldenAngle + Random.Range(-0.32f, 0.32f);
                float radius = Mathf.Lerp(_minPropSpacing * 0.85f, radiusLimit, Mathf.Sqrt(t));
                candidateWS += new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                scaleFactor = Random.Range(0.75f, 0.98f);
            }

            if (!TryValidatePropSurface(candidateWS, _edgeSafetyMargin, out float surfaceY))
                continue;

            candidateWS.y = surfaceY + _surfaceYOffset;

            if (IsBlockedByTreeOrObstacle(candidateWS, 0.16f))
                continue;

            // Bunga dilarang muncul di dekat tepi pulau; area pinggir hanya diisi rumput
            bool allowFlowerHere = IsSafeFromOuterEdge(candidateWS, flowerEdgeMargin);
            GameObject prefab = PickFoliagePrefab(isFlowerPatch, itemIdx == 0, allowFlowerHere);
            if (prefab == null)
                continue;

            bool isGrass = IsGrassPrefab(prefab);
            bool isFlower = IsFlowerPrefab(prefab);
            float propRadius = 0.2f;

            if (IsTooCloseToAnyIslandProp(candidateWS, propRadius, isGrass, isFlower, isRock: false))
                continue;

            SpawnPropInstance(prefab, candidateWS, isRock: false, scaleMultiplier: scaleFactor);
            _spawnedProps.Add(new SpawnedPropInfo
            {
                worldPos = candidateWS,
                radius = propRadius,
                isGrass = isGrass,
                isFlower = isFlower,
                isRock = false
            });
        }
    }

    [ContextMenu("Clear Detailing")]
    public void ClearDetailing()
    {
        ResolveReferences();
        if (_detailingContainer == null) return;

        for (int i = _detailingContainer.childCount - 1; i >= 0; i--)
        {
            GameObject child = _detailingContainer.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        _spawnedProps.Clear();
    }

    private bool TryPickSpacedTopPoint(float requiredEdgeMargin, float obstacleFootprintRadius, out Vector3 safePoint)
    {
        safePoint = Vector3.zero;
        if (_topTriangles.Count == 0 || _totalSurfaceArea <= 0f) return false;

        bool found = false;
        float bestMinDistSqr = -1f;

        for (int attempt = 0; attempt < 40; attempt++)
        {
            float targetArea = Random.Range(0f, _totalSurfaceArea);
            TopTriangle tri = FindTriangleByArea(targetArea);

            float r1 = Mathf.Sqrt(Random.value);
            float r2 = Random.value;
            Vector3 candidate = (1f - r1) * tri.a + (r1 * (1f - r2)) * tri.b + (r1 * r2) * tri.c;

            if (!TryValidatePropSurface(candidate, requiredEdgeMargin, out float surfaceY))
                continue;

            if (IsBlockedByTreeOrObstacle(candidate, obstacleFootprintRadius))
                continue;

            candidate.y = surfaceY + _surfaceYOffset;
            float distSqr = GetNearestIslandPropDistSqr(candidate);
            if (distSqr > bestMinDistSqr)
            {
                bestMinDistSqr = distSqr;
                safePoint = candidate;
                found = true;
            }
        }

        return found;
    }

    private float GetNearestIslandPropDistSqr(Vector3 pos)
    {
        float minSqr = float.PositiveInfinity;

        for (int i = 0; i < _spawnedProps.Count; i++)
        {
            Vector2 d = new Vector2(_spawnedProps[i].worldPos.x - pos.x, _spawnedProps[i].worldPos.z - pos.z);
            if (d.sqrMagnitude < minSqr) minSqr = d.sqrMagnitude;
        }

        if (transform.parent != null)
        {
            BlockGroundDetailSpawner[] siblings = transform.parent.GetComponentsInChildren<BlockGroundDetailSpawner>(true);
            for (int i = 0; i < siblings.Length; i++)
            {
                BlockGroundDetailSpawner s = siblings[i];
                if (s == null || s == this) continue;
                for (int j = 0; j < s._spawnedProps.Count; j++)
                {
                    Vector2 d = new Vector2(s._spawnedProps[j].worldPos.x - pos.x, s._spawnedProps[j].worldPos.z - pos.z);
                    if (d.sqrMagnitude < minSqr) minSqr = d.sqrMagnitude;
                }
            }
        }

        return minSqr;
    }

    private TopTriangle FindTriangleByArea(float area)
    {
        int low = 0;
        int high = _topTriangles.Count - 1;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            if (_topTriangles[mid].cumulativeArea < area)
                low = mid + 1;
            else
                high = mid;
        }
        return _topTriangles[low];
    }

    private bool TryValidatePropSurface(Vector3 pointWS, float requiredEdgeMargin, out float surfaceY)
    {
        if (!TryGetSurfaceYFromList(_topTriangles, pointWS.x, pointWS.z, out surfaceY))
            return false;

        return IsSafeFromOuterEdge(pointWS, requiredEdgeMargin);
    }

    private bool IsSafeFromOuterEdge(Vector3 pointWS, float requiredEdgeMargin)
    {
        List<TopTriangle> surfaceList = _islandWalkableTriangles.Count > 0 ? _islandWalkableTriangles : _topTriangles;

        const int samples = 16;
        for (int i = 0; i < samples; i++)
        {
            float angle = i * (Mathf.PI * 2f / samples);
            float checkX = pointWS.x + Mathf.Cos(angle) * requiredEdgeMargin;
            float checkZ = pointWS.z + Mathf.Sin(angle) * requiredEdgeMargin;

            if (!TryGetSurfaceYFromList(surfaceList, checkX, checkZ, out _))
                return false;
        }

        if (_hasIslandOuterBounds)
        {
            if (pointWS.x < _islandOuterBounds.min.x + requiredEdgeMargin ||
                pointWS.x > _islandOuterBounds.max.x - requiredEdgeMargin ||
                pointWS.z < _islandOuterBounds.min.z + requiredEdgeMargin ||
                pointWS.z > _islandOuterBounds.max.z - requiredEdgeMargin)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetSurfaceYFromList(List<TopTriangle> triangles, float x, float z, out float y)
    {
        for (int i = 0; i < triangles.Count; i++)
        {
            TopTriangle tri = triangles[i];
            if (IsPointInTriangleXZ(x, z, tri.a, tri.b, tri.c))
            {
                y = (tri.a.y + tri.b.y + tri.c.y) / 3f;
                return true;
            }
        }

        y = 0f;
        return false;
    }

    private static bool IsPointInTriangleXZ(float px, float pz, Vector3 a, Vector3 b, Vector3 c)
    {
        float d1 = (px - b.x) * (a.z - b.z) - (a.x - b.x) * (pz - b.z);
        float d2 = (px - c.x) * (b.z - c.z) - (b.x - c.x) * (pz - c.z);
        float d3 = (px - a.x) * (c.z - a.z) - (c.x - a.x) * (pz - a.z);

        bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
        bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);

        return !(hasNeg && hasPos);
    }

    private bool IsBlockedByTreeOrObstacle(Vector3 worldPos, float footprintRadius)
    {
        float effectiveClear = _centerClearRadius + footprintRadius;
        Vector2 posXZ = new Vector2(worldPos.x, worldPos.z);
        float clearSqr = effectiveClear * effectiveClear;

        for (int i = 0; i < _nearbyTreePointsXZ.Count; i++)
        {
            if ((posXZ - _nearbyTreePointsXZ[i]).sqrMagnitude < clearSqr)
                return true;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(
            worldPos + Vector3.up * 0.35f,
            Mathf.Max(0.32f, _centerClearRadius * 0.75f + footprintRadius),
            _overlapBuffer,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null) continue;

            if (_blockTileRoot != null && col.transform.IsChildOf(_blockTileRoot)) continue;
            if (_detailingContainer != null && col.transform.IsChildOf(_detailingContainer)) continue;
            if (col.transform == transform) continue;

            return true;
        }

        return false;
    }

    private bool IsTooCloseToAnyIslandProp(Vector3 worldPos, float candidateRadius, bool isGrass, bool isFlower, bool isRock)
    {
        if (IsTooCloseToList(_spawnedProps, worldPos, candidateRadius, isGrass, isFlower, isRock))
            return true;

        if (transform.parent != null)
        {
            BlockGroundDetailSpawner[] siblings = transform.parent.GetComponentsInChildren<BlockGroundDetailSpawner>(true);
            for (int i = 0; i < siblings.Length; i++)
            {
                BlockGroundDetailSpawner sibling = siblings[i];
                if (sibling == null || sibling == this) continue;
                if (IsTooCloseToList(sibling._spawnedProps, worldPos, candidateRadius, isGrass, isFlower, isRock))
                    return true;
            }
        }

        return false;
    }

    private bool IsTooCloseToList(List<SpawnedPropInfo> list, Vector3 worldPos, float candidateRadius, bool isGrass, bool isFlower, bool isRock)
    {
        for (int i = 0; i < list.Count; i++)
        {
            SpawnedPropInfo existing = list[i];
            float reqSpacing;

            if (isRock && existing.isRock)
            {
                reqSpacing = _minRockSpacing;
            }
            else if (isRock || existing.isRock)
            {
                reqSpacing = (isFlower || existing.isFlower)
                    ? (_rockBodyRadius + candidateRadius + 0.14f)
                    : (_rockBodyRadius * 0.80f);
            }
            else if (isGrass && existing.isGrass)
            {
                reqSpacing = _minPropSpacing * _grassSpacingMultiplier;
            }
            else
            {
                reqSpacing = _minPropSpacing;
            }

            Vector2 diff = new Vector2(existing.worldPos.x - worldPos.x, existing.worldPos.z - worldPos.z);
            if (diff.sqrMagnitude < (reqSpacing * reqSpacing))
                return true;
        }

        return false;
    }

    private GameObject PickFoliagePrefab(bool isFlowerPatch, bool isCenterItem, bool allowFlower)
    {
        bool hasGrass = _grassPrefabs != null && _grassPrefabs.Length > 0;
        bool hasFlower = allowFlower && _flowerPrefabs != null && _flowerPrefabs.Length > 0;

        if (!hasGrass && !hasFlower) return null;
        if (!hasFlower) return GetRandomFromArray(_grassPrefabs);
        if (!hasGrass) return GetRandomFromArray(_flowerPrefabs);

        if (isFlowerPatch)
        {
            return (isCenterItem || Random.value < 0.78f)
                ? GetRandomFromArray(_flowerPrefabs)
                : GetRandomFromArray(_grassPrefabs);
        }
        else
        {
            return (!isCenterItem && Random.value < 0.28f)
                ? GetRandomFromArray(_flowerPrefabs)
                : GetRandomFromArray(_grassPrefabs);
        }
    }

    private static GameObject GetRandomFromArray(GameObject[] arr)
    {
        if (arr == null || arr.Length == 0) return null;
        return arr[Random.Range(0, arr.Length)];
    }

    private bool IsGrassPrefab(GameObject prefab)
    {
        if (_grassPrefabs == null) return false;
        for (int i = 0; i < _grassPrefabs.Length; i++)
        {
            if (_grassPrefabs[i] == prefab) return true;
        }
        return false;
    }

    private bool IsFlowerPrefab(GameObject prefab)
    {
        if (_flowerPrefabs == null) return false;
        for (int i = 0; i < _flowerPrefabs.Length; i++)
        {
            if (_flowerPrefabs[i] == prefab) return true;
        }
        return false;
    }

    private bool HasAnyValidPrefab()
    {
        return (_grassPrefabs != null && _grassPrefabs.Length > 0)
            || (_flowerPrefabs != null && _flowerPrefabs.Length > 0)
            || (_rockPrefabs != null && _rockPrefabs.Length > 0);
    }

    private void SpawnPropInstance(GameObject prefab, Vector3 worldPos, bool isRock, float scaleMultiplier)
    {
        float randomYaw = Random.Range(0f, 360f);
        float tiltX = Random.Range(-_maxTiltAngle, _maxTiltAngle);
        float tiltZ = Random.Range(-_maxTiltAngle, _maxTiltAngle);
        Quaternion rot = Quaternion.Euler(tiltX, randomYaw, tiltZ);

        GameObject instance = Instantiate(prefab, worldPos, rot, _detailingContainer);

        Vector2 activeScaleRange = isRock ? _rockScaleRange : _scaleRange;
        float baseScale = Random.Range(
            Mathf.Min(activeScaleRange.x, activeScaleRange.y),
            Mathf.Max(activeScaleRange.x, activeScaleRange.y)
        );
        instance.transform.localScale = prefab.transform.localScale * (baseScale * scaleMultiplier);

        Collider[] cols = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            cols[i].enabled = false;
        }
    }

    public void ConfigureFromIslandDefinition(
        GameObject[] grassPrefabs, GameObject[] flowerPrefabs, GameObject[] rockPrefabs, GameObject dirtyRoot)
    {
        _grassPrefabs = grassPrefabs;
        _flowerPrefabs = flowerPrefabs;
        _rockPrefabs = rockPrefabs;
        _dirtyVisualsRoot = dirtyRoot;
        _spawnOnStart = false;
    }

    public void SetExternalSurfaceAndTrees(
        MeshFilter[] partitionFilters, IReadOnlyList<Vector3> allIslandTreePositions, Bounds outerBounds)
    {
        _externalSurfaceFilters = partitionFilters;
        _islandOuterBounds = outerBounds;
        _hasIslandOuterBounds = true;

        _externalTreePointsXZ.Clear();
        if (allIslandTreePositions != null)
        {
            for (int i = 0; i < allIslandTreePositions.Count; i++)
                _externalTreePointsXZ.Add(new Vector2(allIslandTreePositions[i].x, allIslandTreePositions[i].z));
        }
    }
}