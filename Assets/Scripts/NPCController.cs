// --- FILE: NPCController.cs ---

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum NPCState { Idling, Wandering, SeekingFood, SeekingWater, SeekingRest, SeekingBath, Interacting, Playing }

public class NPCController : MonoBehaviour
{
    [Header("Identity & Personality")]
    public string npcName = "Bobi";
    [Range(1, 10)] public float socialness = 5f;
    [Range(1, 10)] public float laziness = 5f;
    [Range(1, 10)] public float cleanliness = 5f;

    [Header("Core Attributes (Needs)")]
    [Range(0, 100)] public float hunger = 100f;
    [Range(0, 100)] public float thirst = 100f;
    [Range(0, 100)] public float energy = 100f;
    [Range(0, 100)] public float happiness = 70f;
    [Range(0, 100)] public float hygiene = 100f;

    [Header("State & Action")]
    public NPCState currentState = NPCState.Idling;
    private float timeInCurrentState = 0f;
    private Vector2 targetPosition;
    private float moveSpeed = 1.5f;

    public UnityAndGeminiV3 geminiAPI;
    private List<NPCController> nearbyNPCs = new List<NPCController>();
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize()
    {
        this.moveSpeed = 2.0f - (laziness / 10f); // Lazier NPCs move slower
        StartWandering();
        gameObject.name = $"NPC - {this.npcName}";
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("NPC"))
        {
            NPCController otherNpc = other.GetComponent<NPCController>();
            if (otherNpc != null && !nearbyNPCs.Contains(otherNpc))
            {
                nearbyNPCs.Add(otherNpc);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("NPC"))
        {
            NPCController otherNpc = other.GetComponent<NPCController>();
            if (otherNpc != null)
            {
                nearbyNPCs.Remove(otherNpc);
            }
        }
    }

    void Update()
    {
        if (currentState == NPCState.Interacting) return; // Pause needs decay during interaction

        // Needs decay over time
        hunger -= (0.5f + laziness / 20f) * Time.deltaTime;
        thirst -= 1.0f * Time.deltaTime;
        energy -= (0.8f + laziness / 15f) * Time.deltaTime;
        hygiene -= (0.6f + (10 - cleanliness) / 20f) * Time.deltaTime; // Less clean NPCs get dirty faster

        // Low needs impact happiness
        if (hunger < 20 || thirst < 20 || energy < 20 || hygiene < 20)
        {
            happiness -= 3f * Time.deltaTime;
        }

        // Clamp values
        hunger = Mathf.Max(0, hunger);
        thirst = Mathf.Max(0, thirst);
        energy = Mathf.Max(0, energy);
        happiness = Mathf.Max(0, happiness);
        hygiene = Mathf.Max(0, hygiene);

        // Check for death condition
        if (hunger <= 0 || thirst <= 0 || energy <= 0)
        {
            Debug.LogWarning($"{npcName} has died!");
            FindObjectOfType<GameManager>().OnNPCDeath();
            Destroy(gameObject);
            return;
        }

        // Re-evaluate action periodically
        timeInCurrentState += Time.deltaTime;
        if (timeInCurrentState > Random.Range(5f, 8f))
        {
            DecideNextAction();
        }
    }

    void FixedUpdate()
    {
        ExecuteCurrentStateAction();
    }

    void DecideNextAction()
    {
        timeInCurrentState = 0f;
        
        // --- Utility AI: Address the most critical need first ---
        if (hunger < 25) { SetState(NPCState.SeekingFood, $"{npcName} is very hungry and is seeking food."); }
        else if (thirst < 30) { SetState(NPCState.SeekingWater, $"{npcName} is very thirsty and is seeking water."); }
        else if (energy < 20) { SetState(NPCState.SeekingRest, $"{npcName} is very tired and is seeking a resting spot."); }
        else if (hygiene < 35) { SetState(NPCState.SeekingBath, $"{npcName} feels dirty and is seeking a bath."); }
        else
        {
            // --- If all needs are met, decide on a leisure activity ---
            Debug.Log($"{npcName} is content. Deciding on a leisure activity... (Nearby NPCs: {nearbyNPCs.Count})");
            
            float diceRoll = Random.value;
            float interactionChance = (socialness / 10f) * 0.6f;
            float playChance = (1.0f - (laziness / 10f)) * 0.4f;

            if (nearbyNPCs.Count > 0 && diceRoll < interactionChance)
            {
                Debug.Log($"{npcName} decided to attempt an interaction. (Roll: {diceRoll:F2} < Chance: {interactionChance:F2})");
                AttemptToStartInteraction();
            }
            else if (happiness > 60 && diceRoll < interactionChance + playChance)
            {
                Debug.Log($"{npcName} decided to play.");
                StartPlaying();
            }
            else
            {
                Debug.Log($"{npcName} decided to wander around.");
                StartWandering();
            }
        }
    }

    void AttemptToStartInteraction()
    {
        List<NPCController> availableNpcs = nearbyNPCs.FindAll(npc => 
            npc.currentState != NPCState.Interacting && 
            npc.currentState != NPCState.SeekingFood &&
            npc.currentState != NPCState.SeekingWater &&
            npc.currentState != NPCState.SeekingRest &&
            npc.currentState != NPCState.SeekingBath
        );

        if (availableNpcs.Count > 0)
        {
            Debug.Log($"{npcName} found {availableNpcs.Count} available NPC(s) to interact with.");
            NPCController target = availableNpcs[Random.Range(0, availableNpcs.Count)];
            StartInteraction(target);
        }
        else
        {
            // No available targets, just wander for now
            StartWandering();
        }
    }

