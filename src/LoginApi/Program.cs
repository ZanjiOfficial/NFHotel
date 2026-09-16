using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(NpgsqlDataSource.Create(
    builder.Configuration.GetConnectionString("Default")!));

var app = builder.Build();

app.MapPost("/auth/login", async (LoginRequest req, NpgsqlDataSource db, IConfiguration config) =>
{
    await using var conn = await db.OpenConnectionAsync();
    var user = await conn.QuerySingleOrDefaultAsync<UserRow>(
        "SELECT id AS Id, password_hash AS PasswordHash FROM users WHERE email = @Email",
        new { req.Email });

    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
    var token = new JwtSecurityToken(
        claims: [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())],
        expires: DateTime.UtcNow.AddMinutes(15),
        signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

    return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token) });
});

app.Run();

record LoginRequest(string Email, string Password);
record UserRow(Guid Id, string PasswordHash);