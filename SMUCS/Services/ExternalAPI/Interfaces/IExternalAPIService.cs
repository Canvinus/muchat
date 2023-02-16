using Microsoft.AspNetCore.Mvc;

namespace SMUCS.Services.ExternalAPI.Interfaces
{
    public interface IExternalAPIService
    {
        Task<U?> Convert<T, U>(T obj);

        Task<JsonResult> PostFormData<T>(string endpoint, string key, IFormFile file, List<KeyValuePair<string, string>> headers);
        
        Task<JsonResult> CallAsync<T, U>(string method, string endpoint, T content, List<KeyValuePair<string, string>> headers);

        Task<JsonResult> CallAsync<U>(string method, string endpoint, List<KeyValuePair<string, string>> headers);

        Task<HttpResponseMessage> CallAsync(string method, string endpoint, List<KeyValuePair<string, string>> headers);
    }
}
