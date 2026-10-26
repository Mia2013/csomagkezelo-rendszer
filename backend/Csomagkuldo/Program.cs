using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Csomagkuldo.Data;
using Csomagkuldo.Models;
using Csomagkuldo.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
    builder.Configuration.AddEnvironmentVariables();
}

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Hiányzik a DefaultConnection adatbázis-beállítás.");
string jwtKey = builder.Configuration["Jwt:Key"] ?? string.Empty;
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Állíts be legalább 32 bájtos Jwt:Key értéket! Lásd a README-t.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<PackageService>();
builder.Services.AddIdentityCore<User>(options => options.User.RequireUniqueEmail = true)
    .AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddScoped<PasswordHasher<User>>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        NameClaimType = "id",
        RoleClaimType = "role"
    };
});
builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // A közös DTO-ból csak a kitöltött mezőket küldjük vissza.
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var sqlError = error?.GetBaseException() as SqlException;
        // 1205: holtpont; 2601/2627: ismétlődő egyedi adat.
        bool conflict = error is DbUpdateConcurrencyException ||
            (sqlError != null && (sqlError.Number == 1205 || sqlError.Number == 2601 || sqlError.Number == 2627));

        int statusCode = conflict ? StatusCodes.Status409Conflict : StatusCodes.Status500InternalServerError;
        string message = conflict
            ? "Az adatok közben megváltoztak, vagy egy megadott azonosító már foglalt. Frissítsd a listát és próbáld újra!"
            : "A művelet nem sikerült. Próbáld újra később!";

        await Results.Problem(
            statusCode: statusCode,
            title: conflict ? "Ütköző módosítás." : "Váratlan szerverhiba.",
            detail: message).ExecuteAsync(context);
    });
});
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// Az integrációs tesztek innen indítják az alkalmazást.
public partial class Program { }
