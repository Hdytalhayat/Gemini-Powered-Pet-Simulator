// --- START OF FILE NPCController.cs ---

using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// Enum untuk Status / State NPC agar lebih terorganisir
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
    private SpriteRenderer spriteRenderer; // --- TAMBAHAN --- Untuk membalik sprite

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>(); // --- TAMBAHAN ---
    }

    public void Initialize()
    {
        this.moveSpeed = 2.0f - (laziness / 10f);
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
        if (currentState == NPCState.Interacting) return; // --- MODIFIKASI --- Jika berinteraksi, jangan lakukan update needs.

        hunger -= (0.5f + laziness / 20f) * Time.deltaTime;
        thirst -= 1.0f * Time.deltaTime;
        energy -= (0.8f + laziness / 15f) * Time.deltaTime;
        hygiene -= (0.6f + (10 - cleanliness) / 20f) * Time.deltaTime;

        if (hunger < 20 || thirst < 20 || energy < 20 || hygiene < 20)
        {
            happiness -= 3f * Time.deltaTime;
        }

        hunger = Mathf.Max(0, hunger);
        thirst = Mathf.Max(0, thirst);
        energy = Mathf.Max(0, energy);
        happiness = Mathf.Max(0, happiness);
        hygiene = Mathf.Max(0, hygiene);

        if (hunger <= 0 || thirst <= 0 || energy <= 0)
        {
            Debug.LogWarning($"{npcName} has died!");
            FindObjectOfType<GameManager>().OnNPCDeath();
            Destroy(gameObject);
            return;
        }

        timeInCurrentState += Time.deltaTime;
        if (timeInCurrentState > Random.Range(5f, 10f))
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

        if (hunger < 30) { SetState(NPCState.SeekingFood, $"{npcName} merasa lapar."); }
        else if (thirst < 40) { SetState(NPCState.SeekingWater, $"{npcName} merasa haus."); }
        else if (energy < 50) { SetState(NPCState.SeekingRest, $"{npcName} merasa lelah."); }
        else if (hygiene < 40) { SetState(NPCState.SeekingBath, $"{npcName} merasa kotor."); }
        else
        {
            float diceRoll = Random.value;
            float interactionChance = (socialness / 10f) * 0.5f;
            float playChance = (1.0f - (laziness / 10f)) * 0.3f;

            // --- MODIFIKASI LOGIKA INTERAKSI ---
            if (nearbyNPCs.Count > 0 && diceRoll < interactionChance)
            {
                // Coba cari target yang mau diajak interaksi
                AttemptToStartInteraction();
            }
            else if (happiness > 60 && diceRoll < interactionChance + playChance)
            {
                StartPlaying();
            }
            else
            {
                StartWandering();
            }
        }
    }

    // --- FUNGSI BARU ---
    void AttemptToStartInteraction()
    {
        // Cari target yang 'tersedia' (tidak sibuk)
        List<NPCController> availableNpcs = nearbyNPCs.FindAll(npc => 
            npc.currentState != NPCState.Interacting && 
            npc.currentState != NPCState.SeekingFood &&
            npc.currentState != NPCState.SeekingWater &&
            npc.currentState != NPCState.SeekingRest &&
            npc.currentState != NPCState.SeekingBath
        );

        if (availableNpcs.Count > 0)
        {
            NPCController target = availableNpcs[Random.Range(0, availableNpcs.Count)];
            StartInteraction(target);
        }
        else
        {
            // Tidak ada yang bisa diajak interaksi, wandering saja
            StartWandering();
        }
    }

    void ExecuteCurrentStateAction()
    {
        // --- MODIFIKASI --- Logika pergerakan dan orientasi
        Vector2 directionToTarget = Vector2.zero;
        if (currentState == NPCState.Wandering || currentState == NPCState.SeekingFood || 
            currentState == NPCState.SeekingWater || currentState == NPCState.SeekingRest ||
            currentState == NPCState.SeekingBath)
        {
            directionToTarget = (targetPosition - (Vector2)transform.position).normalized;
            rb.MovePosition(rb.position + directionToTarget * moveSpeed * Time.fixedDeltaTime);

            if (Vector2.Distance(transform.position, targetPosition) < 0.2f)
            {
                HandleArrival();
            }
        }
        
        // --- TAMBAHAN --- Logika membalikkan sprite agar menghadap arah gerakan
        if (spriteRenderer != null && directionToTarget.x != 0)
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
            case NPCState.Wandering: SetState(NPCState.Idling, $"{npcName} berhenti sejenak."); break;
            case NPCState.SeekingFood:
                hunger = 100f;
                happiness = Mathf.Min(100, happiness + 15f);
                Debug.Log($"{npcName} telah makan.");
                SetState(NPCState.Idling, null);
                break;
            case NPCState.SeekingWater:
                thirst = 100f;
                happiness = Mathf.Min(100, happiness + 10f);
                Debug.Log($"{npcName} telah minum.");
                SetState(NPCState.Idling, null);
                break;
            case NPCState.SeekingRest:
                energy = 100f;
                happiness = Mathf.Min(100, happiness + 20f);
                Debug.Log($"{npcName} telah istirahat.");
                SetState(NPCState.Idling, null);
                break;
            case NPCState.SeekingBath:
                hygiene = 100f;
                happiness = Mathf.Min(100, happiness + 15f);
                Debug.Log($"{npcName} telah mandi.");
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
            Debug.LogWarning($"Tidak ada objek dengan tag '{tag}' ditemukan. {npcName} akan wandering.");
            StartWandering();
        }
    }
    
    void StartWandering()
    {
        SetState(NPCState.Wandering, $"{npcName} berjalan-jalan.");
        targetPosition = new Vector2(Random.Range(-10, 10), Random.Range(-10, 10)); 
    }

    void StartPlaying()
    {
        SetState(NPCState.Playing, $"{npcName} sedang bermain dengan gembira!");
    }
    
    public void SetPersonality(string name, float social, float lazy, float clean)
    {
        this.npcName = name;
        this.socialness = social;
        this.laziness = lazy;
        this.cleanliness = clean;
    }
    
    // --- MODIFIKASI BESAR ---
    void StartInteraction(NPCController other)
    {
        // 1. Set state kedua NPC menjadi Interacting
        SetState(NPCState.Interacting, $"{npcName} memulai interaksi dengan {other.npcName}");
        other.SetState(NPCState.Interacting, $"{other.npcName} diajak berinteraksi oleh {npcName}");

        // 2. Hentikan gerakan
        rb.velocity = Vector2.zero; 
        other.rb.velocity = Vector2.zero;

        // 3. Buat mereka saling berhadapan
        FaceEachOther(other);

        // 4. Minta GameManager untuk memproses interaksi
        FindObjectOfType<GameManager>().RequestInteraction(this, other);
    }
    
    // --- FUNGSI BARU --- Untuk membuat NPC saling berhadapan
    void FaceEachOther(NPCController other)
    {
        if (spriteRenderer == null || other.spriteRenderer == null) return;

        bool otherIsToTheRight = other.transform.position.x > transform.position.x;
        spriteRenderer.flipX = !otherIsToTheRight; // Menghadap ke kanan jika target di kanan
        other.spriteRenderer.flipX = otherIsToTheRight; // Menghadap ke kiri jika target di kiri
    }

    public void ReceiveInteractionResult(bool wasPositive)
    {
        if (currentState != NPCState.Interacting) return; // Keamanan, jangan proses jika tidak sedang berinteraksi

        if(wasPositive)
        {
            Debug.Log($"{npcName} senang dengan interaksinya.");
            happiness = Mathf.Min(100, happiness + 20f);
        }
        else
        {
            Debug.Log($"{npcName} tidak suka dengan interaksinya.");
            happiness = Mathf.Max(0, happiness - 10f);
        }
        // Setelah interaksi, kembali ke state normal setelah jeda singkat
        StartCoroutine(EndInteractionRoutine());
    }

    // --- FUNGSI BARU --- Coroutine untuk mengakhiri interaksi dengan jeda
    private IEnumerator EndInteractionRoutine()
    {
        yield return new WaitForSeconds(1.5f); // Beri waktu pemain untuk melihat hasilnya
        SetState(NPCState.Idling, $"{npcName} selesai berinteraksi.");
    }
    
    private Transform FindClosestObjectWithTag(string tag)
    {
        GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
        if (objects.Length == 0) return null;

        Transform closest = null;
        float minDistance = Mathf.Infinity;
        Vector3 currentPosition = transform.position;
        foreach (GameObject obj in objects)
        {
            float distance = Vector3.Distance(obj.transform.position, currentPosition);
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