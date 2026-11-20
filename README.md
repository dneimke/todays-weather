# Today's Weather

A beautiful, responsive single-page view of today's weather, wherever you are.

## Features

- Live forecasts and city search from the keyless Open-Meteo APIs, with optional air quality.
- Location detection, quick city switching, and saved location and temperature units.
- Hourly temperatures, daylight, wind, UV, precipitation, and atmospheric details.
- Weather-responsive backgrounds and a sample forecast when live data is unavailable.

## Tech stack

Blazor WebAssembly on .NET 10, CSS, minimal JavaScript interop, and xUnit.

## Quick start

```sh
git clone https://github.com/dneimke/todays-weather.git
cd todays-weather
dotnet run --project src/TodaysWeather
dotnet test
```

## Project structure

- `src/TodaysWeather/` — Blazor app, weather service, and static assets.
- `tests/TodaysWeather.Tests/` — weather calculations and service tests.

## License

MIT — see [LICENSE](LICENSE).
