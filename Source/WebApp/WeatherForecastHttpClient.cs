using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using WebApp.Pages;

namespace WebApp
{
    public class WeatherForecastHttpClient
    {
        private readonly HttpClient _http;

        public WeatherForecastHttpClient(HttpClient http)
        {
            _http = http;
        }

        /// <summary>
        /// Just keeping this default implementation for reference.
        /// </summary>
        /// <returns></returns>
        public async Task<FetchData.WeatherForecast[]?> GetForecastAsync()
        {
            var forecasts = Array.Empty<FetchData.WeatherForecast>();

            try
            {
                forecasts = await _http.GetFromJsonAsync<FetchData.WeatherForecast[]>("WeatherForecast");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (Exception ex)
            {
                // Keeping this one here just to be able to set a breakpoint when debugging.
                throw;
            }

            return forecasts;
        }

        /// <summary>
        /// TODO: Not using this method for now.
        /// </summary>
        /// <returns></returns>
        public async Task GetFileDataAsync()
        {
            try
            {
                var stream = await _http.GetStreamAsync("fileData");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<string> GetStreamingFileData()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "filedata");
            request.SetBrowserResponseStreamingEnabled(true); // Enable response streaming

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();

            if (response.Content is object)
            {
                var resp = await response.Content.ReadAsStringAsync();
                return resp;

                // TODO: We can also deserialize the stream to an object list if we want.
                //var stream = await response.Content.ReadAsStreamAsync();
                //var data = await JsonSerializer.DeserializeAsync<List<Book>>(stream);
                // do something with the data or return it
            }

            return "";
        }
    }
}
