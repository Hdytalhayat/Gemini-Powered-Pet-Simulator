// --- START OF FILE UnityAndGeminiV3.cs ---

using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System;

// Struktur JSON untuk parsing response dari Gemini
[System.Serializable]
public class Response
{
    public Candidate[] candidates;
    public PromptFeedback promptFeedback; // Untuk menangani jika prompt diblokir
}

[System.Serializable]
public class PromptFeedback
{
    public SafetyRating[] safetyRatings;
}

[System.Serializable]
public class SafetyRating
{
    public string category;
    public string probability;
}

// Struktur JSON untuk membuat body request ke Gemini
[System.Serializable]
public class ChatRequest
{
    public Content[] contents;
    public GenerationConfig generationConfig;
    public SafetySetting[] safetySettings; // Menambahkan safety settings
}

[System.Serializable]
public class GenerationConfig
{
    public int candidateCount = 1;
    public float temperature = 0.9f;
    // Anda bisa menambahkan parameter lain seperti topK, topP, dll.
}

[System.Serializable]
public class SafetySetting
{
    public string category;
    public string threshold;
}

[System.Serializable]
public class Candidate
{
    public Content content;
}

[System.Serializable]
public class Content
{
    public string role;
    public Part[] parts;
}

[System.Serializable]
public class Part
{
    public string text;
}

// Helper class untuk membaca API Key dari file JSON
[System.Serializable]
public class UnityAndGeminiKey
{
    public string key;
}


public class UnityAndGeminiV3 : MonoBehaviour
{
    // --- MODIFIKASI --- Event ini sekarang mengirimkan string response dan boolean status sukses
    public Action<string, bool> OnGeminiResponse;
    
    [Header("API Key Configuration")]
    [Tooltip("Assign the 'GeminiKey.json' file here. This file should contain your private API key.")]
    public TextAsset geminiKeyJsonFile;
    private string apiKey = "";
    
    // --- MODIFIKASI --- Menggunakan model Flash terbaru yang lebih cepat dan efisien
    private string apiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent";

    private void Start()
    {
        if (geminiKeyJsonFile == null)
        {
            Debug.LogError("File kunci API 'GeminiKey.json' belum di-assign di Inspector pada script UnityAndGeminiV3!");
            Debug.LogWarning("Pastikan Anda sudah mengganti nama 'GeminiKey.template.json' menjadi 'GeminiKey.json' dan mengisinya dengan API Key Anda, lalu assign ke Inspector.");
            return;
        }
        try
        {
            UnityAndGeminiKey jsonApiKey = JsonUtility.FromJson<UnityAndGeminiKey>(geminiKeyJsonFile.text);
            apiKey = jsonApiKey.key;

            if (apiKey == "GANTI_DENGAN_API_KEY_GEMINI_ANDA" || string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("API Key di dalam 'GeminiKey.json' belum diisi. Silakan edit file tersebut.");
                apiKey = ""; // Kosongkan agar tidak mencoba request
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Gagal memuat API Key dari JSON: {e.Message}");
        }
    }

    /// <summary>
    /// Fungsi utama untuk mengirim request ke Gemini.
    /// </summary>
    /// <param name="prompt">Prompt utama dari user.</param>
    /// <param name="history">History percakapan sebelumnya (opsional).</param>
    /// <param name="systemInstruction">Instruksi sistem (opsional).</param>
    public void RequestGemini(string prompt, List<Content> history = null, string systemInstruction = null)
    {
        StartCoroutine(SendRequestToGemini(prompt, history, systemInstruction));
    }

    private IEnumerator SendRequestToGemini(string prompt, List<Content> history, string systemInstruction)
    {
        string url = $"{apiEndpoint}?key={apiKey}";

        // --- PEMBUATAN REQUEST BODY ---
        List<Content> contentsList = new List<Content>();

        // 1. Tambahkan System Instruction jika ada (disarankan)
        if (!string.IsNullOrEmpty(systemInstruction))
        {
            // Pola: User memberikan instruksi, Model mengkonfirmasi
            contentsList.Add(CreateUserContent(systemInstruction));
            contentsList.Add(CreateModelContent("Understood. I will follow the instructions."));
        }

        // 2. Tambahkan history percakapan jika ada
        if (history != null)
        {
            contentsList.AddRange(history);
        }

        // 3. Tambahkan prompt baru dari user
        contentsList.Add(CreateUserContent(prompt));

        ChatRequest requestBody = new ChatRequest
        {
            contents = contentsList.ToArray(),
            generationConfig = new GenerationConfig { temperature = 1.0f }, // Buat lebih kreatif
            // Set safety settings agar tidak terlalu ketat untuk game
            safetySettings = new SafetySetting[] {
                new SafetySetting { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                new SafetySetting { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                new SafetySetting { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                new SafetySetting { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
            }
        };

        string jsonData = JsonUtility.ToJson(requestBody);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

        // --- PENGIRIMAN REQUEST ---
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"API Request Error: {www.error}");
                Debug.LogError($"Error Response: {www.downloadHandler.text}");
                OnGeminiResponse?.Invoke(www.downloadHandler.text, false); // Kirim event gagal
            }
            else
            {
                // Debug.Log("API Request complete! Raw Response: " + www.downloadHandler.text);
                Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);

                // Cek jika response diblokir atau tidak ada kandidat
                if (response.candidates == null || response.candidates.Length == 0)
                {
                    string reason = "Response was empty or blocked.";
                    if(response.promptFeedback != null && response.promptFeedback.safetyRatings != null && response.promptFeedback.safetyRatings.Length > 0)
                    {
                        reason += $" Block reason: {response.promptFeedback.safetyRatings[0].category}";
                    }
                    Debug.LogWarning(reason + "\nFull response: " + www.downloadHandler.text);
                    OnGeminiResponse?.Invoke(reason, false); // Kirim event gagal
                }
                else
                {
                    string text = response.candidates[0].content.parts[0].text;
                    // Debug.Log($"Gemini Response: {text}");
                    OnGeminiResponse?.Invoke(text, true); // Kirim event sukses dengan response text
                }
            }
        }
    }
    
    // Helper functions untuk membuat Content lebih mudah
    private Content CreateUserContent(string text)
    {
        return new Content { role = "user", parts = new Part[] { new Part { text = text } } };
    }

    private Content CreateModelContent(string text)
    {
        return new Content { role = "model", parts = new Part[] { new Part { text = text } } };
    }
}