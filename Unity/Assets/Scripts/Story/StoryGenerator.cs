using System;
using System.IO;
using UnityEngine;

public class StoryGenerator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GPTManager gpt;

    [Header("Seed")]
    [TextArea(2, 5)]
    [SerializeField] private string seed = "폐허가 된 관문 도시, 용병단 운영, 퍼마데스";

    [SerializeField] private bool retryOnceOnParseFail = true;
    [SerializeField] private bool autoGenerateOnPlay = false;

    private void Start()
    {
        Debug.Log("[StoryGenerator] Start() 실행됨");
        if (autoGenerateOnPlay) GenerateAndSave();
    }

    [ContextMenu("Generate Story Package And Save")]
    public void GenerateAndSave()
    {
        if (gpt == null)
        {
            Debug.LogError("[StoryGenerator] GPTManager 연결 안 됨");
            return;
        }

        string prompt = BuildPrompt(seed);

        gpt.RequestJson(
            prompt,
            onSuccessJson: (json) => HandleJsonResult(json, allowRetry: retryOnceOnParseFail),
            onError: (err) => Debug.LogError("[StoryGenerator] GPT 호출 실패:\n" + err)
        );
    }

    private void HandleJsonResult(string json, bool allowRetry)
    {
        json = (json ?? "").Trim();

        // 콘솔에 잘릴 수 있으니, 실패 대비로 원본을 파일로 저장해둠(디버그용)
        SaveDebugRaw(json, "raw_last.json");

        // { ... }만 뽑아보는 안전망
        if (GPTManager.TryExtractJsonObject(json, out var extracted))
            json = extracted;

        if (TryParse(json, out var data, out var err))
        {
            if (string.IsNullOrEmpty(data.seed)) data.seed = seed;

            StoryStorage.Save(data);
            Debug.Log("[StoryGenerator] 생성/저장 완료");
            Debug.Log("[StoryGenerator] 저장 경로: " + StoryStorage.GetPath());
            return;
        }

        Debug.LogError("[StoryGenerator] JSON 파싱 실패: " + err);
        Debug.LogError("[StoryGenerator] RAW JSON 파일 저장됨: " +
                       Path.Combine(Application.persistentDataPath, "raw_last.json"));

        if (!allowRetry) return;

        Debug.LogWarning("[StoryGenerator] JSON 파싱 실패 -> 1회 재요청 시도");

        // ✅ 재요청은 '고쳐서 다시 출력' 방식이 훨씬 성공률 높음
        string fixPrompt =
            "아래는 JSON 규칙을 위반한 출력이야. 반드시 유효한 JSON 객체로 고쳐서 다시 출력해.\n" +
            "- 설명/코드블록/마크다운 금지\n" +
            "- 콤마/대괄호/따옴표 규칙 정확히\n" +
            "- 문자열 안에 큰따옴표(\") 사용 금지\n" +
            "- 줄바꿈 없이 한 줄 문장만\n\n" +
            "잘못된 JSON:\n" + json + "\n\n" +
            "정답은 오직 JSON 객체만 출력:";

        gpt.RequestJson(
            fixPrompt,
            onSuccessJson: (json2) => HandleJsonResult(json2, allowRetry: false),
            onError: (err2) => Debug.LogError("[StoryGenerator] 재시도 호출 실패:\n" + err2)
        );
    }

    private static void SaveDebugRaw(string text, string fileName)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllText(path, text);
        }
        catch { /* ignore */ }
    }

    private static bool TryParse(string json, out StoryPackage data, out string error)
    {
        data = null;
        error = null;

        try
        {
            data = JsonUtility.FromJson<StoryPackage>(json);
            if (data == null) { error = "FromJson 결과 null"; return false; }
            if (string.IsNullOrEmpty(data.worldName)) { error = "worldName 누락"; return false; }
            if (string.IsNullOrEmpty(data.logline)) { error = "logline 누락"; return false; }
            if (data.storyBeats == null || data.storyBeats.Count < 8) { error = "storyBeats 부족(8개 이상 필요)"; return false; }
            if (data.mercenaries == null || data.mercenaries.Count < 8) { error = "mercenaries 부족(8명 필요)"; return false; }
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    private static string BuildPrompt(string seed)
    {
        return
$@"반드시 JSON 객체만 출력해. 설명/코드블록/마크다운/추가 텍스트 금지.
아래 스키마를 정확히 따라.

{{
  ""worldName"": ""string"",
  ""logline"": ""string"",
  ""storyBeats"": [""string""],
  ""mercenaries"": [
    {{
      ""id"": ""m001"",
      ""name"": ""string"",
      ""job"": ""string"",
      ""race"": ""string"",
      ""trait"": ""string"",
      ""tagline"": ""string""
    }}
  ],
  ""seed"": ""string""
}}

규칙(중요):
- storyBeats/tagline/name/worldName/logline/trait/job/race 문자열에 큰따옴표("") 절대 사용 금지
- 줄바꿈 금지(각 문장은 한 줄)
- storyBeats는 8~12개, 각 1문장
- mercenaries는 8명, id는 m001~m008
- job/race/trait는 중복 최소화
- seed는 아래 키워드를 그대로 넣어

seed 키워드: {seed}";
    }
}