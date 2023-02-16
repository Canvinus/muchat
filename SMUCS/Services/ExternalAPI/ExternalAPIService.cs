using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SMUCS.Services.ExternalAPI.Interfaces;
using System.Text;

namespace SMUCS.Services.ExternalAPI
{
    public class ExternalAPIService : IExternalAPIService
    {
        private readonly HttpClient _client = new HttpClient();
        private readonly string _host;

        public ExternalAPIService(IConfiguration configuration)
        {
            _host = configuration["MainSystemHost"];
        }

        public async Task<U?> Convert<T, U>(T obj)
        {
            var jsonString = JsonConvert.SerializeObject(obj);
            var result = JsonConvert.DeserializeObject<U>(jsonString);

            return result;
        }

        public async Task<JsonResult> PostFormData<T>(string endpoint, string key, IFormFile file,
            List<KeyValuePair<string, string>> headers)
        {
            _client.DefaultRequestHeaders.Clear();
            foreach (var header in headers)
                _client.DefaultRequestHeaders.Add(header.Key, header.Value);

            MultipartFormDataContent httpContent = new MultipartFormDataContent();
            httpContent.Add(new StreamContent(file.OpenReadStream()), key, file.FileName);

            HttpResponseMessage response = new HttpResponseMessage();
            response = await _client.PostAsync(
                _host + endpoint,
                httpContent);
            
            var responseString = await response.Content.ReadAsStringAsync();

            return new JsonResult(JsonConvert.DeserializeObject<T>(responseString))
                { StatusCode = (int)response.StatusCode };
        }

        public async Task<JsonResult> CallAsync<T, U>(string method, string endpoint, T content, List<KeyValuePair<string, string>> headers)
        {
            _client.DefaultRequestHeaders.Clear();
            foreach (var header in headers)
                _client.DefaultRequestHeaders.Add(header.Key, header.Value);

            var jsonObject = JsonConvert.SerializeObject(content);
            var httpContent = new StringContent(jsonObject,
                Encoding.UTF8, "application/json");

            HttpResponseMessage response = new HttpResponseMessage();
            switch (method)
            {
                case "GET":
                    response = await _client.GetAsync(
                                    _host + endpoint);
                    break;

                case "POST":
                    response = await _client.PostAsync(
                                    _host + endpoint,
                                    httpContent);
                    break;

                case "PUT":
                    response = await _client.PutAsync(
                                    _host + endpoint,
                                    httpContent);
                    break;

                case "DELETE":
                    response = await _client.DeleteAsync(
                                    _host + endpoint);
                    break;
            }


            var responseString = await response.Content.ReadAsStringAsync();
            
            return new JsonResult(JsonConvert.DeserializeObject<U>(responseString))
                { StatusCode = (int)response.StatusCode };
        }

        public async Task<JsonResult> CallAsync<U>(string method, string endpoint, List<KeyValuePair<string, string>> headers)
        {
            _client.DefaultRequestHeaders.Clear();
            foreach (var header in headers)
                _client.DefaultRequestHeaders.Add(header.Key, header.Value);

            HttpResponseMessage response = new HttpResponseMessage();
            switch (method)
            {
                case "GET":
                    response = await _client.GetAsync(
                                    _host + endpoint);
                    break;

                case "DELETE":
                    response = await _client.DeleteAsync(
                                    _host + endpoint);
                    break;
            }

            var responseString = await response.Content.ReadAsStringAsync();

            return new JsonResult(JsonConvert.DeserializeObject<U>(responseString))
                { StatusCode = (int)response.StatusCode };
        }
        
        public async Task<HttpResponseMessage> CallAsync(string method, string endpoint, List<KeyValuePair<string, string>> headers)
        {
            _client.DefaultRequestHeaders.Clear();
            foreach (var header in headers)
                _client.DefaultRequestHeaders.Add(header.Key, header.Value);

            HttpResponseMessage response = new HttpResponseMessage();
            switch (method)
            {
                case "GET":
                    response = await _client.GetAsync(
                        _host + endpoint);
                    break;
            }

            return response;
        }
        
    }
}
