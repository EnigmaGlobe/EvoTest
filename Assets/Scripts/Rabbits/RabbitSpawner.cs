using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

public class RabbitSpawner : MonoBehaviour
{
    // Fired at the end of each round. Passes the 1-based round index.
    public System.Action<int> OnRoundEnded;

    // NEW: emit FormResponse events (ExperimentController will subscribe)
    public System.Action<FormResponse> OnFormResponse;

    [Header("Prefab (drag in)")]
    [SerializeField] private RabbitTarget rabbitPrefab;

    [Header("Session + Round Settings")]
    [SerializeField] private float sessionDuration = 60f;
    [SerializeField] private float roundDuration = 10f;

    [Header("Rabbit Count Scaling")]
    [SerializeField] private int baseRabbitsPerRound = 3;
    [SerializeField] private int rabbitsIncreasePerMinute = 2;
    [SerializeField] private int maxRabbitsPerRound = 30;

    [Header("Ground + NavMesh (Terrain)")]
    [SerializeField] private float navmeshSampleRadius = 8f;
    [SerializeField] private int spawnTries = 40;
    [SerializeField] private float minSpawnSeparation = 4f;
    [SerializeField] private float terrainEdgePadding = 3f;
    [SerializeField] private float navmeshEdgeBuffer = 1.2f;

    [Header("Spawn Area (viewport)")]
    [SerializeField] private float viewportPadding = 0.08f;

    [Header("Rabbit Movement (anti-still)")]
    [SerializeField] private float roamRadius = 12f;
    [SerializeField] private float stuckCheckInterval = 1.0f;
    [SerializeField] private float stuckMoveThreshold = 0.05f;
    [SerializeField] private float repathIfIdleDistance = 0.6f;

    [Header("UI (TMP)")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text hitText;

    [Header("References")]
    [SerializeField] private Camera mainCamera;

    // NEW: set by ExperimentController, used for event payload
    [Header("Logging (filled by ExperimentController)")]
    [SerializeField] private string formId;
    [SerializeField] private string userId;
    [SerializeField] private string sessionId;
    [SerializeField] private string dialogueId;
    [SerializeField] private string envId;

    // Track elapsed so respawns log correct time without resetting session
    private float sessionElapsedSeconds = 0f;

    // Keep the current round's intended rabbit count so we can respawn same amount
    private int rabbitsPlannedThisRound = 0;

    private readonly List<RabbitTarget> alive = new List<RabbitTarget>();
    private int hits = 0;

    private bool sessionRunning = false;
    private Coroutine sessionCo;

    private Terrain terrain;

    private float sessionRemaining;
    private float roundRemaining;

    // NEW: round tracking
    private int currentRoundIndex = 0;
    private int spawnedThisRound = 0;
    private int hitsThisRound = 0;
    private int spawnFailsThisRound = 0;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        terrain = Terrain.activeTerrain;

        UpdateHitUI();
        sessionRemaining = sessionDuration;
        roundRemaining = roundDuration;
        UpdateTimerUI();
    }

    /// <summary>
    /// Called by ExperimentController to configure IDs for logging.
    /// </summary>
    public void SetLoggingIds(string _formId, string dialogueId, string userId, string envId)
    {
        this.formId = _formId;
        this.userId = userId;
        this.envId = envId;
        this.dialogueId = dialogueId;
    }

    // Starts the whole session (sessionDuration) with rounds of roundDuration
    public void StartRound()
    {
        // ✅ If already running, DO NOT restart session — just respawn current round rabbits
        if (sessionRunning)
        {
            RespawnCurrentRoundRabbits();
            return;
        }

        // Otherwise start the whole session (your original behavior)
        if (sessionCo != null) StopCoroutine(sessionCo);

        CleanupRabbits();

        hits = 0;
        UpdateHitUI();

        sessionRemaining = sessionDuration;
        roundRemaining = roundDuration;
        sessionElapsedSeconds = 0f;

        UpdateTimerUI();

        sessionCo = StartCoroutine(SessionRoutine());
    }

    private void RespawnCurrentRoundRabbits()
    {
        if (rabbitPrefab == null || mainCamera == null) return;

        // If we're somehow called before the first round sets this, estimate quickly
        int count = rabbitsPlannedThisRound;
        if (count <= 0)
            count = Mathf.Clamp(baseRabbitsPerRound, 0, maxRabbitsPerRound);

        CleanupRabbits();

        // Optional: reset only spawn counters for this new set
        spawnedThisRound = 0;
        // keep hitsThisRound as-is (so players don't lose hits)
        // spawnFailsThisRound = 0; // reset if you prefer

        float elapsed = sessionElapsedSeconds;

        for (int i = 0; i < count; i++)
            SpawnOneRabbit(elapsed);
    }



