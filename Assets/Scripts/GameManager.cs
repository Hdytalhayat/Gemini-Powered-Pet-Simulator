// --- START OF FILE GameManager.cs ---

using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// Class helper untuk parsing JSON dari Gemini
[System.Serializable]
public class NPCPersonality
{
    public string name;
    public float socialness;
    public float laziness;
    public float cleanliness;
}

public class GameManager : MonoBehaviour
{
    [Header("API Settings")]
    [Tooltip("Jika dicentang, game akan menggunakan data acak lokal dan tidak akan memanggil Gemini API. Berguna untuk development agar tidak menghabiskan kuota.")]
    public bool useOfflineDevelopmentMode = true;

    public enum GeminiRequestType { Personality, Interaction }

    [Header("Game Configuration")]
    public UnityAndGeminiV3 geminiAPI;
    public GameObject npcPrefab;
    public int numberOfNPCsToCreate = 3;

    private List<NPCController> activeNpcs = new List<NPCController>();
    private Queue<NPCController> npcPersonalityQueue = new Queue<NPCController>();
    private GeminiRequestType lastRequestType;
    
    private NPCController _interactingInitiator;
    private NPCController _interactingOther;

    private List<string> offlineNames = new List<string> { "Piko", "Mochi", "Boba", "Kiki", "Dodo", "Fifi", "Gigi", "Hobi", "Nino", "Popo" };
    
    void Start()
    {
        if (geminiAPI == null || npcPrefab == null)
        {
            Debug.LogError("Gemini API atau NPC Prefab belum di-assign di Inspector!");
            return;
        }

        if (!useOfflineDevelopmentMode)
        {
            geminiAPI.OnGeminiResponse += HandleGeminiResponse;
        }
        else
        {
            Debug.LogWarning("GAME BERJALAN DALAM MODE OFFLINE. Tidak ada panggilan API yang akan dilakukan.");
        }


        for (int i = 0; i < numberOfNPCsToCreate; i++)
        {
            GameObject newNPCObj = Instantiate(npcPrefab, new Vector2(Random.Range(-5, 5), Random.Range(-5, 5)), Quaternion.identity);
            NPCController npcController = newNPCObj.GetComponent<NPCController>();
            npcController.geminiAPI = this.geminiAPI; 
            npcPersonalityQueue.Enqueue(npcController);
            activeNpcs.Add(npcController);
        }

        RequestNextNPCPersonality();
    }
    
    private void HandleGeminiResponse(string response, bool success)
    {
        if (!success) {
            Debug.LogError("Gemini request gagal.");
            if(lastRequestType == GeminiRequestType.Personality) AssignDefaultPersonalityAndContinue();
            if(lastRequestType == GeminiRequestType.Interaction) EndFailedInteraction();
            return;
        }

        switch(lastRequestType)
        {
            case GeminiRequestType.Personality:
                HandleGeminiPersonalityResponse(response);
                break;
            case GeminiRequestType.Interaction:
                HandleGeminiInteractionResponse(response);
                break;
        }
    }
    
    void EndFailedInteraction()
    {
        Debug.LogWarning("Interaksi gagal karena API Error. NPC kembali beraktivitas.");
        if (_interactingInitiator != null) _interactingInitiator.ReceiveInteractionResult(false);
        if (_interactingOther != null) _interactingOther.ReceiveInteractionResult(false);
        _interactingInitiator = null;
        _interactingOther = null;
    }

    void RequestNextNPCPersonality()
    {
        if (npcPersonalityQueue.Count > 0)
        {
            if (useOfflineDevelopmentMode)
            {
                HandleOfflinePersonalityGeneration();
            }
            else
            {
                lastRequestType = GeminiRequestType.Personality;
                Debug.Log("Meminta kepribadian baru dari Gemini...");
                string systemPrompt = "You are a game designer creating unique personalities for cute animal NPCs. Respond ONLY with a valid JSON object and nothing else, not even the word 'json' or backticks. The JSON must contain: a 'name' (a single cute animal name), 'socialness' (a number from 1 to 10), 'laziness' (a number from 1 to 10), and 'cleanliness' (a number from 1 to 10).";
                string prompt = "Generate a new, unique NPC personality.";
                geminiAPI.RequestGemini(prompt, null, systemPrompt);
            }
        }
        else
        {
            Debug.Log("Semua NPC sudah memiliki kepribadian!");
        }
    }

    void HandleOfflinePersonalityGeneration()
    {
        if (npcPersonalityQueue.Count > 0)
        {
            NPCController targetNpc = npcPersonalityQueue.Dequeue();
            string randomName = $"Pet#{Random.Range(100,999)}";
            if(offlineNames.Count > 0)
            {
                int index = Random.Range(0, offlineNames.Count);
                randomName = offlineNames[index];
                offlineNames.RemoveAt(index);
            }

            targetNpc.SetPersonality(randomName, Random.Range(1, 11), Random.Range(1, 11), Random.Range(1, 11));
            targetNpc.Initialize();
            
            StartCoroutine(DelayedRequestNextPersonality(0.5f));
        }
    }

