using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class NyxarisManager : MonoBehaviour
{
    public static NyxarisManager Instance { get; private set; }

    [Header("UI Structure")]
    // Drag your "MainInterface" GameObject here in the inspector
    public GameObject mainInterfacePanel; 

    [Header("UI Elements")]
    public TMP_Text dialogueText;
    public TMP_InputField messageInput;
    public Image portrait;

    [Header("Sprites")]
    public Sprite neutral;
    public Sprite explaining;
    public Sprite annoyed;

    [Header("Current Game State Connection")]
    [Tooltip("Options: idle, combat, story")]
    public string currentMode = "idle";
    [Range(0f, 1f)]
    public float currentTrust = 0.5f;

    [System.Serializable]
    public class NyxarisRequest { public string message; public string mode; public float trust; }
    [System.Serializable]
    public class NyxarisResponse { public string response; public string emotion; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Automatically hide the interface when the game starts up
        HideInterface();
    }

    // Call this public method from your player interaction script, a trigger zone, or a keypress
    public void ShowInterface()
    {
        if (mainInterfacePanel != null)
        {
            mainInterfacePanel.SetActive(true);
            messageInput.text = "";
            messageInput.ActivateInputField(); // Instantly focuses the text box for typing
        }
    }

    public void HideInterface()
    {
        if (mainInterfacePanel != null)
        {
            mainInterfacePanel.SetActive(false);
        }
    }

    void Update()
    {
        // Press the Escape key to close the chat interface
        if (mainInterfacePanel != null && mainInterfacePanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            HideInterface();
        }
        
        // Use Shift to bring up interface if it's hidden
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            if (mainInterfacePanel != null && !mainInterfacePanel.activeSelf)
            {
                ShowInterface();
            }
        }
    }

    public void SendInputMessage()
    {
        string msg = messageInput.text;

        if (string.IsNullOrWhiteSpace(msg))
            return;

        StartCoroutine(SendRequest(msg));
        messageInput.text = "";
        messageInput.ActivateInputField();
    }

    IEnumerator SendRequest(string msg)
    {
        // 🔥 Now leverages dynamic system variables instead of hardcoded placeholders
        NyxarisRequest requestData = new NyxarisRequest { 
            message = msg, 
            mode = currentMode, 
            trust = currentTrust 
        };
        string json = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request = new UnityWebRequest("http://127.0.0.1:5001/nyxaris", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                NyxarisResponse response = JsonUtility.FromJson<NyxarisResponse>(request.downloadHandler.text);
                SetEmotion(response.emotion);
                StartCoroutine(TypeText(response.response));
            }
            else
            {
                dialogueText.text = "Connection failed.";
            }
        }
    }

    void SetEmotion(string emotion)
    {
        switch (emotion)
        {
            case "cutely-annoyed": 
                portrait.sprite = annoyed; 
                break;
            case "explaining": 
                portrait.sprite = explaining; 
                break;
            default: 
                portrait.sprite = neutral; 
                break;
        }
    }

    IEnumerator TypeText(string text)
    {
        dialogueText.text = "";
        foreach (char c in text)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(0.02f);
        }
    }
}