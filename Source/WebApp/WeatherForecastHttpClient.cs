using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using WebApp.Pages;

namespace WebApp
{
    public class WeatherForecastHttpClient
    {
        private readonly HttpClient http;

        public WeatherForecastHttpClient(HttpClient http)
        {
            this.http = http;
        }

        public async Task<FetchData.WeatherForecast[]> GetForecastAsync()
        {
            var forecasts = Array.Empty<FetchData.WeatherForecast>();

            try
            {
                forecasts = await http.GetFromJsonAsync<FetchData.WeatherForecast[]>("WeatherForecast");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (Exception ex)
            {
                throw;
            }

            return forecasts;
        }
    }
}
