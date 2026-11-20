window.weatherStorage = {
    getCity: () => {
        try { return JSON.parse(localStorage.getItem("weather-city")); }
        catch { return null; }
    },
    setCity: city => { try { localStorage.setItem("weather-city", JSON.stringify(city)); } catch { } },
    getFahrenheit: () => { try { return localStorage.getItem("weather-fahrenheit") === "true"; } catch { return false; } },
    setFahrenheit: value => { try { localStorage.setItem("weather-fahrenheit", String(value)); } catch { } },
    showCurrentHour: () => {
        const container = document.querySelector(".hourly-scroll");
        const hour = container?.querySelector(".hour.focused");
        if (container && hour) container.scrollLeft = Math.max(0, hour.offsetLeft - container.offsetLeft - container.clientWidth / 3);
    },
    getLocation: () => new Promise((resolve, reject) => {
        if (!navigator.geolocation) { reject(new Error("Geolocation unavailable")); return; }
        navigator.geolocation.getCurrentPosition(
            position => resolve({ latitude: position.coords.latitude, longitude: position.coords.longitude }),
            reject,
            { timeout: 10000, maximumAge: 300000 }
        );
    })
};