    private IEnumerator SessionRoutine()
    {
        
        sessionRunning = true;

        float elapsed = 0f;
        int totalRounds = Mathf.CeilToInt(sessionDuration / roundDuration);
        int roundIndex = 0;

        Coroutine stuckCo = StartCoroutine(StuckFixerRoutine());

        while (elapsed < sessionDuration)
        {
            roundIndex++;
            currentRoundIndex = roundIndex;

            // reset round counters
            spawnedThisRound = 0;
            hitsThisRound = 0;
            spawnFailsThisRound = 0;

            // Rabbit scaling based on minute index
            int minuteIndex = Mathf.FloorToInt(elapsed / 60f);
            int rabbitsThisRound = baseRabbitsPerRound + minuteIndex * rabbitsIncreasePerMinute;
            rabbitsThisRound = Mathf.Clamp(rabbitsThisRound, 0, maxRabbitsPerRound);
            rabbitsThisRound = Mathf.Clamp(rabbitsThisRound, 0, maxRabbitsPerRound);
            rabbitsPlannedThisRound = rabbitsThisRound; // ✅ remember planned count this round
            CleanupRabbits();

            for (int i = 0; i < rabbitsThisRound; i++)
                SpawnOneRabbit(elapsed);

            // Round timer
            float t = 0f;
            while (t < roundDuration && elapsed < sessionDuration)
            {
                t += Time.deltaTime;
                elapsed += Time.deltaTime;

                sessionElapsedSeconds = elapsed; // ✅ keep for respawn timestamps

                sessionRemaining = Mathf.Max(0f, sessionDuration - elapsed);
                roundRemaining = Mathf.Max(0f, roundDuration - t);
                UpdateTimerUI(roundIndex, totalRounds);

                yield return null;
            }

            // Emit round_end summary
            EmitRoundEnd(elapsed, rabbitsThisRound);

            // End-of-round callback (your ExperimentCondition listens to this)
            OnRoundEnded?.Invoke(roundIndex);
        }

        sessionRunning = false;

        if (stuckCo != null) StopCoroutine(stuckCo);
        CleanupRabbits();

        // Final UI snap
        sessionRemaining = 0f;
        roundRemaining = 0f;
        UpdateTimerUI(totalRounds, totalRounds);
    }

    private void SpawnOneRabbit(float elapsedSeconds)
    {
        if (rabbitPrefab == null || mainCamera == null) return;

        if (!TryGetSpawnPos(out Vector3 pos))
        {
            spawnFailsThisRound++;
            EmitSpawnFail(elapsedSeconds);
            Debug.LogWarning("[Spawn] Failed to find valid spawn point on NavMesh (in view).");
            return;
        }

        RabbitTarget rabbit = Instantiate(rabbitPrefab, pos, Quaternion.identity);
        rabbit.gameObject.SetActive(true);

        // Assign id + spawn time + round index to rabbit
        rabbit.AssignMeta(currentRoundIndex);

        // Optional: ensure agent is snapped correctly if agent is on the same object
        NavMeshAgent agent = rabbit.GetComponentInParent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(pos);
            agent.isStopped = false;

            if (agent.speed <= 0.01f) agent.speed = 3.5f;
            if (agent.acceleration <= 0.01f) agent.acceleration = 8f;

            agent.ResetPath();
            GiveRandomDestination(agent, pos);
        }

        rabbit.Init(mainCamera, viewportPadding);
        rabbit.OnHit += HandleRabbitHit;
        alive.Add(rabbit);

        spawnedThisRound++;

