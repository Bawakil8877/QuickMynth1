namespace QuickMynth1.Services
{
    public class LoggingHandler : DelegatingHandler
    {
        private readonly ILogger _logger;
        public LoggingHandler(ILogger<LoggingHandler> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Log request
            _logger.LogInformation("→ Request {Method} {Url}\nHeaders: {@Headers}\nBody: {Body}",
                request.Method, request.RequestUri,
                request.Headers,
                request.Content == null ? "<none>" : await request.Content.ReadAsStringAsync());

            var response = await base.SendAsync(request, cancellationToken);

            // Log response
            _logger.LogInformation("← Response {StatusCode}\nHeaders: {@Headers}\nBody: {Body}",
                (int)response.StatusCode,
                response.Headers,
                response.Content == null ? "<none>" : await response.Content.ReadAsStringAsync());

            return response;
        }
    }

}
