using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace APIClient
{
    internal class LogRequestHandler: DelegatingHandler
    {
        private readonly ILogger _logger;

        public LogRequestHandler(ILogger logger):base()
        {
            _logger = logger;
        }

        public LogRequestHandler(HttpMessageHandler innerHandler,ILogger logger):base(innerHandler)
        {
            _logger = logger;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken )
        {
           // var logger = _logger.ForContext<LogRequestHandler>();
            _logger.ForContext("OtherData", "Test Data").
                ForContext("Source",typeof(LogRequestHandler).FullName).
                Information("request {request}",request);
            return base.SendAsync(request, cancellationToken);
        }
    }
}