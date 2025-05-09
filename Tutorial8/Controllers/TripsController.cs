using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;

namespace TravelAgencyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TripsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public TripsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult GetTrips()
    {
        var trips = new List<object>();
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        using (var connection = new SqlConnection(connectionString))
        using (var command = new SqlCommand(@"
            SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                   STRING_AGG(c.Name, ', ') AS Countries
            FROM Trip t
            LEFT JOIN Country_Trip ct ON t.IdTrip = ct.IdTrip
            LEFT JOIN Country c ON ct.IdCountry = c.IdCountry
            GROUP BY t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople
        ", connection))
        {
            connection.Open();
            var reader = command.ExecuteReader();
            while (reader.Read())
            {
                trips.Add(new
                {
                    IdTrip = reader["IdTrip"],
                    Name = reader["Name"],
                    Description = reader["Description"],
                    DateFrom = reader["DateFrom"],
                    DateTo = reader["DateTo"],
                    MaxPeople = reader["MaxPeople"],
                    Countries = reader["Countries"]
                });
            }
        }

        return Ok(trips);
    }
}