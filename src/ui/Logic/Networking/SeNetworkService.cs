using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic.Networking
{
    public class SeNetworkService : IDisposable
    {
        private static readonly Lazy<HttpClient> _httpClient = new(() => CreateDefaultHttpClient());
        private static readonly JsonSerializerSettings _jsonSettings = new()
        {
            MaxDepth = 20,
            NullValueHandling = NullValueHandling.Ignore
        };

        public string BaseUrl { get; }

        public SeNetworkService(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("URL cannot be null or empty", nameof(baseUrl));
            }

            if (!Uri.IsWellFormedUriString(baseUrl, UriKind.Absolute))
            {
                throw new ArgumentException($"'{baseUrl}' is not a valid URL.", nameof(baseUrl));
            }

            BaseUrl = baseUrl.Trim().TrimEnd('/') + "/";
            _httpClient.Value.BaseAddress = new Uri(BaseUrl);
        }

        private static HttpClient CreateDefaultHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }

        public async Task<T> PostAsync<T>(string path, object content = null)
        {
            var response = await SendRequestAsync(HttpMethod.Post, path, content);
            return await ReadResponseAsync<T>(response);
        }

        public T Post<T>(string path, object content = null)
        {
            return PostAsync<T>(path, content).GetAwaiter().GetResult();
        }

        public async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string uri, object content = null)
        {
            using var request = new HttpRequestMessage(method, uri);

            if (content != null)
            {
                var json = JsonConvert.SerializeObject(content, _jsonSettings);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return await _httpClient.Value.SendAsync(request);
        }

        private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(jsonResponse, _jsonSettings);
        }

        public readonly struct SeSequence
        {
            public int Index { get; }
            public int StartMilliseconds { get; }
            public int EndMilliseconds { get; }
            public string Text { get; }

            public SeSequence(int index, int startMilliseconds, int endMilliseconds, string text)
            {
                Index = index;
                StartMilliseconds = startMilliseconds;
                EndMilliseconds = endMilliseconds;
                Text = text ?? string.Empty;
            }
        }

        [Serializable]
        public readonly struct SeUser
        {
            public string UserName { get; }
            public string Ip { get; }
            public DateTime LastActivity { get; }

            public SeUser(string userName, string ip, DateTime lastActivity)
            {
                UserName = userName ?? string.Empty;
                Ip = ip ?? string.Empty;
                LastActivity = lastActivity;
            }
        }

        public class SeUpdate
        {
            public DateTime Time { get; set; }
            public SeUser User { get; set; }
            public int Index { get; set; }
            public int StartMilliseconds { get; set; }
            public int EndMilliseconds { get; set; }
            public string Text { get; set; }
            public string Action { get; set; }

            public SeUpdate()
            {
            }

            public SeUpdate(SeUser user, int index, int startMilliseconds, int endMilliseconds, string text, string action)
            {
                User = user;
                Index = index;
                StartMilliseconds = startMilliseconds;
                EndMilliseconds = endMilliseconds;
                Text = text;
                Action = action;
                Time = DateTime.Now;
            }
        }

        public class SeSession
        {
            public DateTime Started { get; private set; }
            public string Id { get; private set; }
            public string FileName { get; private set; }
            public List<SeSequence> Subtitle { get; private set; }
            public List<SeSequence> OriginalSubtitle { get; private set; }
            public List<SeUser> Users { get; private set; }
            public List<SeUpdate> Updates { get; private set; }

            public SeSession(string sessionId, List<SeSequence> subtitle, string fileName, List<SeSequence> originalsubtitle)
            {
                Started = DateTime.Now;
                Id = sessionId;
                FileName = fileName;
                Subtitle = subtitle;
                OriginalSubtitle = originalsubtitle;
                Renumber();
                Users = new List<SeUser>();
                Updates = new List<SeUpdate>();
            }

            public void Renumber()
            {
                for (int i = 0; i < Subtitle.Count; i++)
                {
                    Subtitle[i].Index = i;
                }
            }
        }

        public class StartRequest
        {
            public string SessionId { get; set; }
            public string UserName { get; set; }
            public string FileName { get; set; }
            public List<SeSequence> Subtitle { get; set; }
            public List<SeSequence> OriginalSubtitle { get; set; }
        }

        public class StartResponse
        {
            public SeUser User { get; set; }
            public string Message { get; set; }
        }

        public StartResponse Start(StartRequest request)
        {
            return Post<StartResponse>("Start", request);
        }

        public class JoinRequest
        {
            public string SessionId { get; set; }
            public string UserName { get; set; }
        }

        public class JoinResponse
        {
            public List<SeUser> Users { get; set; }
            public string Message { get; set; }
        }

        public JoinResponse Join(JoinRequest request)
        {
            return Post<JoinResponse>("Join", request);
        }

        public class LeaveRequest
        {
            public string SessionId { get; set; }
            public string UserName { get; set; }
        }

        public void Leave(LeaveRequest request)
        {
            Post<JoinResponse>("Leave", request);
        }

        public class DeleteLinesRequest
        {
            public string SessionId { get; set; }
            public List<int> Indices { get; set; }
            public SeUser User { get; set; }
        }

        public bool DeleteLines(DeleteLinesRequest request)
        {
            return Post<bool>("DeleteLines", request);
        }

        public class InsertLineRequest
        {
            public string SessionId { get; set; }
            public int Index { get; set; }
            public int StartMilliseconds { get; set; }
            public int EndMilliseconds { get; set; }
            public string Text { get; set; }
            public SeUser User { get; set; }
        }

        public bool InsertLine(InsertLineRequest request)
        {
            return Post<bool>("InsertLine", request);
        }

        public class UpdateLineRequest
        {
            public string SessionId { get; set; }
            public int Index { get; set; }
            public SeSequence Sequence { get; set; }
            public SeUser User { get; set; }
        }

        public bool UpdateLine(UpdateLineRequest request)
        {
            return Post<bool>("UpdateLine", request);
        }

        public class SendMessageRequest
        {
            public string SessionId { get; set; }
            public string Text { get; set; }
            public SeUser User { get; set; }
        }

        public bool SendMessage(SendMessageRequest request)
        {
            return Post<bool>("SendMessage", request);
        }

        public class GetUpdatesRequest
        {
            public string SessionId { get; set; }
            public string UserName { get; set; }
            public DateTime LastUpdateTime { get; set; }
        }

        public class GetUpdatesResponse
        {
            public List<SeUpdate> Updates { get; set; }
            public string Message { get; set; }
            public DateTime NewUpdateTime { get; set; }
            public int NumberOfLines { get; set; }
        }

        public GetUpdatesResponse GetUpdates(GetUpdatesRequest request)
        {
            return Post<GetUpdatesResponse>("GetUpdates", request);
        }

        public class GetSubtitleRequest
        {
            public string SessionId { get; set; }
        }

        public class GetSubtitleResponse
        {
            public List<SeSequence> Subtitle { get; set; }
            public string FileName { get; set; }
            public DateTime UpdateTime { get; set; }
        }

        public GetSubtitleResponse GetSubtitle(GetSubtitleRequest request)
        {
            return Post<GetSubtitleResponse>("GetSubtitle", request);
        }

        public class GetOriginalSubtitleRequest
        {
            public string SessionId { get; set; }
        }

        public class GetOriginalSubtitleResponse
        {
            public List<SeSequence> Subtitle { get; set; }
        }

        public GetOriginalSubtitleResponse GetOriginalSubtitle(GetOriginalSubtitleRequest request)
        {
            return Post<GetOriginalSubtitleResponse>("GetOriginalSubtitle", request);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _httpClient.IsValueCreated)
            {
                _httpClient.Value?.Dispose();
            }
        }
    }
}