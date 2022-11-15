using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;

namespace BlazorWasmApp1.Pages;

public partial class FetchData
{
    [Inject]
    private HttpClient Http { get; set; } = null!;

    private WeatherForecast[]? forecasts;

    protected override async Task OnInitializedAsync()
    {
        await Task.Delay(1000);
        this.forecasts = await this.Http.GetFromJsonAsync<WeatherForecast[]>("sample-data/weather.json");
    }

    public class WeatherForecast
    {
        public DateTime Date { get; set; }

        public int TemperatureC { get; set; }

        public string? Summary { get; set; }

        public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
    }
}
