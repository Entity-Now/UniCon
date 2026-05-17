using Microsoft.Extensions.Logging;
using Quartz;
using System.Net.Http;
using System.Text.Json;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace UniCon.Core.Jobs.BuiltIn
{
    /// <summary>
    /// 增强型内置任务：发送 HTTP 请求
    /// </summary>
    public class HttpJob : UniConJobBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HttpJob(ILogger<HttpJob> logger, IHttpClientFactory httpClientFactory) : base(logger)
        {
            _httpClientFactory = httpClientFactory;
        }

        public override async Task Execute(IJobExecutionContext context)
        {
            var dataMap = context.MergedJobDataMap;

            // 安全地获取参数，避免 KeyNotFoundException
            var url = GetStringSafe(dataMap, JobDataKeys.HttpUrl);
            var method = GetStringSafe(dataMap, JobDataKeys.HttpMethod) ?? "GET";
            var headersJson = GetStringSafe(dataMap, JobDataKeys.HttpHeaders);
            var queryParamsJson = GetStringSafe(dataMap, JobDataKeys.HttpQueryParams);
            var body = GetStringSafe(dataMap, JobDataKeys.HttpBody);

            if (string.IsNullOrEmpty(url))
            {
                _logger.LogWarning("HttpJob skipped: URL is null or empty.");
                return;
            }

            // 1. 处理查询参数 (RULE 2.1)
            url = AppendQueryParameters(url, queryParamsJson);

            // 2. 创建请求
            var request = new HttpRequestMessage(new HttpMethod(method), url);

            // 3. 处理请求头
            ApplyHeaders(request, headersJson);

            // 4. 处理请求体
            if (!string.IsNullOrEmpty(body))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            await SendRequestAsync(request);
        }

        private string? GetStringSafe(JobDataMap map, string key)
        {
            return map.ContainsKey(key) ? map.GetString(key) : null;
        }

        private string AppendQueryParameters(string url, string? json)
        {
            if (string.IsNullOrEmpty(json)) return url;

            try
            {
                var queryParams = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (queryParams == null) return url;

                var uriBuilder = new UriBuilder(url);
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                foreach (var kvp in queryParams)
                {
                    query[kvp.Key] = kvp.Value;
                }
                uriBuilder.Query = query.ToString();
                return uriBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse HttpJob query parameters.");
                return url;
            }
        }

        private void ApplyHeaders(HttpRequestMessage request, string? json)
        {
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (headers == null) return;

                foreach (var kvp in headers)
                {
                    request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply HttpJob headers.");
            }
        }

        private async Task SendRequestAsync(HttpRequestMessage request)
        {
            _logger.LogInformation($"Executing HttpJob: {request.Method} {request.RequestUri}");
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.SendAsync(request);
                _logger.LogInformation($"HttpJob response: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HttpJob request failed.");
            }
        }
    }
}
