using System.Diagnostics;

namespace Cataben.API.Middleware
{
    public class RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger
        )
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;
            var requestBody = await ReadRequestBodyAsync(request);

            logger.LogInformation(
                "Request: {Method} {Path} {QueryString} | Body: {Body}",
                request.Method,
                request.Path,
                request.QueryString,
                requestBody
                );

            // Swap the response stream so we can log it, but ALWAYS restore the original
            // stream in a finally. Previously the restore only ran on the success path, so
            // when a downstream component threw, ExceptionMiddleware (registered earlier in
            // the pipeline) wrote the error body into the orphaned MemoryStream and clients
            // received an empty response.
            var originalBodyStream = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await next(context);
            }
            finally
            {
                stopwatch.Stop();

                context.Response.Body = originalBodyStream;

                responseBodyStream.Seek(0, SeekOrigin.Begin);
                var responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();

                // Response compression runs later in the pipeline and writes br/gzip/deflate bytes into
                // the MemoryStream we swapped in above, so reading it back here yields compressed bytes —
                // logging those verbatim shows garbled output. Substitute a placeholder when an encoding
                // is present. (Requests aren't compressed, so request-body logging is unaffected.)
                var encoding = context.Response.Headers.ContentEncoding.ToString();
                var bodyForLog = !string.IsNullOrEmpty(encoding)
                    ? $"<omitted: {encoding}-encoded, {responseBody.Length} bytes>"
                    : Truncate(responseBody);

                logger.LogInformation(
                    "Response: {StatusCode} | Body: {Body} | Duration: {Duration} ms",
                    context.Response.StatusCode,
                    bodyForLog,
                    stopwatch.ElapsedMilliseconds
                    );

                responseBodyStream.Seek(0, SeekOrigin.Begin);
                await responseBodyStream.CopyToAsync(originalBodyStream);
            }
        }

        private async Task<string> ReadRequestBodyAsync(HttpRequest request)
        {
            if(!request.Body.CanSeek)
                request.EnableBuffering();

            request.Body.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(request.Body).ReadToEndAsync();
            request.Body.Seek(0, SeekOrigin.Begin);

            return Truncate(body);
        }

        private static string Truncate(string value) =>
            value.Length > 500 ? value[..500] + "..." : value;
    }

    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}
