using System.Net;
using System.Text;
using TodaysWeather.Services;

namespace TodaysWeather.Tests;

public class WeatherTests
{
    [Theory]
    [InlineData(0, "Clear sky", "clear")]
    [InlineData(2, "Partly cloudy", "partly")]
    [InlineData(45, "Foggy", "fog")]
    [InlineData(63, "Rain", "rain")]
    [InlineData(75, "Snow", "snow")]
    [InlineData(95, "Thunderstorm", "storm")]
    public void MapsWeatherCodes(int code, string condition, string theme)
    {
        Assert.Equal(condition, WeatherMath.Condition(code));
        Assert.Equal(theme, WeatherMath.Theme(code, true));
    }

    [Fact]
    public void ClearNightUsesNightTheme() => Assert.Equal("night", WeatherMath.Theme(0, false));

    [Fact]
    public void ConvertsUnits()
    {
        Assert.Equal(32, WeatherMath.Temperature(0, true));
        Assert.Equal(20, WeatherMath.Temperature(20, false));
        Assert.Equal(62.1371, WeatherMath.Speed(100, true), 4);
        Assert.Equal(100, WeatherMath.Speed(100, false));
    }

    [Fact]
    public void CalculatesDaylightAndClampsProgress()
    {
        var sunrise = new DateTime(2026, 6, 1, 6, 0, 0);
        var sunset = sunrise.AddHours(12).AddMinutes(30);
        Assert.Equal("12h 30m", WeatherMath.DaylightDuration(sunrise, sunset));
        Assert.Equal(0, WeatherMath.DaylightProgress(sunrise.AddHours(-1), sunrise, sunset));
        Assert.Equal(0.5, WeatherMath.DaylightProgress(sunrise.AddMinutes(375), sunrise, sunset));
        Assert.Equal(1, WeatherMath.DaylightProgress(sunset.AddHours(1), sunrise, sunset));
        Assert.Equal(0, WeatherMath.DaylightProgress(sunrise, sunset, sunrise));
    }

    [Fact]
    public async Task ParsesForecastAndOptionalAirQuality()
    {
        const string forecast = """
        {"utc_offset_seconds":3600,"current":{"temperature_2m":18.5,"relative_humidity_2m":70,"apparent_temperature":17,"is_day":1,"weather_code":2,"wind_speed_10m":12,"wind_gusts_10m":18},"hourly":{"time":["2026-06-01T12:00"],"temperature_2m":[18.5],"apparent_temperature":[17],"precipitation_probability":[20],"weather_code":[2],"is_day":[1]},"daily":{"temperature_2m_max":[21],"temperature_2m_min":[12],"sunrise":["2026-06-01T05:00"],"sunset":["2026-06-01T21:00"],"uv_index_max":[5],"precipitation_sum":[0.5],"precipitation_probability_max":[20]}}
        """;
        var service = new WeatherService(new HttpClient(new StubHandler(request => request.RequestUri!.Host.StartsWith("air-quality") ? """{"current":{"us_aqi":42,"pm2_5":8.5}}""" : forecast)));
        var result = await service.GetAsync(WeatherService.Cities[0]);
        Assert.False(result.IsSample);
        Assert.Equal(18.5, result.Forecast.Current.Temperature_2m);
        Assert.Equal(70, result.Forecast.Current.RelativeHumidity_2m);
        Assert.Equal(21, result.Forecast.Daily.Temperature_2mMax[0]);
        Assert.Equal(42, result.Air!.UsAqi);
        Assert.Equal(8.5, result.Air.Pm2_5);
    }

    [Fact]
    public async Task FallsBackOnFailedOrIncompleteForecast()
    {
        foreach (var response in new[] { "{}", "not json", """{"hourly":{"time":null}}""" })
        {
            var service = new WeatherService(new HttpClient(new StubHandler(_ => response)));
            var result = await service.GetAsync(WeatherService.Cities[0]);
            Assert.True(result.IsSample);
            Assert.True(WeatherService.IsValid(result.Forecast));
            Assert.Equal(24, result.Forecast.Hourly.Time.Length);
        }
        var failed = new WeatherService(new HttpClient(new StubHandler(_ => throw new HttpRequestException())));
        Assert.True((await failed.GetAsync(WeatherService.Cities[0])).IsSample);
    }

    [Fact]
    public async Task SearchesCitiesAndEscapesQuery()
    {
        string? query = null;
        var service = new WeatherService(new HttpClient(new StubHandler(request => {
            query = request.RequestUri!.Query;
            return """{"results":[{"name":"London","country":"United Kingdom","admin1":"England","latitude":51.5,"longitude":-0.1}]}""";
        })));
        var cities = await service.SearchAsync("London &", CancellationToken.None);
        Assert.Contains("London%20%26", query);
        Assert.Equal("England, United Kingdom", cities[0].Region);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respond(request), Encoding.UTF8, "application/json") });
    }
}
