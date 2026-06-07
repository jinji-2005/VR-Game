using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class PlayerRespawnController : MonoBehaviour
{
    [Serializable]
    private class RespawnCheckpoint
    {
        public string name = "Checkpoint";
        public Vector3 position;
        public float activationRadius = 8f;
        public float heightTolerance = 6f;
    }

    [Header("Audio")]
    [SerializeField] private AudioSource deathAudioSource;

    [Header("Void Detection")]
    [SerializeField] private float hardRespawnY = -20f;
    [SerializeField] private float voidProbeDistance = 18f;
    [SerializeField] private float minVoidFallSpeed = -4f;
    [SerializeField] private float voidRespawnDelay = 1.25f;
    [SerializeField] private float respawnCooldown = 0.6f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Respawn")]
    [SerializeField] private float respawnHeightOffset = 0.2f;
    [SerializeField] private bool resetLookPitchOnRespawn = false;
    [SerializeField] private bool logCheckpointActivation = false;

    [Header("Checkpoints")]
    [SerializeField] private RespawnCheckpoint[] checkpoints = Array.Empty<RespawnCheckpoint>();

    [Header("Checkpoint Visuals")]
    [SerializeField] private bool showCheckpointVisuals = true;
    [SerializeField] private float visualHeightOffset = 1.3f;
    [SerializeField] private float checkpointLightRange = 9f;
    [SerializeField] private float checkpointLightIntensity = 1.7f;
    [SerializeField] private Color checkpointLightColor = new Color(0.26f, 0.56f, 1f, 1f);
    [SerializeField] private Color activeCheckpointLightColor = new Color(0.5f, 0.86f, 1f, 1f);

    private CharacterController characterController;
    private PlayerController playerController;
    private Vector3 fallbackSpawnPosition;
    private Quaternion fallbackSpawnRotation;
    private Vector3 previousPosition;
    private float voidTimer;
    private float respawnCooldownTimer;
    private int activeCheckpointIndex = -1;
    private Quaternion activeCheckpointRotation;
    private Transform checkpointVisualRoot;
    private readonly List<CheckpointVisualInstance> checkpointVisuals = new();
    private static Material checkpointCoreTemplate;
    private static Material checkpointParticleTemplate;

    private sealed class CheckpointVisualInstance
    {
        public Transform root;
        public Light pointLight;
        public Transform core;
        public Material coreMaterial;
        public ParticleSystem stars;
        public ParticleSystem motes;
        public float phaseOffset;
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();
        fallbackSpawnPosition = transform.position;
        fallbackSpawnRotation = transform.rotation;
        previousPosition = transform.position;
    }

    private void Start()
    {
        TryActivateNearestCheckpoint(forceActivation: true);
        RebuildCheckpointVisuals();
    }

    private void Update()
    {
        if (respawnCooldownTimer > 0f)
            respawnCooldownTimer -= Time.deltaTime;

        TryActivateNearestCheckpoint(forceActivation: false);
        EvaluateVoidFall();
        UpdateCheckpointVisuals();
        previousPosition = transform.position;
    }

    [ContextMenu("Load Level45 Defaults")]
    private void LoadLevel45Defaults()
    {
        checkpoints = BuildLevel45Defaults();
        RebuildCheckpointVisuals();
    }

    private void OnDisable()
    {
        ClearCheckpointVisuals();
    }

    private void EvaluateVoidFall()
    {
        if (respawnCooldownTimer > 0f)
        {
            voidTimer = 0f;
            return;
        }

        if (transform.position.y <= hardRespawnY)
        {
            RespawnAtActiveCheckpoint();
            return;
        }

        if (characterController != null && characterController.isGrounded)
        {
            voidTimer = 0f;
            return;
        }

        float verticalSpeed = Time.deltaTime > 0f
            ? (transform.position.y - previousPosition.y) / Time.deltaTime
            : 0f;

        bool isFallingFastEnough = verticalSpeed <= minVoidFallSpeed;
        bool hasGroundBelow = Physics.Raycast(
            GetGroundProbeOrigin(),
            Vector3.down,
            voidProbeDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (isFallingFastEnough && !hasGroundBelow)
        {
            voidTimer += Time.deltaTime;
            if (voidTimer >= voidRespawnDelay)
                RespawnAtActiveCheckpoint();
            return;
        }

        voidTimer = 0f;
    }

    private void TryActivateNearestCheckpoint(bool forceActivation)
    {
        if (checkpoints == null || checkpoints.Length == 0)
            return;

        int bestIndex = -1;
        float bestDistance = float.MaxValue;
        Vector3 currentPosition = transform.position;

        for (int i = 0; i < checkpoints.Length; i++)
        {
            RespawnCheckpoint checkpoint = checkpoints[i];
            float verticalDifference = Mathf.Abs(currentPosition.y - checkpoint.position.y);
            if (verticalDifference > checkpoint.heightTolerance)
                continue;

            Vector2 planarOffset = new Vector2(
                currentPosition.x - checkpoint.position.x,
                currentPosition.z - checkpoint.position.z
            );

            float planarDistance = planarOffset.magnitude;
            if (planarDistance > checkpoint.activationRadius)
                continue;

            if (!forceActivation && !IsCheckpointGrounded(currentPosition))
                continue;

            if (planarDistance < bestDistance)
            {
                bestDistance = planarDistance;
                bestIndex = i;
            }
        }

        if (bestIndex == -1 || bestIndex == activeCheckpointIndex)
            return;

        activeCheckpointIndex = bestIndex;
        activeCheckpointRotation = transform.rotation;
        UpdateCheckpointVisuals(forceRefresh: true);

        if (logCheckpointActivation)
        {
            Debug.Log(
                $"Checkpoint reached: {checkpoints[bestIndex].name}",
                this
            );
        }
    }

    private void RespawnAtActiveCheckpoint()
    {
        if (deathAudioSource != null && deathAudioSource.clip != null)
            deathAudioSource.PlayOneShot(deathAudioSource.clip);

        voidTimer = 0f;
        respawnCooldownTimer = respawnCooldown;

        Vector3 targetPosition = fallbackSpawnPosition;
        Quaternion targetRotation = fallbackSpawnRotation;

        if (activeCheckpointIndex >= 0 && activeCheckpointIndex < checkpoints.Length)
        {
            targetPosition = checkpoints[activeCheckpointIndex].position;
            targetRotation = activeCheckpointRotation;
        }

        targetPosition.y += respawnHeightOffset;

        if (playerController != null)
        {
            playerController.TeleportTo(
                targetPosition,
                targetRotation,
                resetLookPitchOnRespawn
            );
        }
        else
        {
            bool hadController = characterController != null;
            bool wasEnabled = hadController && characterController.enabled;
            if (hadController)
                characterController.enabled = false;

            transform.SetPositionAndRotation(targetPosition, targetRotation);

            if (hadController)
                characterController.enabled = wasEnabled;
        }

        previousPosition = transform.position;
    }

    private Vector3 GetGroundProbeOrigin()
    {
        if (characterController == null)
            return transform.position + Vector3.up * 0.5f;

        Bounds bounds = characterController.bounds;
        return new Vector3(
            bounds.center.x,
            bounds.max.y - 0.1f,
            bounds.center.z
        );
    }

    private bool IsCheckpointGrounded(Vector3 currentPosition)
    {
        if (characterController != null && characterController.isGrounded)
            return true;

        return Physics.Raycast(
            currentPosition + Vector3.up * 0.4f,
            Vector3.down,
            2.4f,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private RespawnCheckpoint[] BuildLevel45Defaults()
    {
        if (SceneManager.GetActiveScene().name != "Level45")
            return Array.Empty<RespawnCheckpoint>();

        return new[]
        {
            new RespawnCheckpoint
            {
                name = "Spawn Walkway",
                position = new Vector3(60.28f, 79f, 171.05f),
                activationRadius = 10f,
                heightTolerance = 8f
            },
            new RespawnCheckpoint
            {
                name = "Wreckage Span",
                position = new Vector3(37.191315f, 76.57667f, 197.18898f),
                activationRadius = 12f,
                heightTolerance = 8f
            },
            new RespawnCheckpoint
            {
                name = "Upper Building",
                position = new Vector3(22.637192f, 75.943924f, 268.43954f),
                activationRadius = 12f,
                heightTolerance = 8f
            },
            new RespawnCheckpoint
            {
                name = "Far Platform",
                position = new Vector3(95.25f, 69.07f, 255.19f),
                activationRadius = 11f,
                heightTolerance = 8f
            },
            new RespawnCheckpoint
            {
                name = "End Platform",
                position = new Vector3(143.59f, 70.52f, 252.03f),
                activationRadius = 11f,
                heightTolerance = 8f
            }
        };
    }

    private void RebuildCheckpointVisuals()
    {
        ClearCheckpointVisuals();

        if (!Application.isPlaying || !showCheckpointVisuals || checkpoints == null || checkpoints.Length == 0)
            return;

        GameObject rootObject = new GameObject("__CheckpointVisuals");
        checkpointVisualRoot = rootObject.transform;

        for (int i = 0; i < checkpoints.Length; i++)
            checkpointVisuals.Add(CreateCheckpointVisual(checkpoints[i], i));

        UpdateCheckpointVisuals(forceRefresh: true);
    }

    private CheckpointVisualInstance CreateCheckpointVisual(RespawnCheckpoint checkpoint, int index)
    {
        string sanitizedName = string.IsNullOrWhiteSpace(checkpoint.name)
            ? $"Checkpoint_{index + 1}"
            : checkpoint.name.Replace(' ', '_');

        GameObject rootObject = new GameObject($"Checkpoint_{sanitizedName}");
        Transform root = rootObject.transform;
        root.SetParent(checkpointVisualRoot, worldPositionStays: false);
        root.position = checkpoint.position + Vector3.up * visualHeightOffset;

        GameObject lightObject = new GameObject("AuraLight");
        lightObject.transform.SetParent(root, worldPositionStays: false);
        lightObject.transform.localPosition = Vector3.zero;
        Light pointLight = lightObject.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.range = checkpointLightRange;
        pointLight.intensity = checkpointLightIntensity;
        pointLight.color = checkpointLightColor;
        pointLight.shadows = LightShadows.None;

        GameObject coreObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coreObject.name = "BeaconCore";
        coreObject.transform.SetParent(root, worldPositionStays: false);
        coreObject.transform.localPosition = Vector3.zero;
        coreObject.transform.localScale = Vector3.one * 0.22f;
        Collider coreCollider = coreObject.GetComponent<Collider>();
        if (coreCollider != null)
            Destroy(coreCollider);

        Renderer coreRenderer = coreObject.GetComponent<Renderer>();
        Material coreMaterial = new Material(GetCheckpointCoreTemplate());
        coreRenderer.sharedMaterial = coreMaterial;
        coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        coreRenderer.receiveShadows = false;

        ParticleSystem stars = CreateStarsParticleSystem(root, "BlueStars");
        ParticleSystem motes = CreateMoteParticleSystem(root, "BlueMotes");

        return new CheckpointVisualInstance
        {
            root = root,
            pointLight = pointLight,
            core = coreObject.transform,
            coreMaterial = coreMaterial,
            stars = stars,
            motes = motes,
            phaseOffset = index * 0.73f
        };
    }

    private void UpdateCheckpointVisuals(bool forceRefresh = false)
    {
        if (!showCheckpointVisuals || checkpointVisuals.Count == 0)
            return;

        float time = Application.isPlaying ? Time.time : 0f;

        for (int i = 0; i < checkpointVisuals.Count; i++)
        {
            CheckpointVisualInstance visual = checkpointVisuals[i];
            if (visual == null || visual.root == null)
                continue;

            bool isActive = i == activeCheckpointIndex;
            float twinkle = 0.9f + 0.18f * Mathf.Sin(time * 2.1f + visual.phaseOffset);
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 1.4f + visual.phaseOffset * 1.5f);
            Color baseColor = isActive ? activeCheckpointLightColor : checkpointLightColor;

            if (visual.pointLight != null)
            {
                visual.pointLight.color = Color.Lerp(baseColor, Color.white, isActive ? 0.18f : 0.06f);
                visual.pointLight.intensity = checkpointLightIntensity * (isActive ? 1.55f : 0.82f) * twinkle;
                visual.pointLight.range = checkpointLightRange * (isActive ? 1.12f : 1f);
            }

            if (visual.core != null)
            {
                float scale = isActive
                    ? Mathf.Lerp(0.24f, 0.31f, pulse)
                    : Mathf.Lerp(0.17f, 0.22f, pulse * 0.45f);
                visual.core.localScale = Vector3.one * scale;
            }

            if (visual.coreMaterial != null)
            {
                Color emission = baseColor * (isActive ? 3f : 1.8f) * twinkle;
                if (visual.coreMaterial.HasProperty("_BaseColor"))
                    visual.coreMaterial.SetColor("_BaseColor", Color.Lerp(baseColor, Color.white, isActive ? 0.2f : 0.08f));
                if (visual.coreMaterial.HasProperty("_Color"))
                    visual.coreMaterial.SetColor("_Color", Color.Lerp(baseColor, Color.white, isActive ? 0.2f : 0.08f));
                if (visual.coreMaterial.HasProperty("_EmissionColor"))
                {
                    visual.coreMaterial.EnableKeyword("_EMISSION");
                    visual.coreMaterial.SetColor("_EmissionColor", emission);
                }
            }

            if (forceRefresh)
            {
                ApplyParticleTint(visual.stars, baseColor, isActive ? 1f : 0.82f);
                ApplyParticleTint(visual.motes, baseColor, isActive ? 1f : 0.75f);
            }
        }
    }

    private void ApplyParticleTint(ParticleSystem system, Color baseColor, float alphaMultiplier)
    {
        if (system == null)
            return;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(baseColor, Color.white, 0.18f), 0f),
                new GradientColorKey(baseColor, 0.45f),
                new GradientColorKey(Color.Lerp(baseColor, Color.white, 0.35f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f * alphaMultiplier, 0.15f),
                new GradientAlphaKey(0.82f * alphaMultiplier, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var main = system.main;
        main.startColor = new ParticleSystem.MinMaxGradient(gradient);
    }

    private ParticleSystem CreateStarsParticleSystem(Transform parent, string objectName)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.localPosition = Vector3.zero;

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.maxParticles = 70;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 18f;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.75f;

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.15f;

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.25f, 1f),
                new Keyframe(0.75f, 0.9f),
                new Keyframe(1f, 0f)
            )
        );

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetCheckpointParticleTemplate();

        system.Clear(withChildren: true);
        system.Play();
        return system;
    }

    private ParticleSystem CreateMoteParticleSystem(Transform parent, string objectName)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.localPosition = Vector3.down * 0.35f;

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 4.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.16f, 0.38f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.maxParticles = 36;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = -0.02f;

        var emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 8f;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.28f;
        shape.angle = 8f;

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.1f;

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(0.4f, 1f),
                new Keyframe(1f, 0f)
            )
        );

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetCheckpointParticleTemplate();

        system.Clear(withChildren: true);
        system.Play();
        return system;
    }

    private void ClearCheckpointVisuals()
    {
        for (int i = 0; i < checkpointVisuals.Count; i++)
        {
            CheckpointVisualInstance visual = checkpointVisuals[i];
            if (visual?.coreMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(visual.coreMaterial);
                else
                    DestroyImmediate(visual.coreMaterial);
            }
        }

        checkpointVisuals.Clear();

        if (checkpointVisualRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(checkpointVisualRoot.gameObject);
        else
            DestroyImmediate(checkpointVisualRoot.gameObject);

        checkpointVisualRoot = null;
    }

    private static Material GetCheckpointCoreTemplate()
    {
        if (checkpointCoreTemplate != null)
            return checkpointCoreTemplate;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit") ??
            Shader.Find("Standard") ??
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color");

        checkpointCoreTemplate = new Material(shader);
        checkpointCoreTemplate.name = "Runtime_CheckpointCore_Template";

        if (checkpointCoreTemplate.HasProperty("_BaseColor"))
            checkpointCoreTemplate.SetColor("_BaseColor", Color.white);
        if (checkpointCoreTemplate.HasProperty("_Color"))
            checkpointCoreTemplate.SetColor("_Color", Color.white);
        if (checkpointCoreTemplate.HasProperty("_Smoothness"))
            checkpointCoreTemplate.SetFloat("_Smoothness", 0.2f);
        if (checkpointCoreTemplate.HasProperty("_Metallic"))
            checkpointCoreTemplate.SetFloat("_Metallic", 0f);
        if (checkpointCoreTemplate.HasProperty("_EmissionColor"))
        {
            checkpointCoreTemplate.EnableKeyword("_EMISSION");
            checkpointCoreTemplate.SetColor("_EmissionColor", Color.white * 2f);
        }

        return checkpointCoreTemplate;
    }

    private static Material GetCheckpointParticleTemplate()
    {
        if (checkpointParticleTemplate != null)
            return checkpointParticleTemplate;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Particles/Standard Unlit") ??
            Shader.Find("Sprites/Default");

        checkpointParticleTemplate = new Material(shader);
        checkpointParticleTemplate.name = "Runtime_CheckpointParticles_Template";

        if (checkpointParticleTemplate.HasProperty("_BaseColor"))
            checkpointParticleTemplate.SetColor("_BaseColor", Color.white);
        if (checkpointParticleTemplate.HasProperty("_Color"))
            checkpointParticleTemplate.SetColor("_Color", Color.white);

        return checkpointParticleTemplate;
    }

    private void OnDrawGizmosSelected()
    {
        if (checkpoints == null)
            return;

        for (int i = 0; i < checkpoints.Length; i++)
        {
            RespawnCheckpoint checkpoint = checkpoints[i];
            Gizmos.color = i == activeCheckpointIndex
                ? new Color(0.2f, 1f, 0.4f, 1f)
                : new Color(0.25f, 0.75f, 1f, 0.9f);

            Vector3 center = checkpoint.position;
            Gizmos.DrawWireSphere(center, checkpoint.activationRadius);
            Gizmos.DrawLine(
                center + Vector3.up * checkpoint.heightTolerance,
                center - Vector3.up * checkpoint.heightTolerance
            );
        }

        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.75f);
        Gizmos.DrawLine(
            new Vector3(transform.position.x - 3f, hardRespawnY, transform.position.z),
            new Vector3(transform.position.x + 3f, hardRespawnY, transform.position.z)
        );
    }
}
