using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Program.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        public string? RequestId { get; set; }
        public string? ErrorMessage { get; set; }
        public string? StackTrace { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        private readonly ILogger<ErrorModel> _logger;

        public ErrorModel(ILogger<ErrorModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            
            // Получаем информацию об ошибке из контекста
            var exceptionHandlerPathFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            if (exceptionHandlerPathFeature?.Error != null)
            {
                var exception = exceptionHandlerPathFeature.Error;
                ErrorMessage = exception.Message;
                StackTrace = exception.StackTrace;
                
                // Логируем ошибку
                _logger.LogError(exception, "Необработанное исключение: {Message}", exception.Message);
            }
        }
    }

}
