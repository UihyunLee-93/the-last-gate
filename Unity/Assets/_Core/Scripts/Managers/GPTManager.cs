using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class GPTManager : MonoBehaviour
{
    [Header("GPT Settings")]
    [Tooltip("Inspector에 키를 넣고, GitHub에 올릴 땐 비워둘것.")]
    [SerializeField] private string apiKey = "";

    [Tooltip("플레이 시작 시 자동 테스트 호출 (디버그용)")]
    [SerializeField] private bool autoTestOnPlay = false;

    [Header("Model")]
    [SerializeField] private string model = "gpt-4.1-mini";

    [TextArea(2, 5)]
    [SerializeField] private string systemPrompt = "You are a fantasy game narrator.";

    private const string URL = "https://api.openai.com/v1/chat/completions";

   
   // 외부에서 쓰는 호출 함수
   
    public void Request(string prompt, Action<string> onSuccess, Action<string> onError = null)
    {
        StartCoroutine(AskGPT(prompt, onSuccess, onError));
    }

   // 실제 GPT 호출 코루틴
 
    private IEnumerator AskGPT(string prompt, Action<string> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onError?.Invoke("[GPT ERROR] API Key가 비어있습니다. Inspector에서 입력하세요.");
            yield break;
        }

        // JSON 문자열에 들어갈 특수문자 이스케이프 처리
        string safePrompt = EscapeForJson(prompt);
        string safeSystem = EscapeForJson(systemPrompt);

        string bodyJson =
            "{"
            + $"\"model\":\"{model}\","
            + "\"messages\":["
            + $"{{\"role\":\"system\",\"content\":\"{safeSystem}\"}},"
            + $"{{\"role\":\"user\",\"content\":\"{safePrompt}\"}}"
            + "]"
            + "}";

        using (UnityWebRequest request = new UnityWebRequest(URL, "POST"))
        {
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

            // JSON 파싱: content만 뽑기
            string content = TryExtractContent(raw, out string parseError);
            if (content == null)
            {
                string err = $"[GPT PARSE ERROR] {parseError}\nRAW:\n{raw}";
                Debug.LogError(err);
                onError?.Invoke(err);
                yield break;
            }

            onSuccess?.Invoke(content);
        }
    }

    //  테스트 전용 (Inspector 우클릭 메뉴)
  
    [ContextMenu("GPT Test Call")]
    public void TestGPTCall()
    {
        Debug.Log("[GPT TEST] 호출 시작");

        Request(
            "용병단이 첫 관문 앞에 섰을 때의 짧은 묘사 한 문장",
            (text) =>
            {
                Debug.Log("[GPT TEST RESULT] (content only)");
                Debug.Log(text);
            },
            (err) =>
            {
                Debug.LogError("[GPT TEST FAILED]");
                Debug.LogError(err);
            }
        );
    }

    private void Awake()
    {
        if (autoTestOnPlay)
            TestGPTCall();
    }

   
    //  응답 파싱용 클래스들
    [Serializable] private class GPTResponse { public Choice[] choices; }
    [Serializable] private class Choice { public Message message; }
    [Serializable] private class Message { public string content; }

    private string TryExtractContent(string rawJson, out string error)
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

            if (parsed.choices[0] == null || parsed.choices[0].message == null)
            {
                error = "choices[0].message가 null 입니다.";
                return null;
            }

            string content = parsed.choices[0].message.content;

            if (string.IsNullOrEmpty(content))
            {
                error = "message.content가 비어있습니다.";
                return null;
            }

            // 가끔 앞뒤 공백/줄바꿈이 과한 경우 정리
            return content.Trim();
        }
        catch (Exception e)
        {
            error = e.Message;
            return null;
        }
    }

    //  JSON 문자열 안전 이스케이프
  
    private static string EscapeForJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}