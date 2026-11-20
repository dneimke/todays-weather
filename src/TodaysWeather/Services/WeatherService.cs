using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace TodaysWeather.Services;

public record City(string Name, string Country, string? Admin1, double Latitude, double Longitude)
{
    public string Region => string.Join(", ", new[] { Admin1, Country }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
}

public sealed class Forecast
{
    public int UtcOffsetSeconds { get; set; }
    public CurrentWeather Current { get; set; } = new();
    public HourlyWeather Hourly { get; set; } = new();
    public DailyWeather Daily { get; set; } = new();
}

public sealed class CurrentWeather
{
    public double Temperature_2m { get; set; }
    public double RelativeHumidity_2m { get; set; }
    public double ApparentTemperature { get; set; }
    public int IsDay { get; set; }
    public double Precipitation { get; set; }
    public int WeatherCode { get; set; }
    public double CloudCover { get; set; }
    public double PressureMsl { get; set; }
    public double WindSpeed_10m { get; set; }
    public double WindDirection_10m { get; set; }
    public double WindGusts_10m { get; set; }
}

public sealed class HourlyWeather
{
    public DateTime[] Time { get; set; } = [];
    public double[] Temperature_2m { get; set; } = [];
    public double[] ApparentTemperature { get; set; } = [];
    public double[] PrecipitationProbability { get; set; } = [];
    public double[] Precipitation { get; set; } = [];
    public int[] WeatherCode { get; set; } = [];
    public double[] WindSpeed_10m { get; set; } = [];
    public double[] UvIndex { get; set; } = [];
    public int[] IsDay { get; set; } = [];
}

public sealed class DailyWeather
{
    public int[] WeatherCode { get; set; } = [];
    public double[] Temperature_2mMax { get; set; } = [];
    public double[] Temperature_2mMin { get; set; } = [];
    public DateTime[] Sunrise { get; set; } = [];
    public DateTime[] Sunset { get; set; } = [];
    public double[] UvIndexMax { get; set; } = [];
    public double[] PrecipitationSum { get; set; } = [];
    public double[] PrecipitationProbabilityMax { get; set; } = [];
    public double[] WindSpeed_10mMax { get; set; } = [];
}

public sealed class AirQuality
{
    public AirCurrent? Current { get; set; }
}
public sealed class AirCurrent
{
    public double? UsAqi { get; set; }
    public double? Pm2_5 { get; set; }
}
public sealed class GeocodingResponse
{
    public City[]? Results { get; set; }
}
public record WeatherResult(Forecast Forecast, bool IsSample, AirCurrent? Air);

public static class WeatherMath
{
    public static double Temperature(double celsius, bool fahrenheit) => fahrenheit ? celsius * 9 / 5 + 32 : celsius;
    public static double Speed(double kmh, bool imperial) => imperial ? kmh * 0.621371 : kmh;
    public static double DaylightProgress(DateTime now, DateTime sunrise, DateTime sunset) =>
        sunset <= sunrise ? 0 : Math.Clamp((now - sunrise).TotalSeconds / (sunset - sunrise).TotalSeconds, 0, 1);
    public static string DaylightDuration(DateTime sunrise, DateTime sunset)
    {
        var duration = sunset > sunrise ? sunset - sunrise : TimeSpan.Zero;
        return $"{(int)duration.TotalHours}h {duration.Minutes:D2}m";
    }
    public static string Condition(int code) => code switch
    {
        0 => "Clear sky", 1 => "Mostly clear", 2 => "Partly cloudy", 3 => "Overcast",
        45 or 48 => "Foggy", 51 or 53 or 55 or 56 or 57 => "Drizzle",
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "Rain",
        71 or 73 or 75 or 77 or 85 or 86 => "Snow",
        95 or 96 or 99 => "Thunderstorm", _ => "Cloudy"
    };
    public static string Theme(int code, bool day) => code switch
    {
        95 or 96 or 99 => "storm",
        71 or 73 or 75 or 77 or 85 or 86 => "snow",
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "rain",
        45 or 48 => "fog",
        3 => "overcast",
        1 or 2 => day ? "partly" : "night",
        _ => day ? "clear" : "night"
    };
    public static string Icon(int code, bool day) => Theme(code, day) switch
    {
        "storm" => "⛈", "snow" => "❄", "rain" => "☂", "fog" => "≋",
        "overcast" => "☁", "partly" => "⛅", "night" => "☾", _ => "☀"
    };
    public static string WindDirection(double degrees) =>
        new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" }[(int)Math.Round(((degrees % 360 + 360) % 360) / 45, MidpointRounding.AwayFromZero) % 8];
}

public sealed class WeatherService(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static readonly City[] Cities =
    [
        new("Adelaide", "Australia", "South Australia", -34.9287, 138.5986),
        new("Tokyo", "Japan", "Tokyo", 35.6895, 139.6917),
        new("London", "United Kingdom", "England", 51.5085, -0.1257),
        new("New York", "United States", "New York", 40.7143, -74.006),
        new("Reykjavík", "Iceland", null, 64.1355, -21.8954)
    ];

    private static string Coordinates(City city) => $"latitude={city.Latitude.ToString(CultureInfo.InvariantCulture)}&longitude={city.Longitude.ToString(CultureInfo.InvariantCulture)}";

    public async Task<City[]> SearchAsync(string query, CancellationToken token)
    {
        if (query.Trim().Length < 2) return [];
        var response = await http.GetFromJsonAsync<GeocodingResponse>($"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query.Trim())}&count=6&language=en&format=json", JsonOptions, token);
        return response?.Results ?? [];
    }

    public async Task<WeatherResult> GetAsync(City city)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            var coordinates = Coordinates(city);
            var url = $"https://api.open-meteo.com/v1/forecast?{coordinates}&current=temperature_2m,relative_humidity_2m,apparent_temperature,is_day,precipitation,weather_code,cloud_cover,pressure_msl,wind_speed_10m,wind_direction_10m,wind_gusts_10m&hourly=temperature_2m,apparent_temperature,precipitation_probability,precipitation,weather_code,wind_speed_10m,uv_index,is_day&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset,uv_index_max,precipitation_sum,precipitation_probability_max,wind_speed_10m_max&timezone=auto&forecast_days=1";
            var forecast = await http.GetFromJsonAsync<Forecast>(url, JsonOptions, timeout.Token);
            if (!IsValid(forecast)) throw new JsonException("Incomplete weather response");
            AirCurrent? air = null;
            try
            {
                using var airTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                air = (await http.GetFromJsonAsync<AirQuality>($"https://air-quality-api.open-meteo.com/v1/air-quality?{coordinates}&current=us_aqi,pm2_5", JsonOptions, airTimeout.Token))?.Current;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException) { }
            return new WeatherResult(forecast!, false, air);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return new WeatherResult(Sample(), true, null);
        }
    }

    public static bool IsValid(Forecast? f) => f is not null && f.Current is not null && f.Hourly is not null && f.Daily is not null &&
        f.Hourly.Time is { Length: > 0 } && f.Hourly.Temperature_2m?.Length == f.Hourly.Time.Length &&
        f.Hourly.WeatherCode?.Length == f.Hourly.Time.Length && f.Hourly.PrecipitationProbability?.Length == f.Hourly.Time.Length &&
        f.Hourly.IsDay?.Length == f.Hourly.Time.Length && f.Hourly.ApparentTemperature?.Length == f.Hourly.Time.Length &&
        f.Daily.Sunrise is { Length: > 0 } && f.Daily.Sunset is { Length: > 0 } && f.Daily.Temperature_2mMax is { Length: > 0 } &&
        f.Daily.Temperature_2mMin is { Length: > 0 } && f.Daily.UvIndexMax is { Length: > 0 } &&
        f.Daily.PrecipitationSum is { Length: > 0 } && f.Daily.PrecipitationProbabilityMax is { Length: > 0 };

    public static Forecast Sample()
    {
        var now = DateTime.Now;
        var day = now.Date;
        return new Forecast
        {
            UtcOffsetSeconds = (int)TimeZoneInfo.Local.GetUtcOffset(now).TotalSeconds,
            Current = new() { Temperature_2m = 22, ApparentTemperature = 21, IsDay = now.Hour is >= 6 and < 19 ? 1 : 0, WeatherCode = 2, RelativeHumidity_2m = 64, CloudCover = 34, PressureMsl = 1016, WindSpeed_10m = 14, WindDirection_10m = 225, WindGusts_10m = 23 },
            Hourly = new() { Time = Enumerable.Range(0, 24).Select(h => day.AddHours(h)).ToArray(), Temperature_2m = Enumerable.Range(0, 24).Select(h => 18 + 5 * Math.Sin((h - 8) * Math.PI / 12)).ToArray(), ApparentTemperature = Enumerable.Repeat(21d, 24).ToArray(), PrecipitationProbability = Enumerable.Range(0, 24).Select(h => (double)(h % 4 * 5 + 5)).ToArray(), WeatherCode = Enumerable.Repeat(2, 24).ToArray(), IsDay = Enumerable.Range(0, 24).Select(h => h is >= 6 and < 19 ? 1 : 0).ToArray() },
            Daily = new() { Sunrise = [day.AddHours(6).AddMinutes(18)], Sunset = [day.AddHours(19).AddMinutes(42)], Temperature_2mMax = [25], Temperature_2mMin = [16], UvIndexMax = [6], PrecipitationSum = [0.4], PrecipitationProbabilityMax = [20] }
        };
    }
}
