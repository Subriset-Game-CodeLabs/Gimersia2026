using System.Collections;
using UnityEngine;

public class EnemyWaterInteraction : MonoBehaviour
{
    [Header("Referensi Permukaan Air")]
    [Tooltip("Ketinggian Y dari objek Plane air laut di scene")]
    public float waterSurfaceY = 0f;

    [Header("Pengaturan Spawn dari Dalam Air")]
    public bool emergeFromWater = true;
    public float submergeDepth = 2.0f;
    public float telegraphDuration = 1.2f;
    public float emergeDuration = 0.8f;

    [Header("Prefab VFX Low-Poly")]
    public GameObject telegraphVfxPrefab;
    public GameObject splashVfxPrefab;

    [Header("Riak Pergerakan di Air")]
    public float moveRippleInterval = 0.45f;

    private bool hasBreachedSurface;
    private float moveRippleTimer;
    private Vector3 lastPosition;

    void Start()
    {
        lastPosition = transform.position;

        if (emergeFromWater)
        {
            StartCoroutine(EmergeSequence());
        }
        else
        {
            hasBreachedSurface = transform.position.y >= waterSurfaceY;
        }
    }

    private IEnumerator EmergeSequence()
    {
        hasBreachedSurface = false;
        Vector3 surfacePos = new Vector3(transform.position.x, waterSurfaceY, transform.position.z);
        Vector3 startPos = surfacePos + Vector3.down * submergeDepth;
        transform.position = startPos;

        // 1. Munculkan gelembung peringatan di permukaan air sebelum musuh naik
        if (telegraphVfxPrefab != null)
        {
            GameObject telegraph = Instantiate(telegraphVfxPrefab, surfacePos + Vector3.up * 0.05f, Quaternion.identity);
            Destroy(telegraph, telegraphDuration + 1.0f);
        }

        yield return new WaitForSeconds(telegraphDuration);

        // 2. Gerakkan musuh naik menembus permukaan air
        float elapsed = 0f;
        while (elapsed < emergeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / emergeDuration);
            transform.position = Vector3.Lerp(startPos, surfacePos, t);

            CheckSurfaceBreach();
            yield return null;
        }

        transform.position = surfacePos;
        CheckSurfaceBreach();
    }

    void Update()
    {
        // Memicu percikan jika musuh terbang/lompat dan menyentuh permukaan air dari atas atau bawah
        CheckSurfaceBreach();

        // Memicu gelembung kecil berkala saat musuh bergerak menerjang air dangkal
        if (hasBreachedSurface && transform.position.y <= waterSurfaceY + 0.25f)
        {
            float movedDistance = Vector3.Distance(transform.position, lastPosition);
            if (movedDistance > 0.01f)
            {
                moveRippleTimer += Time.deltaTime;
                if (moveRippleTimer >= moveRippleInterval)
                {
                    moveRippleTimer = 0f;
                    SpawnSplash(0.5f);
                }
            }
        }

        lastPosition = transform.position;
    }

    private void CheckSurfaceBreach()
    {
        if (!hasBreachedSurface && transform.position.y >= waterSurfaceY - 0.05f)
        {
            hasBreachedSurface = true;
            SpawnSplash(1.0f);
        }
    }

    private void SpawnSplash(float scaleMultiplier)
    {
        if (splashVfxPrefab == null) return;

        Vector3 splashPos = new Vector3(transform.position.x, waterSurfaceY + 0.05f, transform.position.z);
        GameObject splash = Instantiate(splashVfxPrefab, splashPos, Quaternion.identity);
        splash.transform.localScale *= scaleMultiplier;
        Destroy(splash, 1.5f);
    }
}