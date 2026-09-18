using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(NpgsqlDataSource.Create(
    builder.Configuration.GetConnectionString("Default")!));
builder.Services.AddHttpClient();
builder.Services.AddCors(options => options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        };
    });
    
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
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
        "SELECT id AS Id, password_hash AS PasswordHash, role AS Role FROM auth.users WHERE email = @Email",
        new { req.Email });

    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
    var token = new JwtSecurityToken(
    issuer: config["Jwt:Issuer"],
    audience: config["Jwt:Audience"],
    claims: [
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(ClaimTypes.Role, user.Role)
    ],
    expires: DateTime.UtcNow.AddMinutes(15),
    signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

    return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token) });
});

var admin = app.MapGroup("/admin/users").RequireAuthorization(p => p.RequireRole("admin"));

admin.MapGet("/", async (HttpContext http, NpgsqlDataSource db) =>
{
    var currentId = long.Parse(http.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    await using var conn = await db.OpenConnectionAsync();
    var users = await conn.QueryAsync<AdminUserRow>(
        "SELECT id AS Id, email AS Email, role AS Role FROM auth.users WHERE id <> @CurrentId ORDER BY email",
        new { CurrentId = currentId });
    return Results.Ok(users);
});

admin.MapPut("/{id:long}/email", async (long id, UpdateEmailRequest req, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    try
    {
        var rows = await conn.ExecuteAsync(
            "UPDATE auth.users SET email = @Email WHERE id = @Id",
            new { req.Email, Id = id });
        return rows == 0 ? Results.NotFound() : Results.Ok();
    }
    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
    {
        return Results.Conflict("Email already registered");
    }
});

admin.MapPut("/{id:long}/password", async (long id, UpdatePasswordRequest req, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
    var rows = await conn.ExecuteAsync(
        "UPDATE auth.users SET password_hash = @Hash WHERE id = @Id",
        new { Hash = hash, Id = id });
    return rows == 0 ? Results.NotFound() : Results.Ok();
});

admin.MapPut("/{id:long}/role", async (long id, UpdateRoleRequest req, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    try
    {
        var rows = await conn.ExecuteAsync(
            "UPDATE auth.users SET role = @Role WHERE id = @Id",
            new { req.Role, Id = id });
        return rows == 0 ? Results.NotFound() : Results.Ok();
    }
    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.CheckViolation)
    {
        return Results.BadRequest("Invalid role");
    }
});

admin.MapDelete("/{id:long}", async (long id, HttpContext http, NpgsqlDataSource db) =>
{
    var currentId = long.Parse(http.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    if (id == currentId)
        return Results.BadRequest("Cannot delete your own account");

    await using var conn = await db.OpenConnectionAsync();
    var rows = await conn.ExecuteAsync("DELETE FROM auth.users WHERE id = @Id", new { Id = id });
    return rows == 0 ? Results.NotFound() : Results.Ok();
});

app.Run();

record LoginRequest(string Email, string Password);
record RegisterRequest(string Email, string Password);
record UserRow(long Id, string PasswordHash, string Role);
record AdminUserRow(long Id, string Email, string Role);
record UpdateEmailRequest(string Email);
record UpdatePasswordRequest(string Password);
record UpdateRoleRequest(string Role);