    void HandleGeminiPersonalityResponse(string jsonResponse)
    {
        if (npcPersonalityQueue.Count > 0)
        {
            NPCController targetNpc = npcPersonalityQueue.Peek();
            try
            {
                string cleanedJson = jsonResponse.Replace("```json", "").Replace("```", "").Trim();
                NPCPersonality personality = JsonUtility.FromJson<NPCPersonality>(cleanedJson);
                
                npcPersonalityQueue.Dequeue();
                targetNpc.SetPersonality(personality.name, personality.socialness, personality.laziness, personality.cleanliness);
                targetNpc.Initialize();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Gagal parsing JSON: {e.Message}. Response mentah: {jsonResponse}");
                AssignDefaultPersonalityAndContinue(true); 
                return;
            }
            
            StartCoroutine(DelayedRequestNextPersonality(2.0f));
        }
    }

    private IEnumerator DelayedRequestNextPersonality(float delay)
    {
        yield return new WaitForSeconds(delay);
        RequestNextNPCPersonality();
    }
    
    void AssignDefaultPersonalityAndContinue(bool reQueue = false)
    {
        if (npcPersonalityQueue.Count > 0)
        {
            NPCController targetNpc = npcPersonalityQueue.Dequeue();
            if (reQueue) {
                npcPersonalityQueue.Enqueue(targetNpc);
                StartCoroutine(DelayedRequestNextPersonality(5.0f));
            } else {
                targetNpc.SetPersonality("Robusto", Random.Range(1,11), Random.Range(1,11), Random.Range(1,11));
                targetNpc.Initialize();
                StartCoroutine(DelayedRequestNextPersonality(2.0f));
            }
        }
    }

    public void OnNPCDeath()
    {
        activeNpcs.RemoveAll(npc => npc == null);
        if (activeNpcs.Count == 0)
        {
            Debug.LogError("GAME OVER! Semua NPC telah mati.");
            Time.timeScale = 0;
        }
    }

    public void RequestInteraction(NPCController initiator, NPCController other)
    {
        _interactingInitiator = initiator;
        _interactingOther = other;

        if (useOfflineDevelopmentMode)
        {
            HandleOfflineInteraction();
        }
        else
        {
            lastRequestType = GeminiRequestType.Interaction;
            Debug.Log($"GameManager meminta interaksi antara {initiator.npcName} dan {other.npcName}");
            
            string systemPrompt = "You are a story generator. Describe a one-sentence interaction between two NPCs. Then, on a new line, write 'OUTCOME: POSITIVE' if it was a good interaction, 'OUTCOME: NEGATIVE' if it was bad, or 'OUTCOME: NEUTRAL' if it was neither. Respond with only these two lines.";
            string prompt = $"NPC 1, named {initiator.npcName} (social: {initiator.socialness}/10), interacts with NPC 2, named {other.npcName} (social: {other.socialness}/10). Generate their interaction and its outcome.";

            geminiAPI.RequestGemini(prompt, null, systemPrompt);
        }
    }
    
    // --- MODIFIKASI LOGIKA OFFLINE ---
    void HandleOfflineInteraction()
    {
        Debug.Log($"Mensimulasikan interaksi offline antara {_interactingInitiator.npcName} dan {_interactingOther.npcName}");

        // Logika lebih cerdas:
        // 1. Ambil rata-rata socialness kedua NPC.
        // 2. Semakin tinggi rata-ratanya, semakin besar kemungkinan interaksi positif.
        float avgSocialness = (_interactingInitiator.socialness + _interactingOther.socialness) / 2.0f;
        // Konversi socialness (1-10) menjadi probabilitas (0.1 - 1.0)
        float positiveChance = avgSocialness / 10.0f; 

        bool wasPositive = Random.value < positiveChance;

        if (_interactingInitiator != null) _interactingInitiator.ReceiveInteractionResult(wasPositive);
        if (_interactingOther != null) _interactingOther.ReceiveInteractionResult(wasPositive);

        _interactingInitiator = null;
        _interactingOther = null;
    }

    void HandleGeminiInteractionResponse(string interactionResult)
    {
        Debug.Log("Hasil Interaksi dari Gemini:\n" + interactionResult);
        
        bool wasPositive = false;
        
        if (interactionResult.Contains("OUTCOME: POSITIVE")) wasPositive = true;
        else if (interactionResult.Contains("OUTCOME: NEGATIVE")) wasPositive = false;

        if (_interactingInitiator != null) _interactingInitiator.ReceiveInteractionResult(wasPositive);
        if (_interactingOther != null) _interactingOther.ReceiveInteractionResult(wasPositive);

        _interactingInitiator = null;
        _interactingOther = null;
    }
}