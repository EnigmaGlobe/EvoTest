using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class RabbitTarget : MonoBehaviour
{
    public event Action<RabbitTarget> OnHit;

    [Header("NavMesh Movement")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private float roamRadius = 6f;
    [SerializeField] private Vector2 changeDestEvery = new Vector2(0.6f, 1.3f);

    [Header("Keep inside map")]
    [SerializeField] private Terrain terrain;
    [SerializeField] private float terrainEdgePadding = 2f;
    [SerializeField] private float navmeshEdgeBuffer = 1.2f;
    [SerializeField] private int destTries = 25;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string runTrigger = "Run";
    [SerializeField] private string deadTrigger = "Dead";
    [SerializeField] private float destroyAfterDeadSeconds = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private float debugEverySeconds = 1.0f;

    private bool dead;
    private Coroutine roamCo;
    private Coroutine debugCo;

    public void Init(Camera _cam, float _padding)
    {
        // Find agent in hierarchy (covers parent/child cases)
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInChildren<NavMeshAgent>();

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (terrain == null) terrain = Terrain.activeTerrain;

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.SetTrigger(runTrigger);
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (agent == null)
        {
            Debug.LogError($"[RabbitTarget:{name}] NO NavMeshAgent found in prefab hierarchy.");
            return;
        }

        agent.enabled = true;
        agent.isStopped = false;

        // ✅ Ensure agent drives transform
        agent.updatePosition = true;
        agent.updateRotation = true;

        // ✅ OVERRIDE speed values (not "only if <= 0.01")
        agent.speed = 1.2f;          // slow
        agent.acceleration = 4f;     // smooth
        agent.angularSpeed = 60f;    // slow turning

        // ✅ Prevent “floating” due to base offset
        agent.baseOffset = 0f;

        // ✅ Snap onto navmesh at correct height
        if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            if (debugLogs)
                Debug.Log($"[RabbitTarget:{name}] Warp to NavMesh. From {agent.transform.position} -> {hit.position}");

            agent.Warp(hit.position);
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning($"[RabbitTarget:{name}] SamplePosition failed near {agent.transform.position}");
        }

        if (roamCo != null) StopCoroutine(roamCo);
        roamCo = StartCoroutine(RoamLoop());

        if (debugCo != null) StopCoroutine(debugCo);
        if (debugLogs) debugCo = StartCoroutine(DebugLoop());

        if (debugLogs) DumpAgentState("Init()");
    }

    private IEnumerator RoamLoop()
    {
        while (!dead)
        {
            SetRandomDestinationSafe();
            float wait = UnityEngine.Random.Range(changeDestEvery.x, changeDestEvery.y);
            yield return new WaitForSeconds(wait);
        }
    }

    private void SetRandomDestinationSafe()
    {
        if (agent == null)
        {
            if (debugLogs) Debug.LogWarning($"[RabbitTarget:{name}] No agent.");
            return;
        }
        if (!agent.enabled)
        {
            if (debugLogs) Debug.LogWarning($"[RabbitTarget:{name}] Agent disabled.");
            return;
        }
        if (!agent.isOnNavMesh)
        {
            if (debugLogs) Debug.LogWarning($"[RabbitTarget:{name}] Agent NOT on NavMesh at {transform.position}.");
            return;
        }

        for (int i = 0; i < destTries; i++)
        {
            Vector2 rnd = UnityEngine.Random.insideUnitCircle * roamRadius;
            Vector3 candidate = agent.transform.position + new Vector3(rnd.x, 0f, rnd.y);
            candidate = ClampToTerrain(candidate);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, roamRadius, NavMesh.AllAreas))
                continue;

            if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
            {
                if (edgeHit.distance < navmeshEdgeBuffer)
                    continue;
            }

            NavMeshPath path = new NavMeshPath();
            bool hasPath = agent.CalculatePath(hit.position, path);

            if (!hasPath || path.status != NavMeshPathStatus.PathComplete)
            {
                if (debugLogs)
                    Debug.Log($"[RabbitTarget:{name}] Try {i}: path NOT complete (status={path.status}) to {hit.position}");
                continue;
            }

            agent.ResetPath();
            agent.SetDestination(hit.position);

            if (debugLogs)
                Debug.Log($"[RabbitTarget:{name}] SetDestination -> {hit.position} (remainingDist={agent.remainingDistance:0.00})");

            return;
        }

        if (debugLogs)
            Debug.LogWarning($"[RabbitTarget:{name}] Could not find safe destination after {destTries} tries.");
    }

    private Vector3 ClampToTerrain(Vector3 p)
    {
        if (terrain == null) return p;

        Vector3 tp = terrain.transform.position;
        Vector3 ts = terrain.terrainData.size;

        p.x = Mathf.Clamp(p.x, tp.x + terrainEdgePadding, tp.x + ts.x - terrainEdgePadding);
        p.z = Mathf.Clamp(p.z, tp.z + terrainEdgePadding, tp.z + ts.z - terrainEdgePadding);

        return p;
    }

    private IEnumerator DebugLoop()
    {
        var wait = new WaitForSeconds(debugEverySeconds);
        while (!dead)
        {
            DumpAgentState("Tick");
            yield return wait;
        }
    }

    private void DumpAgentState(string tag)
    {
        if (!debugLogs) return;

        if (agent == null)
        {
            Debug.Log($"[RabbitTarget:{name}] {tag} agent=NULL");
            return;
        }

        Vector3 v = agent.velocity;
        Debug.Log(
            $"[RabbitTarget:{name}] {tag} " +
            $"pos={transform.position} onNav={agent.isOnNavMesh} enabled={agent.enabled} stopped={agent.isStopped} " +
            $"updPos={agent.updatePosition} updRot={agent.updateRotation} " +
            $"speed={agent.speed:0.00} accel={agent.acceleration:0.00} " +
            $"hasPath={agent.hasPath} pending={agent.pathPending} " +
            $"remDist={agent.remainingDistance:0.00} " +
            $"vel=({v.x:0.00},{v.y:0.00},{v.z:0.00})"
        );
    }

    public void TryHit()
    {
        if (dead) return;
        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        dead = true;

        if (roamCo != null) StopCoroutine(roamCo);
        if (debugCo != null) StopCoroutine(debugCo);

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        OnHit?.Invoke(this);

        if (animator != null) animator.SetTrigger(deadTrigger);

        yield return new WaitForSeconds(destroyAfterDeadSeconds);
        Destroy(gameObject);
    }
}