        // Emit spawn FormResponse
        EmitSpawn(rabbit, elapsedSeconds, pos);
    }

    // Picks a random reachable point on navmesh near "origin" and sets destination
    private void GiveRandomDestination(NavMeshAgent agent, Vector3 origin)
    {
        if (agent == null || !agent.isOnNavMesh) return;

        for (int i = 0; i < 10; i++)
        {
            Vector2 r = Random.insideUnitCircle * roamRadius;
            Vector3 candidate = new Vector3(origin.x + r.x, origin.y + 2f, origin.z + r.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navmeshSampleRadius, NavMesh.AllAreas))
            {
                // ✅ keep movement destination on-screen
                if (!IsInCameraView(hit.position))
                    continue;

                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    agent.SetDestination(hit.position);
                    return;
                }
            }
        }
    }


    // Periodically nudges agents that are idle/stuck
    private IEnumerator StuckFixerRoutine()
    {
        Dictionary<NavMeshAgent, Vector3> lastPos = new Dictionary<NavMeshAgent, Vector3>();

        while (sessionRunning)
        {
            for (int i = 0; i < alive.Count; i++)
            {
                if (alive[i] == null) continue;

                NavMeshAgent agent = alive[i].GetComponentInParent<NavMeshAgent>();
                if (agent == null || !agent.enabled || !agent.isOnNavMesh) continue;

                Vector3 pos = agent.transform.position;

                if (!lastPos.ContainsKey(agent))
                    lastPos[agent] = pos;

                float moved = Vector3.Distance(pos, lastPos[agent]);
                lastPos[agent] = pos;

                bool noGoodPath = !agent.hasPath || agent.pathPending;
                bool tooIdle = agent.remainingDistance <= repathIfIdleDistance;

                if (moved < stuckMoveThreshold && (noGoodPath || tooIdle))
                {
                    agent.ResetPath();
                    GiveRandomDestination(agent, pos);
                }
            }

            yield return new WaitForSeconds(stuckCheckInterval);
        }
    }

    // ✅ Hard camera check helper
    private bool IsInCameraView(Vector3 worldPos)
    {
        if (mainCamera == null) return true;

        Vector3 vp = mainCamera.WorldToViewportPoint(worldPos);
        if (vp.z <= 0f) return false;

        return (vp.x >= viewportPadding && vp.x <= 1f - viewportPadding &&
                vp.y >= viewportPadding && vp.y <= 1f - viewportPadding);
    }

    // Spawn point: visible viewport -> terrain -> snap to navmesh -> avoid edges/overlaps -> final camera check
    private bool TryGetSpawnPos(out Vector3 pos)
    {
        pos = default;

        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null) return false;

        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = terrain.terrainData.size;

        for (int attempt = 0; attempt < spawnTries; attempt++)
        {
            float vx = Random.Range(viewportPadding, 1f - viewportPadding);
            float vy = Random.Range(viewportPadding, 1f - viewportPadding);

            Ray ray = mainCamera.ViewportPointToRay(new Vector3(vx, vy, 0f));

            if (!RaycastToTerrainXZ(ray, tPos, tSize, out Vector3 candidateOnTerrain))
                continue;

            if (!NavMesh.SamplePosition(candidateOnTerrain, out NavMeshHit navHit, navmeshSampleRadius, NavMesh.AllAreas))
                continue;

            Vector3 snapped = navHit.position;

            // Avoid spawning near navmesh edge
            if (NavMesh.FindClosestEdge(snapped, out NavMeshHit edgeHit, NavMesh.AllAreas))
            {
                if (edgeHit.distance < navmeshEdgeBuffer)
                    continue;
            }

            // Avoid overlaps
            bool tooClose = false;
            for (int i = 0; i < alive.Count; i++)
            {
                if (alive[i] == null) continue;

                Vector3 otherPos = alive[i].transform.position;
                NavMeshAgent otherAgent = alive[i].GetComponentInParent<NavMeshAgent>();
                if (otherAgent != null) otherPos = otherAgent.transform.position;

                if (Vector3.Distance(snapped, otherPos) < minSpawnSeparation)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            // must be visible
            if (!IsInCameraView(snapped))
                continue;

            pos = snapped;
            return true;
        }

        return false;
    }

    private bool RaycastToTerrainXZ(Ray ray, Vector3 terrainPos, Vector3 terrainSize, out Vector3 terrainPoint)
    {
        terrainPoint = default;

        Plane basePlane = new Plane(Vector3.up, new Vector3(0f, terrainPos.y, 0f));
        if (!basePlane.Raycast(ray, out float enter))
            return false;

        Vector3 hit = ray.GetPoint(enter);

        float x = Mathf.Clamp(hit.x, terrainPos.x + terrainEdgePadding, terrainPos.x + terrainSize.x - terrainEdgePadding);
        float z = Mathf.Clamp(hit.z, terrainPos.z + terrainEdgePadding, terrainPos.z + terrainSize.z - terrainEdgePadding);

        float maxOutside = 50f;
        if (hit.x < terrainPos.x - maxOutside || hit.x > terrainPos.x + terrainSize.x + maxOutside ||
            hit.z < terrainPos.z - maxOutside || hit.z > terrainPos.z + terrainSize.z + maxOutside)
            return false;

        float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrainPos.y;

        terrainPoint = new Vector3(x, y, z);
        return true;
    }

    private void HandleRabbitHit(RabbitTarget rabbit)
    {
        if (!sessionRunning) return;

        hits++;
        hitsThisRound++;
        UpdateHitUI();

        EmitHit(rabbit, GetElapsedSeconds());
    }

    private float GetElapsedSeconds()
    {
        // session elapsed = sessionDuration - sessionRemaining
        return Mathf.Max(0f, sessionDuration - sessionRemaining);
    }

    private void UpdateHitUI()
    {
        if (hitText != null)
            hitText.text = $"Hits: {hits}";
    }

    private void UpdateTimerUI(int currentRound = 0, int totalRounds = 0)
    {
        if (timerText == null) return;

        int sessionSec = Mathf.CeilToInt(sessionRemaining);
        int sMin = sessionSec / 60;
        int sSec = sessionSec % 60;

        int roundSec = Mathf.CeilToInt(roundRemaining);

        if (totalRounds > 0)
            timerText.text = $"Time: {sMin:00}:{sSec:00}  |  Round: {currentRound}/{totalRounds}  ({roundSec:00}s)";
        else
            timerText.text = $"Time: {sMin:00}:{sSec:00}  |  Round: {roundSec:00}s";
    }

    private void CleanupRabbits()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] != null) Destroy(alive[i].gameObject);
        }
        alive.Clear();
    }

    // Optional: allow ExperimentConditionToggle to set timings
    public void SetTimings(float sessionSeconds, float roundSeconds)
    {
        sessionDuration = Mathf.Max(1f, sessionSeconds);
        roundDuration = Mathf.Max(0.1f, roundSeconds);
    }

    public float RoundDuration => roundDuration;
    public float SessionDuration => sessionDuration;

    // -------------------------
    // FormResponse emit helpers
    // -------------------------

    private void EmitSpawn(RabbitTarget rabbit, float elapsedSeconds, Vector3 worldPos)
    {
        if (rabbit == null || mainCamera == null) return;

        Vector3 vp = mainCamera.WorldToViewportPoint(worldPos);
        float dist = Vector3.Distance(mainCamera.transform.position, worldPos);

        var fr = FormResponse.NewEvent(formId, userId, sessionId, envId, "spawn", elapsedSeconds, dialogueId);
        fr.battleId = currentRoundIndex.ToString();
        fr.interactableId = rabbit.RabbitId;
        fr.status = "ok";

        var data = new SimpleRabbitData
        {
            rabbitId = rabbit.RabbitId,
            roundIndex = currentRoundIndex,
            eventType = "spawn",
            x = worldPos.x,
            y = worldPos.y,
            z = worldPos.z,
            vpx = vp.x,
            vpy = vp.y,
            vpz = vp.z,
            distance = dist
        };
        fr.objectDatas.Add(JsonUtility.ToJson(data));

        OnFormResponse?.Invoke(fr);
    }

    private void EmitHit(RabbitTarget rabbit, float elapsedSeconds)
    {
        if (rabbit == null || mainCamera == null) return;

        Vector3 worldPos = rabbit.transform.position;
        Vector3 vp = mainCamera.WorldToViewportPoint(worldPos);
        float dist = Vector3.Distance(mainCamera.transform.position, worldPos);

        float timeFromSpawn = elapsedSeconds - rabbit.SpawnElapsedSeconds;
        if (timeFromSpawn < 0f) timeFromSpawn = 0f;

        var fr = FormResponse.NewEvent(formId, userId, sessionId, envId, "hit", elapsedSeconds, dialogueId);
        fr.battleId = currentRoundIndex.ToString();
        fr.interactableId = rabbit.RabbitId;
        fr.isAnsCorrect = true;
        fr.status = "ok";

        var data = new SimpleRabbitData
        {
            rabbitId = rabbit.RabbitId,
            roundIndex = currentRoundIndex,
            eventType = "hit",
            timeFromSpawn = timeFromSpawn,
            x = worldPos.x,
            y = worldPos.y,
            z = worldPos.z,
            vpx = vp.x,
            vpy = vp.y,
            vpz = vp.z,
            distance = dist
        };
        fr.objectDatas.Add(JsonUtility.ToJson(data));

        OnFormResponse?.Invoke(fr);
    }

    private void EmitSpawnFail(float elapsedSeconds)
    {
        var fr = FormResponse.NewEvent(formId, userId, sessionId, envId, "spawn_fail", elapsedSeconds, dialogueId);
        fr.battleId = currentRoundIndex.ToString();
        fr.status = "failed_spawn";
        OnFormResponse?.Invoke(fr);
    }

    private void EmitRoundEnd(float elapsedSeconds, int rabbitsPlannedThisRound)
    {
        var fr = FormResponse.NewEvent(formId, userId, sessionId, envId, "round_end", elapsedSeconds, dialogueId);
        fr.battleId = currentRoundIndex.ToString();
        fr.status = "ok";

        var summary = new RoundSummaryData
        {
            roundIndex = currentRoundIndex,
            rabbitsPlanned = rabbitsPlannedThisRound,
            rabbitsSpawned = spawnedThisRound,
            rabbitsHit = hitsThisRound,
            spawnFails = spawnFailsThisRound
        };
        fr.objectDatas.Add(JsonUtility.ToJson(summary));

        OnFormResponse?.Invoke(fr);
    }
}
