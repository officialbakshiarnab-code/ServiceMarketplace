window.serviceMarketplaceRegistrationLocation = {
    getCurrentPosition: () => new Promise((resolve) => {
        if (!navigator.geolocation) {
            resolve({
                succeeded: false,
                error: "This browser does not support current location."
            });
            return;
        }

        navigator.geolocation.getCurrentPosition(
            position => resolve({
                succeeded: true,
                latitude: position.coords.latitude,
                longitude: position.coords.longitude,
                accuracyMeters: position.coords.accuracy
            }),
            error => resolve({
                succeeded: false,
                error: error.message || "Location permission was denied or unavailable."
            }),
            {
                enableHighAccuracy: false,
                timeout: 10000,
                maximumAge: 60000
            });
    })
};
