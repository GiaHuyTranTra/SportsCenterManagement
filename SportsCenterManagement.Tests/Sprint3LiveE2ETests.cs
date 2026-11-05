using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using APIViewModel.MemberSubscription;
using APIViewModel.MembershipInvoice;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace SportsCenterManagement.Tests;

public class Sprint3LiveE2ETests
{
    private const string LiveApiBaseUrl = "http://localhost:5198";
    private const string SqlServerConnectionString =
        "Server=.\\SQLEXPRESS;Database=SportsCenterManagementSprint2;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string SigningKey = "SportsCenterDevKey_32chars_Min!X";

    private const string SeededReceptionistId = "acc-rec-001";
    private const int SeededTrialPackageId = 1;
    private const decimal SeededTrialPackagePrice = 350000m;

    private static string GenerateToken(string accountId, string email, string role)
    {
        List<Claim> claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, accountId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken jwt = new JwtSecurityToken(
            issuer: "SportsCenterManagement",
            audience: "SportsCenterManagement",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    [Fact]
    public async Task LiveApi_UC33_CounterRegistration_And_UC35_InvoiceSearchAndReceipt_WorksWithRealDatabase()
    {
        using HttpClient client = new HttpClient { BaseAddress = new Uri(LiveApiBaseUrl) };
        // Identifiers come from the seeded dataset
        // (Database/2026-11-06-titan-arena-seed.sql); the previous hard-coded
        // GUID belonged to a throwaway test account that no longer exists.
        string receptionistToken = GenerateToken(
            SeededReceptionistId,
            "makitonati@gmail.com",
            "Receptionist");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", receptionistToken);

        // =========================================================================
        // STEP 1: UC33 - Counter Member Registration (Live API -> Real SQL Server)
        // =========================================================================
        string randomSuffix = Guid.NewGuid().ToString("N")[..8];
        string testEmail = $"live.uc33.{randomSuffix}@sportscenter.local";
        string testPhone = "09" + Random.Shared.Next(10000000, 99999999);

        var counterRegPayload = new
        {
            fullName = $"Live UC33 Member {randomSuffix}",
            email = testEmail,
            phone = testPhone,
            dateOfBirth = "1998-08-18",
            packageId = SeededTrialPackageId,
            expectedPrice = SeededTrialPackagePrice,
            paymentMethod = "CASH"
        };

        HttpResponseMessage regResponse = await client.PostAsJsonAsync("/api/Member/counter-registration", counterRegPayload);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        CounterRegisterMemberResponseAPIViewModel? regData =
            await regResponse.Content.ReadFromJsonAsync<CounterRegisterMemberResponseAPIViewModel>();

        Assert.NotNull(regData);
        Assert.NotNull(regData.Member);
        Assert.NotNull(regData.Receipt);

        string memberId = regData.Member.AccountId;
        string memberCode = regData.Member.MemberCode;
        int invoiceId = regData.Receipt.InvoiceId;
        string invoiceNumber = regData.Receipt.InvoiceNumber;

        Assert.Equal("PENDING_PAYMENT", regData.Receipt.InvoiceStatus);
        Assert.Equal("PENDING_PAYMENT", regData.Receipt.SubscriptionStatus);
        Assert.Equal(350000m, regData.Receipt.Amount);

        // =========================================================================
        // STEP 2: VERIFY UC33 IN REAL SQL SERVER DATABASE (NO MOCK DB)
        // =========================================================================
        await using (SqlConnection sqlConn = new SqlConnection(SqlServerConnectionString))
        {
            await sqlConn.OpenAsync();
            await using SqlCommand cmd = sqlConn.CreateCommand();
            cmd.CommandText = @"
                SELECT i.InvoiceNumber, i.Amount, i.Status AS InvoiceStatus, 
                       s.Status AS SubStatus, m.FullName, m.MemberCode
                FROM MembershipInvoice i
                INNER JOIN MemberSubscription s ON i.SubscriptionId = s.Id
                INNER JOIN Member m ON i.MemberId = m.AccountId
                WHERE i.Id = @id";
            cmd.Parameters.AddWithValue("@id", invoiceId);

            await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Invoice and Member must exist in SQL Server database.");
            Assert.Equal(invoiceNumber, reader["InvoiceNumber"].ToString());
            Assert.Equal("PENDING_PAYMENT", reader["InvoiceStatus"].ToString());
            Assert.Equal("PENDING_PAYMENT", reader["SubStatus"].ToString());
            Assert.Equal(memberCode, reader["MemberCode"].ToString());
        }

        // =========================================================================
        // STEP 3: UC35 - Search Invoices (Live API -> Real SQL Server)
        // =========================================================================
        HttpResponseMessage searchResponse = await client.GetAsync(
            $"/api/MembershipInvoice/search?search={invoiceNumber}&status=PENDING_PAYMENT&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        PagedMembershipInvoiceAPIViewModel? pagedResult =
            await searchResponse.Content.ReadFromJsonAsync<PagedMembershipInvoiceAPIViewModel>();

        Assert.NotNull(pagedResult);
        Assert.True(pagedResult.TotalItems >= 1);
        Assert.Contains(pagedResult.Items, item => item.InvoiceNumber == invoiceNumber && item.InvoiceStatus == "PENDING_PAYMENT");

        // =========================================================================
        // STEP 4: UC35 - Get Receipt Details Before Payment (Must fail with RECEIPT_NOT_AVAILABLE)
        // =========================================================================
        HttpResponseMessage pendingReceiptResponse = await client.GetAsync($"/api/MembershipInvoice/{invoiceId}/receipt");
        Assert.Equal(HttpStatusCode.BadRequest, pendingReceiptResponse.StatusCode);

        // =========================================================================
        // STEP 5: UC34 - Pay Cash Invoice (Atomic PAID + CONFIRMED in Real SQL Server)
        // =========================================================================
        var payPayload = new { paymentMethod = "CASH" };
        HttpResponseMessage payResponse = await client.PostAsJsonAsync($"/api/MembershipInvoice/{invoiceId}/pay", payPayload);
        Assert.Equal(HttpStatusCode.OK, payResponse.StatusCode);

        MembershipReceiptAPIViewModel? paidReceipt =
            await payResponse.Content.ReadFromJsonAsync<MembershipReceiptAPIViewModel>();
        Assert.NotNull(paidReceipt);
        Assert.Equal("PAID", paidReceipt.InvoiceStatus);
        Assert.Equal("CONFIRMED", paidReceipt.SubscriptionStatus);
        Assert.Equal(invoiceNumber, paidReceipt.InvoiceNumber);
        Assert.Equal(350000m, paidReceipt.Amount);

        // =========================================================================
        // STEP 6: UC35 - Get Receipt Details After Payment (Live API -> Real SQL Server)
        // =========================================================================
        HttpResponseMessage paidReceiptResponse = await client.GetAsync($"/api/MembershipInvoice/{invoiceId}/receipt");
        Assert.Equal(HttpStatusCode.OK, paidReceiptResponse.StatusCode);

        MembershipReceiptAPIViewModel? receiptData =
            await paidReceiptResponse.Content.ReadFromJsonAsync<MembershipReceiptAPIViewModel>();

        Assert.NotNull(receiptData);
        Assert.Equal(invoiceNumber, receiptData.InvoiceNumber);
        Assert.Equal(SeededTrialPackagePrice, receiptData.Amount);
        Assert.Equal(SeededTrialPackageId, receiptData.PackageId);
        Assert.Equal(memberCode, receiptData.MemberCode);
        Assert.Equal("PAID", receiptData.InvoiceStatus);
        Assert.Equal("CONFIRMED", receiptData.SubscriptionStatus);

        // =========================================================================
        // STEP 7: Verify directly in SQL Server
        // =========================================================================
        await using (SqlConnection sqlConn = new SqlConnection(SqlServerConnectionString))
        {
            await sqlConn.OpenAsync();
            await using SqlCommand cmd = sqlConn.CreateCommand();
            cmd.CommandText = @"
                SELECT i.Status AS InvoiceStatus, s.Status AS SubStatus, i.PaidBy
                FROM MembershipInvoice i
                INNER JOIN MemberSubscription s ON i.SubscriptionId = s.Id
                WHERE i.Id = @id";
            cmd.Parameters.AddWithValue("@id", invoiceId);

            await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("PAID", reader["InvoiceStatus"].ToString());
            Assert.Equal("CONFIRMED", reader["SubStatus"].ToString());
            Assert.Equal(SeededReceptionistId, reader["PaidBy"].ToString());
        }
    }
}
