using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GKR_Backend.Services
{
    public class AskStepService
    {
        private const int MaxQuestionLength = 200;
        private const int MaxRequestsPerHourPerIp = 20;
        private const string Model = "claude-sonnet-4-6";
        private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";

        private readonly IHttpClientFactory _httpClientFactory;

        // 記憶體內快取：key = type + 正規化後的問題文字，重啟後清空即可
        private readonly ConcurrentDictionary<string, string> _cache = new();

        // 記憶體內限流：每個 IP 一份請求時間佇列，用來算「過去一小時內」的次數
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _requestLog = new();

        public AskStepService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public static bool IsQuestionTooLong(string? question)
        {
            return (question?.Length ?? 0) > MaxQuestionLength;
        }

        // 對同一個 IP 檢查並登記這次請求；回傳 true 代表已超過每小時上限，應該拒絕
        public bool IsRateLimited(string clientIp)
        {
            var now = DateTime.UtcNow;
            var queue = _requestLog.GetOrAdd(clientIp, _ => new Queue<DateTime>());

            lock (queue)
            {
                while (queue.Count > 0 && now - queue.Peek() > TimeSpan.FromHours(1))
                {
                    queue.Dequeue();
                }

                if (queue.Count >= MaxRequestsPerHourPerIp)
                {
                    return true;
                }

                queue.Enqueue(now);
                return false;
            }
        }

        private static string BuildCacheKey(string type, string question)
        {
            var normalized = question.Trim().ToLowerInvariant();
            return $"{type}::{normalized}";
        }

        public async Task<(string Answer, bool FromCache)> AskAsync(AskStepRequest request)
        {
            var type = request.Type ?? "UNKNOWN";
            var question = request.Question ?? "";
            var cacheKey = BuildCacheKey(type, question);

            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return (cached, true);
            }

            var answer = await CallClaudeAsync(request);
            _cache[cacheKey] = answer;
            return (answer, false);
        }

        private async Task<string> CallClaudeAsync(AskStepRequest request)
        {
            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("ANTHROPIC_API_KEY 環境變數未設定。");
            }

            const string systemPrompt =
                "你是「GKR Protocol 互動式視覺化教學工具」裡的助教。GKR Protocol 是一種讓 Verifier " +
                "能夠有效驗證分層算術電路計算結果的互動式證明協議，核心手法是把「電路計算是否正確」" +
                "轉化成一系列的 Sumcheck（多項式求和）問題，每一輪由 Verifier 送出隨機挑戰、Prover 回應，" +
                "從輸出層逐層驗證到輸入層。使用者目前正盯著協議裡的某一個步驟發問，你只能根據使用者提供的" +
                "該步驟資訊（事件類型、層數、輪次、附帶數值）來回答，絕對不可以編造協議中沒有提到的技術細節，" +
                "不確定的地方就誠實說不確定。回答一律使用繁體中文，並嚴格控制在 150 字以內。" +
                "回答只能用純文字，不可以使用任何 Markdown 語法（例如 #、##、*、**、-、1.、`、``` 或表格），" +
                "也不要用條列或標題排版，直接寫成一般的敘述句段落即可。";

            var dataJson = request.Data == null ? "null" : JsonSerializer.Serialize(request.Data);
            var userContent =
                $"目前步驟資訊：\n" +
                $"事件類型：{request.Type}\n" +
                $"層數：{request.Layer}\n" +
                $"輪次：{request.Round}\n" +
                $"附帶資料：{dataJson}\n\n" +
                $"使用者問題：{request.Question}";

            var payload = new
            {
                model = Model,
                max_tokens = 500,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userContent }
                }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, AnthropicApiUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("x-api-key", apiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");

            var client = _httpClientFactory.CreateClient("anthropic");
            var response = await client.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Anthropic API 呼叫失敗：{response.StatusCode} - {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            return text ?? "";
        }
    }
}
