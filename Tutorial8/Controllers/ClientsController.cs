using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace TravelAgencyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ClientsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("{id}/trips")]
    public IActionResult GetClientTrips(int id)
    {
        var trips = new List<object>();
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        using (var connection = new SqlConnection(connectionString))
        using (var command = new SqlCommand(@"
            SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, 
                   ct.RegisteredAt, ct.PaymentDate
            FROM Client_Trip ct
            INNER JOIN Trip t ON ct.IdTrip = t.IdTrip
            WHERE ct.IdClient = @IdClient
        ", connection))
        {
            command.Parameters.AddWithValue("@IdClient", id);
            connection.Open();
            var reader = command.ExecuteReader();

            if (!reader.HasRows)
                return NotFound($"No trips found for client with ID {id}");

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
                    RegisteredAt = reader["RegisteredAt"],
                    PaymentDate = reader["PaymentDate"] == DBNull.Value ? null : reader["PaymentDate"]
                });
            }
        }

        return Ok(trips);
    }

    [HttpPost]
    public IActionResult CreateClient([FromBody] ClientDto client)
    {
        if (string.IsNullOrEmpty(client.FirstName) || string.IsNullOrEmpty(client.LastName) ||
            string.IsNullOrEmpty(client.Email))
        {
            return BadRequest("FirstName, LastName, and Email are required.");
        }

        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        int newClientId;

        using (var connection = new SqlConnection(connectionString))
        using (var command = new SqlCommand(@"
            INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
            VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel);
            SELECT SCOPE_IDENTITY();
        ", connection))
        {
            command.Parameters.AddWithValue("@FirstName", client.FirstName);
            command.Parameters.AddWithValue("@LastName", client.LastName);
            command.Parameters.AddWithValue("@Email", client.Email);
            command.Parameters.AddWithValue("@Telephone", (object?)client.Telephone ?? DBNull.Value);
            command.Parameters.AddWithValue("@Pesel", (object?)client.Pesel ?? DBNull.Value);

            connection.Open();
            newClientId = Convert.ToInt32(command.ExecuteScalar());
        }

        return CreatedAtAction(nameof(GetClientTrips), new { id = newClientId }, new { IdClient = newClientId });
    }

    [HttpPut("{id}/trips/{tripId}")]
    public IActionResult RegisterClientToTrip(int id, int tripId)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();

            using (var checkClientCmd = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @IdClient", connection))
            {
                checkClientCmd.Parameters.AddWithValue("@IdClient", id);
                if (checkClientCmd.ExecuteScalar() == null)
                    return NotFound("Client not found.");
            }

            int maxPeople;
            using (var checkTripCmd = new SqlCommand("SELECT MaxPeople FROM Trip WHERE IdTrip = @IdTrip", connection))
            {
                checkTripCmd.Parameters.AddWithValue("@IdTrip", tripId);
                var result = checkTripCmd.ExecuteScalar();
                if (result == null)
                    return NotFound("Trip not found.");

                maxPeople = (int)result;
            }

            using (var countCmd = new SqlCommand("SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @IdTrip", connection))
            {
                countCmd.Parameters.AddWithValue("@IdTrip", tripId);
                int currentCount = (int)countCmd.ExecuteScalar();

                if (currentCount >= maxPeople)
                    return BadRequest("Trip is full.");
            }

            using (var insertCmd = new SqlCommand(@"
                INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt)
                VALUES (@IdClient, @IdTrip, @RegisteredAt)
            ", connection))
            {
                insertCmd.Parameters.AddWithValue("@IdClient", id);
                insertCmd.Parameters.AddWithValue("@IdTrip", tripId);
                insertCmd.Parameters.AddWithValue("@RegisteredAt", DateTime.Now);

                insertCmd.ExecuteNonQuery();
            }
        }

        return Ok("Client registered to trip.");
    }

    [HttpDelete("{id}/trips/{tripId}")]
    public IActionResult DeleteClientTrip(int id, int tripId)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        using (var connection = new SqlConnection(connectionString))
        using (var command = new SqlCommand(@"
            DELETE FROM Client_Trip
            WHERE IdClient = @IdClient AND IdTrip = @IdTrip
        ", connection))
        {
            command.Parameters.AddWithValue("@IdClient", id);
            command.Parameters.AddWithValue("@IdTrip", tripId);

            connection.Open();
            int rowsAffected = command.ExecuteNonQuery();

            if (rowsAffected == 0)
                return NotFound("Registration not found.");

            return Ok("Registration deleted.");
        }
    }

    public class ClientDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Telephone { get; set; }
        public string Pesel { get; set; }
    }
}