using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using APIViewModel.Member;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

namespace SportsCenterManagement.Tests;

public class Sprint3LiveE2ETests
{
    private const string LiveApiBaseUrl = "http://localhost:5198";
    private const string SqlServerConnectionString =
        "Server=.\\SQLEXPRESS;Database=SportsCenterManagementSprint2;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string SigningKey = "SportsCenterDevKey_32chars_Min!X";
    private const string SeededReceptionistId = "acc-rec-001";

    [Fact(Skip = "Live destructive smoke test; run manually against an isolated database and matching API build.")]
    public async Task LiveApi_CounterRegistration_CreatesAccountWithoutPackageOrder()
    {
        using HttpClient client = new() { BaseAddress = new Uri(LiveApiBaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken(SeededReceptionistId, "makitonati@gmail.com", "Receptionist"));
        string suffix = Guid.NewGuid().ToString("N")[..8];

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Member/counter-registration", new
        {
            fullName = $"Live counter member {suffix}",
            email = $"live.counter.{suffix}@sportscenter.local",
            phone = "09" + Random.Shared.Next(10000000, 99999999),
            dateOfBirth = "1998-08-18"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        CounterCreateMemberResponseAPIViewModel? data =
            await response.Content.ReadFromJsonAsync<CounterCreateMemberResponseAPIViewModel>();
        Assert.NotNull(data);

        await using SqlConnection connection = new(SqlServerConnectionString);
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT_BIG(*)
            FROM Member m
            LEFT JOIN MemberSubscription s ON s.MemberId = m.AccountId
            LEFT JOIN MembershipInvoice i ON i.MemberId = m.AccountId
            WHERE m.AccountId = @memberId AND s.Id IS NULL AND i.Id IS NULL
            """;
        command.Parameters.AddWithValue("@memberId", data.Member.AccountId);
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    private static string GenerateToken(string accountId, string email, string role)
    {
        Claim[] claims =
        {
            new(ClaimTypes.NameIdentifier, accountId),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "SportsCenterManagement",
            audience: "SportsCenterManagement",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials));
    }
}
