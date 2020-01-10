using StreamCore.SimpleJSON;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SongRequestManager
{
    internal class WebResponse
    {
        public readonly HttpStatusCode StatusCode;
        public readonly string ReasonPhrase;
        public readonly HttpResponseHeaders Headers;
        public readonly HttpRequestMessage RequestMessage;
        public readonly bool IsSuccessStatusCode;

        private readonly byte[] _content;

        internal WebResponse(HttpResponseMessage resp, byte[] body)
        {
            StatusCode = resp.StatusCode;
            ReasonPhrase = resp.ReasonPhrase;
            Headers = resp.Headers;
            RequestMessage = resp.RequestMessage;
            IsSuccessStatusCode = resp.IsSuccessStatusCode;

            _content = body;
        }

        public byte[] ContentToBytes() => _content;
        public string ContentToString() => Encoding.UTF8.GetString(_content);
        public JSONNode ConvertToJsonNode()
        {
            return JSONNode.Parse(ContentToString());
        }
    }

    internal class WebClient
    {
        private HttpClient _client;

        internal WebClient()
        {
            _client = new HttpClient();
            _client.DefaultRequestHeaders.UserAgent.TryParseAdd($"SongRequestManager/{Plugin.Version}");
        }

        ~WebClient()
        {
            if (_client != null)
            {
                _client.Dispose();
            }
        }

        internal async Task<WebResponse> GetAsync(string url, CancellationToken token)
        {
            return await SendAsync(HttpMethod.Get, url, token);
        }

        internal async Task<byte[]> DownloadImage(string url, CancellationToken token)
        {
            var response = await SendAsync(HttpMethod.Get, url, token);

            if (response.IsSuccessStatusCode)
            {
                return response.ContentToBytes();
            }
            return null;
        }

        internal async Task<byte[]> DownloadSong(string url, CancellationToken token, IProgress<double> progress = null)
        {
            // check if beatsaver url needs to be pre-pended
            if (!url.StartsWith(@"https://beatsaver.com/"))
            {
                url = $"https://beatsaver.com/{url}";
            }

            var response = await SendAsync(HttpMethod.Get, url, token, progress: progress);

            if (response.IsSuccessStatusCode)
            {
                return response.ContentToBytes();
            }
            return null;
        }

        internal async Task<WebResponse> SendAsync(HttpMethod methodType, string url, CancellationToken token, IProgress<double> progress = null)
        {
            Plugin.Log($"{methodType.ToString()}: {url}");

            // create new request messsage
            var req = new HttpRequestMessage(methodType, url);

            // send request
            var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);

            //if ((int)resp.StatusCode == 429)
            //{
            //    // rate limiting handling
            //}

            if (token.IsCancellationRequested) throw new TaskCanceledException();

            using (var memoryStream = new MemoryStream())
            using (var stream = await resp.Content.ReadAsStreamAsync())
            {
                var buffer = new byte[8192];
                var bytesRead = 0; ;

                long? contentLength = resp.Content.Headers.ContentLength;
                var totalRead = 0;

                // send report
                progress?.Report(0);

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    if (token.IsCancellationRequested) throw new TaskCanceledException();

                    if (contentLength != null)
                    {
                        progress?.Report((double)totalRead / (double)contentLength);
                    }

                    await memoryStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                }

                progress?.Report(1);
                byte[] bytes = memoryStream.ToArray();

                return new WebResponse(resp, bytes);
            }
        }
    }
}
