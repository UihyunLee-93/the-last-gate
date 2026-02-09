using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class GPTManager : MonoBehaviour
{
    [Header("API Key")]
    [Tooltip("Inspector에만 넣고, GitHub에 올릴 땐 비워두세요.")]
    [SerializeField] private string apiKey = "";

    [Header("Model")]
    [SerializeField] private string model = "gpt-4.1-mini";

    [TextArea(2, 5)]
    [SerializeField] private string systemPrompt = "You are a fantasy game narrator.";

    [Header("Request Options")]
    [SerializeField] private int timeoutSeconds = 30;
    [SerializeField] private float temperature = 0.2f;
    [SerializeField] private int maxTokens = 800;

    private const string URL = "https://api.openai.com/v1/chat/completions";

    public void RequestText(string prompt, Action<string> onSuccess, Action<string> onError = null)
    {
        StartCoroutine(SendChatCompletion(prompt, jsonMode: false, onSuccess, onError));
    }


    public void RequestJson(string prompt, Action<string> onSuccessJson, Action<string> onError = null)
    {
        StartCoroutine(SendChatCompletion(prompt, jsonMode: true,
            onSuccess: (content) =>
            {
                
                if (TryExtractJsonObject(content, out var json))
                    onSuccessJson?.Invoke(json);
                else
                    onError?.Invoke("[GPT JSON ERROR] JSON object를 추출하지 못했습니다.\nRAW:\n" + content);
            },
            onError: onError
        ));
    }


    public void RequestJsonParsed<T>(string prompt, Action<T> onSuccess, Action<string> onError = null)
    {
        RequestJson(prompt,
            (json) =>
            {
                try
                {
                    var obj = JsonUtility.FromJson<T>(json);
                    if (obj == null)
                    {
                        onError?.Invoke("[GPT JSON PARSE ERROR] FromJson 결과가 null 입니다.\nJSON:\n" + json);
                        return;
                    }
                    onSuccess?.Invoke(obj);
                }
                catch (Exception e)
                {
                    onError?.Invoke("[GPT JSON PARSE EXCEPTION] " + e.Message + "\nJSON:\n" + json);
                }
            },
            onError
        );
    }

    private IEnumerator SendChatCompletion(string prompt, bool jsonMode, Action<string> onSuccess, Action<string> onError)
    {
        prompt ??= "";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onError?.Invoke("[GPT ERROR] API Key가 비어있습니다. Inspector에서 입력하세요.");
            yield break;
        }

     
        string finalSystem = jsonMode
            ? (systemPrompt + " Output must be a valid JSON object only. Do not include any extra text.")
            : systemPrompt;

        string finalUser = jsonMode
            ? ("반드시 JSON 객체만 출력해. 설명/코드블록/마크다운/추가 텍스트 금지.\n\n" + prompt)
            : prompt;

        string safeSystem = EscapeForJson(finalSystem);
        string safeUser = EscapeForJson(finalUser);

     
        string responseFormatPart = jsonMode
            ? ",\"response_format\":{\"type\":\"json_object\"}"
            : "";

        string bodyJson =
            "{"
            + $"\"model\":\"{EscapeForJson(model)}\","
            + $"\"temperature\":{temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)},"
            + $"\"max_tokens\":{maxTokens},"
            + "\"messages\":["
            + $"{{\"role\":\"system\",\"content\":\"{safeSystem}\"}},"
            + $"{{\"role\":\"user\",\"content\":\"{safeUser}\"}}"
            + "]"
            + responseFormatPart
            + "}";

        using (UnityWebRequest request = new UnityWebRequest(URL, "POST"))
        {
            request.timeout = timeoutSeconds;

            byte[] body = Encoding.UTF8.GetBytes(bodyJson);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string err = $"[GPT ERROR] {request.responseCode} / {request.error}\n{request.downloadHandler.text}";
                Debug.LogError(err);
                onError?.Invoke(err);
                yield break;
            }

            string raw = request.downloadHandler.text;

       
            string content = TryExtractContent(raw, out string parseError);
            if (content == null)
            {
                string err = $"[GPT PARSE ERROR] {parseError}\nRAW:\n{raw}";
                Debug.LogError(err);
                onError?.Invoke(err);
                yield break;
            }

            onSuccess?.Invoke(content.Trim());
        }
    }


    [Serializable] private class GPTResponse { public Choice[] choices; }
    [Serializable] private class Choice { public Message message; }
    [Serializable] private class Message { public string content; }

    private static string TryExtractContent(string rawJson, out string error)
    {
        error = null;

        try
        {
            var parsed = JsonUtility.FromJson<GPTResponse>(rawJson);

            if (parsed == null || parsed.choices == null || parsed.choices.Length == 0)
            {
                error = "choices가 비어있거나 응답 구조가 예상과 다릅니다.";
                return null;
            }

            var msg = parsed.choices[0]?.message;
            if (msg == null)
            {
                error = "choices[0].message가 null 입니다.";
                return null;
            }

            if (string.IsNullOrEmpty(msg.content))
            {
                error = "message.content가 비어있습니다.";
                return null;
            }

            return msg.content;
        }
        catch (Exception e)
        {
            error = e.Message;
            return null;
        }
    }

 
    private static string EscapeForJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\u2028", "\\u2028")
            .Replace("\u2029", "\\u2029");
    }

   
    public static bool TryExtractJsonObject(string text, out string json)
    {
        json = null;
        if (string.IsNullOrEmpty(text)) return false;

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');

        if (start < 0 || end < 0 || end <= start) return false;

        json = text.Substring(start, end - start + 1).Trim();
        return true;
    }

    // 테스트 (JSON)
  
    [ContextMenu("GPT Test JSON Call")]
    public void TestJsonCall()
    {
        string prompt =
@"아래 스키마로 생성해.
{
  ""worldName"": ""string"",
  ""logline"": ""string"",
  ""storyBeats"": [""string""],
  ""mercenaries"": [
    { ""id"": ""m001"", ""name"": ""string"", ""job"": ""string"", ""race"": ""string"", ""trait"": ""string"", ""tagline"": ""string"" }
  ],
  ""seed"": ""string""
}
조건:
- storyBeats 8개
- mercenaries 3명(id m001~m003)
seed: ""폐허가 된 관문 도시, 용병단 운영""";

        RequestJson(prompt,
            (json) =>
            {
                Debug.Log("[GPT JSON TEST RESULT]");
                Debug.Log(json);
            },
            (err) =>
            {
                Debug.LogError("[GPT JSON TEST FAILED]");
                Debug.LogError(err);
            }
        );
    }
}