    void ExecuteCurrentStateAction()
    {
        Vector2 directionToTarget = (targetPosition - (Vector2)transform.position);

        if (currentState == NPCState.Wandering || currentState == NPCState.SeekingFood || 
            currentState == NPCState.SeekingWater || currentState == NPCState.SeekingRest ||
            currentState == NPCState.SeekingBath)
        {
            rb.MovePosition(rb.position + directionToTarget.normalized * moveSpeed * Time.fixedDeltaTime);
            if (directionToTarget.magnitude < 0.2f)
            {
                HandleArrival();
            }
        }
        
        // Flip sprite based on movement direction
        if (spriteRenderer != null && Mathf.Abs(directionToTarget.x) > 0.01f)
        {
            spriteRenderer.flipX = directionToTarget.x < 0;
        }

        if (currentState == NPCState.Playing)
        {
            if (timeInCurrentState > 5f)
            {
                happiness = Mathf.Min(100, happiness + 10f);
                StartWandering();
            }
        }
        else if (currentState == NPCState.Idling)
        {
             if (timeInCurrentState > Random.Range(2f, 4f))
             {
                 DecideNextAction();
             }
        }
    }

    void HandleArrival()
    {
        switch (currentState)
        {
            case NPCState.Wandering: SetState(NPCState.Idling, $"{npcName} is taking a short break."); break;
            case NPCState.SeekingFood:
                hunger = 100f; happiness = Mathf.Min(100, happiness + 15f);
                Debug.Log($"{npcName} has eaten.");
                SetState(NPCState.Idling, null);
                break;
            case NPCState.SeekingWater:
                thirst = 100f; happiness = Mathf.Min(100, happiness + 10f);
                Debug.Log($"{npcName} has drunk.");
                SetState(NPCState.Idling, null);
                break;
            case NPCState.SeekingRest:
                energy = 100f; happiness = Mathf.Min(100, happiness + 20f);
                Debug.Log($"{npcName} has rested.");
                SetState(NPCState.Idling, null);
                break;
            case NPCState.SeekingBath:
                hygiene = 100f; happiness = Mathf.Min(100, happiness + 15f);
                Debug.Log($"{npcName} has taken a bath.");
                SetState(NPCState.Idling, null);
                break;
        }
    }

    void FindAndSetTarget(string tag)
    {
        Transform targetObject = FindClosestObjectWithTag(tag);
        if (targetObject != null)
        {
            targetPosition = targetObject.position;
        }
        else
        {
            Debug.LogWarning($"No object with tag '{tag}' found. {npcName} will wander instead.");
            StartWandering();
        }
    }
    
    void StartWandering()
    {
        SetState(NPCState.Wandering, null);
        targetPosition = new Vector2(Random.Range(-10, 10), Random.Range(-10, 10)); 
    }

    void StartPlaying()
    {
        SetState(NPCState.Playing, $"{npcName} is playing happily!");
    }
    
    public void SetPersonality(string name, float social, float lazy, float clean)
    {
        this.npcName = name;
        this.socialness = social;
        this.laziness = lazy;
        this.cleanliness = clean;
    }
    
    void StartInteraction(NPCController other)
    {
        SetState(NPCState.Interacting, $"{npcName} is initiating an interaction with {other.npcName}");
        other.SetState(NPCState.Interacting, $"{other.npcName} is being interacted with by {npcName}");

        rb.velocity = Vector2.zero; 
        other.rb.velocity = Vector2.zero;

        FaceEachOther(other);
        
        FindObjectOfType<GameManager>().RequestInteraction(this, other);
    }
    
    void FaceEachOther(NPCController other)
    {
        if (spriteRenderer == null || other.spriteRenderer == null) return;
        bool otherIsToTheRight = other.transform.position.x > transform.position.x;
        spriteRenderer.flipX = !otherIsToTheRight;
        other.spriteRenderer.flipX = otherIsToTheRight;
    }

    public void ReceiveInteractionResult(bool wasPositive)
    {
        if (currentState != NPCState.Interacting) return;
        
        if(wasPositive)
        {
            Debug.Log($"{npcName} is happy with the interaction.");
            happiness = Mathf.Min(100, happiness + 20f);
        }
        else
        {
            Debug.Log($"{npcName} disliked the interaction.");
            happiness = Mathf.Max(0, happiness - 10f);
        }
        StartCoroutine(EndInteractionRoutine());
    }
    
    private IEnumerator EndInteractionRoutine()
    {
        yield return new WaitForSeconds(1.5f); // Let player see the result
        SetState(NPCState.Idling, $"{npcName} has finished interacting.");
    }
    
    private Transform FindClosestObjectWithTag(string tag)
    {
        GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
        if (objects.Length == 0) return null;

        Transform closest = null;
        float minDistance = Mathf.Infinity;
        foreach (GameObject obj in objects)
        {
            float distance = Vector3.Distance(obj.transform.position, transform.position);
            if (distance < minDistance)
            {
                closest = obj.transform;
                minDistance = distance;
            }
        }
        return closest;
    }

    public void SetState(NPCState newState, string logMessage)
    {
        currentState = newState;
        timeInCurrentState = 0f;
        if(!string.IsNullOrEmpty(logMessage))
        {
            Debug.Log(logMessage);
        }

        switch(newState)
        {
            case NPCState.SeekingFood: FindAndSetTarget("FoodSource"); break;
            case NPCState.SeekingWater: FindAndSetTarget("WaterSource"); break;
            case NPCState.SeekingRest: FindAndSetTarget("RestingSpot"); break;
            case NPCState.SeekingBath: FindAndSetTarget("BathSpot"); break;
        }
    }
}