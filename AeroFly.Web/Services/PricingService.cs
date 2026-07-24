using AeroFly.Web.Models;

namespace AeroFly.Web.Services;

public interface IPricingService
{
    double CalculateDistanceKm(Airport departureAirport, Airport arrivalAirport);
    decimal CalculateFinalPrice(decimal basePrice, Airport departureAirport, Airport arrivalAirport, decimal classMultiplier);
}

public class PricingService : IPricingService
{
    private const decimal PricePerKilometer = 0.10m;

    public double CalculateDistanceKm(Airport departureAirport, Airport arrivalAirport)
    {
        const double earthRadiusKm = 6371;

        var latitudeDifference = ToRadians(arrivalAirport.Latitude - departureAirport.Latitude);
        var longitudeDifference = ToRadians(arrivalAirport.Longitude - departureAirport.Longitude);
        var departureLatitude = ToRadians(departureAirport.Latitude);
        var arrivalLatitude = ToRadians(arrivalAirport.Latitude);

        var a = Math.Sin(latitudeDifference / 2) * Math.Sin(latitudeDifference / 2) +
                Math.Cos(departureLatitude) * Math.Cos(arrivalLatitude) *
                Math.Sin(longitudeDifference / 2) * Math.Sin(longitudeDifference / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    public decimal CalculateFinalPrice(
        decimal basePrice,
        Airport departureAirport,
        Airport arrivalAirport,
        decimal classMultiplier)
    {
        var distance = (decimal)CalculateDistanceKm(departureAirport, arrivalAirport);
        var economyPrice =  basePrice;
        return Math.Round(economyPrice * classMultiplier, 2, MidpointRounding.AwayFromZero);
    }

    private static double ToRadians(double value) => value * Math.PI / 180;
}
