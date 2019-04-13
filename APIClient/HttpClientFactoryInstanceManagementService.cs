using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace APIClient
{
    class HttpClientFactoryInstanceManagementService
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly AusPostClient _ausPostClient;
        private readonly ILogger _logger;

        public HttpClientFactoryInstanceManagementService( AusPostClient ausPostClient, ILogger logger)
        {
            _ausPostClient = ausPostClient;
            logger.ForContext<HttpClientFactoryInstanceManagementService>();
            _logger = logger;
        }

        public async Task<string> GetAccountInfo()
        {
          var info = await  _ausPostClient.GetAccountInfoAsync(_cancellationTokenSource.Token);
          return info;
        }
    }
}
