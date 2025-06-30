// --- FILE: UnityAndGeminiV3.cs ---

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// JSON structures for Gemini API communication

[System.Serializable]
public class Response
{
    public Candidate[] candidates;
    public PromptFeedback promptFeedback;
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

[System.Serializable]
public class ChatRequest
{
    public Content[] contents;
    public GenerationConfig generationConfig;
    public SafetySetting[] safetySettings;
}

[System.Serializable]
public class GenerationConfig
{
    public int candidateCount = 1;
    public float temperature = 1.0f;
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

[System.Serializable]
public class UnityAndGeminiKey
{
    public string key;
}

public class UnityAndGeminiV3 : MonoBehaviour
{
    public Action<string, bool> OnGeminiResponse;
    
    [Header("API Key Configuration")]
    [Tooltip("Assign the 'GeminiKey.json' file here. This file should contain your private API key.")]
    public TextAsset geminiKeyJsonFile;

    private string apiKey = "";
    private string apiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent";

    private void Start()
    {
        if (geminiKeyJsonFile == null)
        {
            Debug.LogError("API Key File 'GeminiKey.json' is not assigned in the Inspector on the UnityAndGeminiV3 script!");
            Debug.LogWarning("Please ensure you have renamed 'GeminiKey.template.json' to 'GeminiKey.json', filled in your API Key, and then assigned it to the Inspector slot.");
            return;
        }
        try
        {
            UnityAndGeminiKey jsonApiKey = JsonUtility.FromJson<UnityAndGeminiKey>(geminiKeyJsonFile.text);
            apiKey = jsonApiKey.key;

            if (string.IsNullOrEmpty(apiKey) || apiKey == "GANTI_DENGAN_API_KEY_GEMINI_ANDA")
            {
                Debug.LogError("The API Key inside 'GeminiKey.json' has not been filled out. Please edit the file.");
                apiKey = ""; 
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load API Key from JSON: {e.Message}");
        }
    }
    
    public void RequestGemini(string prompt, List<Content> history = null, string systemInstruction = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("Cannot send request to Gemini: API Key is missing or invalid.");
            OnGeminiResponse?.Invoke("Missing API Key", false);
            return;
        }
        StartCoroutine(SendRequestToGemini(prompt, history, systemInstruction));
    }

    private IEnumerator SendRequestToGemini(string prompt, List<Content> history, string systemInstruction)
    {
        string url = $"{apiEndpoint}?key={apiKey}";

        List<Content> contentsList = new List<Content>();

        if (!string.IsNullOrEmpty(systemInstruction))
        {
            contentsList.Add(CreateContent("user", systemInstruction));
            contentsList.Add(CreateContent("model", "Understood. I will follow the instructions."));
        }

        if (history != null)
        {
            contentsList.AddRange(history);
        }

        contentsList.Add(CreateContent("user", prompt));

        ChatRequest requestBody = new ChatRequest
        {
            contents = contentsList.ToArray(),
            generationConfig = new GenerationConfig(),
            safetySettings = new SafetySetting[] {
                new SafetySetting { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                new SafetySetting { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                new SafetySetting { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                new SafetySetting { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
            }
        };

        string jsonData = JsonUtility.ToJson(requestBody);
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

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
                OnGeminiResponse?.Invoke(www.downloadHandler.text, false);
            }
            else
            {
                Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);

                if (response.candidates == null || response.candidates.Length == 0)
                {
                    string reason = "Response was empty or blocked.";
                    if(response.promptFeedback != null && response.promptFeedback.safetyRatings != null && response.promptFeedback.safetyRatings.Length > 0)
                    {
                        reason += $" Block reason: {response.promptFeedback.safetyRatings[0].category}";
                    }
                    Debug.LogWarning(reason + "\nFull response: " + www.downloadHandler.text);
                    OnGeminiResponse?.Invoke(reason, false);
                }
                else
                {
                    string text = response.candidates[0].content.parts[0].text;
                    OnGeminiResponse?.Invoke(text, true);
                }
            }
        }
    }
    
    private Content CreateContent(string role, string text)
    {
        return new Content { role = role, parts = new Part[] { new Part { text = text } } };
    }
}