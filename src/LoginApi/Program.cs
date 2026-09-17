using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(NpgsqlDataSource.Create(
    builder.Configuration.GetConnectionString("Default")!));
builder.Services.AddHttpClient();
builder.Services.AddCors(options => options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors("AllowAll");
app.UseRouting();
app.MapPost("/auth/register", async (RegisterRequest req, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
    try
    {
        await conn.ExecuteAsync(
            "INSERT INTO auth.users (email, password_hash) VALUES (@Email, @Hash)",
            new { req.Email, Hash = hash });
    }
    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
    {
        return Results.Conflict("Email already registered");
    }
    return Results.Ok();
});

app.MapPost("/auth/login", async (LoginRequest req, NpgsqlDataSource db, IConfiguration config) =>
{
    await using var conn = await db.OpenConnectionAsync();
    var user = await conn.QuerySingleOrDefaultAsync<UserRow>(
        "SELECT id AS Id, password_hash AS PasswordHash FROM auth.users WHERE email = @Email",
        new { req.Email });

    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
    var token = new JwtSecurityToken(
    issuer: config["Jwt:Issuer"],
    audience: config["Jwt:Audience"],
    claims: [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())],
    expires: DateTime.UtcNow.AddMinutes(15),
    signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

    return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token) });
});

app.Run();

record LoginRequest(string Email, string Password);
record RegisterRequest(string Email, string Password);
record UserRow(long Id, string PasswordHash);