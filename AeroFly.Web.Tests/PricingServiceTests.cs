using AeroFly.Web.Models;
using AeroFly.Web.Services;

namespace AeroFly.Web.Tests;

public class PricingServiceTests
{
    private readonly PricingService _pricingService = new();

    [Fact]
    public void CalculateFinalPrice_BusinessIsDoubleEconomy()
    {
        var departure = CreateAirport(30.1219, 31.4056);
        var arrival = CreateAirport(25.2532, 55.3657);

        var economyPrice = _pricingService.CalculateFinalPrice(100, departure, arrival, 1.0m);
        var businessPrice = _pricingService.CalculateFinalPrice(100, departure, arrival, 2.0m);

        Assert.Equal(economyPrice * 2, businessPrice);
    }

    [Fact]
    public void CalculateFinalPrice_LongerDistanceProducesHigherPrice()
    {
        var cairo = CreateAirport(30.1219, 31.4056);
        var dubai = CreateAirport(25.2532, 55.3657);
        var newYork = CreateAirport(40.6413, -73.7781);

        var shortFlight = _pricingService.CalculateFinalPrice(100, cairo, dubai, 1.0m);
        var longFlight = _pricingService.CalculateFinalPrice(100, cairo, newYork, 1.0m);

        Assert.True(longFlight > shortFlight);
    }

    private static Airport CreateAirport(double latitude, double longitude)
    {
        return new Airport
        {
            Name = "Test Airport",
            City = "Test City",
            Country = "Test Country",
            IataCode = "TST",
            Latitude = latitude,
            Longitude = longitude
        };
    }
}
