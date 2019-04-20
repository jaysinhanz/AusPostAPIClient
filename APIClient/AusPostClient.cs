using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Serilog;

namespace APIClient
{
    public class AusPostClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public AusPostClient(HttpClient httpClient,IConfiguration configuration ,ILogger logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _httpClient.BaseAddress = new Uri("https://digitalapi.auspost.com.au");
            _httpClient.Timeout = new TimeSpan(0, 0, 0, 30);
            _httpClient.DefaultRequestHeaders.Clear();
        }

        public async Task<string> GetAccountInfoAsync(CancellationToken token = default)
        {
            // Set the Uri and Http Method
            var ac = _configuration.GetValue<string>("AusPostAccountNo");
            var authKey = _configuration.GetValue<string>("authKey");
            var request = new HttpRequestMessage(HttpMethod.Get, "test/shipping/v1/accounts/" + ac);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var byteArray = new UTF8Encoding().GetBytes(authKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
            {
               
                var ret = response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : string.Empty;
                _logger.ForContext<AusPostClient>();
                _logger.Information(ret);
                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception e)
                {
                    _logger.Information(e.InnerException.Message);
                    return string.Empty;
                }
                
                 return ret;
            }
        }
    }
}