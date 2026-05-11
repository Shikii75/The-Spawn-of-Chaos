using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class NyxarisManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text dialogueText;
    public TMP_InputField messageInput;
    public Image portrait;

    [Header("Sprites")]
    public Sprite neutral;
    public Sprite explaining;
    public Sprite annoyed;

    [System.Serializable]
    public class NyxarisRequest
    {
        public string message;
        public string mode;
        public float trust;
    }

    [System.Serializable]
    public class NyxarisResponse
    {
        public string response;
        public string emotion;
    }

    public void SendInputMessage()
    {
        string msg = messageInput.text;

        if (string.IsNullOrWhiteSpace(msg))
            return;

        StartCoroutine(SendRequest(msg));
        messageInput.text = "";
        messageInput.ActivateInputField(); // keeps typing smooth
    }

    IEnumerator SendRequest(string msg)
    {
        NyxarisRequest requestData = new NyxarisRequest
        {
            message = msg,
            mode = "idle",
            trust = 0.5f
        };

        string json = JsonUtility.ToJson(requestData);

        UnityWebRequest request = new UnityWebRequest(
            "http://127.0.0.1:5001/nyxaris",
            "POST"
        );

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            NyxarisResponse response =
                JsonUtility.FromJson<NyxarisResponse>(
                    request.downloadHandler.text
                );

            SetEmotion(response.emotion);
            StartCoroutine(TypeText(response.response));
        }
        else
        {
            dialogueText.text = "Connection failed.";
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