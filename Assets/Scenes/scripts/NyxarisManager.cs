using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class NyxarisManager : MonoBehaviour
{
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

    [System.Serializable]
    public class NyxarisRequest { public string message; public string mode; public float trust; }
    [System.Serializable]
    public class NyxarisResponse { public string response; public string emotion; }

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
        // Optional: Press the Escape key to close the chat interface
        if (mainInterfacePanel != null && mainInterfacePanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            HideInterface();
        }
        if (!Input.GetKeyDown(KeyCode.LeftShift) && !Input.GetKeyDown(KeyCode.RightShift))
        {
            return;
        }
        // Find the manager and tell it to show up
        FindObjectOfType<NyxarisManager>().ShowInterface();
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
        NyxarisRequest requestData = new NyxarisRequest { message = msg, mode = "idle", trust = 0.5f };
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
            case "cutely-annoyed": portrait.sprite = annoyed; break;
            case "explaining": portrait.sprite = explaining; break;
            default: portrait.sprite = neutral; break;
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