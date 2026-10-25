using Back_End.Data;
using Back_End.Filters;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;
using Back_End.Rules;
using Back_End.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MongoDB.Driver;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add<GameExceptionFilter>();
});
builder.Services.AddEndpointsApiExplorer();


builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LiarCard API",
        Version = "v1"
    });

    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Description = "Nhap JWT Access Token"
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});

builder.Services.Configure<MongoDbContext>(
    builder.Configuration.GetSection("MongoDbContext"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDbContext>>().Value;

    return new MongoClient(settings.ConnectionString);
});

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDbContext>>().Value;

    var client = sp.GetRequiredService<IMongoClient>();

    return client.GetDatabase(settings.DatabaseName);
});
builder.Services.AddSingleton<IMongoCollection<RevokedAccessToken>>(sp =>
{
    var database = sp.GetRequiredService<IMongoDatabase>();

    return database.GetCollection<RevokedAccessToken>(
        "revoked_access_tokens");
});
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddSingleton<TableService>();

// Liars Card Gameplay Engine & Services
builder.Services.AddSingleton<IMatchStore, InMemoryMatchStore>();
builder.Services.AddSingleton<DeckRuleEngine>();
builder.Services.AddSingleton<TurnStateMachine>();
builder.Services.AddScoped<DeckRoundService>();
builder.Services.AddScoped<PenaltyService>();
builder.Services.AddScoped<DeckActionService>();
builder.Services.AddScoped<ChallengeService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<MatchQueryService>();


var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new Exception("JWT Key chưa được cấu hình");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new Exception("JWT Issuer chưa được cấu hình");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new Exception("JWT Audience chưa được cấu hình");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            ),

            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var jti = context.Principal?.FindFirst("jti")?.Value;

                if (string.IsNullOrWhiteSpace(jti))
                {
                    context.Fail("Access Token thiếu jti.");
                    return;
                }

                var collection = context.HttpContext.RequestServices
                    .GetRequiredService<IMongoCollection<RevokedAccessToken>>();

                var now = DateTime.UtcNow;

                var revoked = await collection
                    .Find(t => t.Jti == jti && t.ExpiresAt > now)
                    .AnyAsync();

                if (revoked)
                {
                    context.Fail("Access Token đã bị thu hồi.");
                }
            }
        };

    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<PlayerConnectionService>();
builder.Services.AddHostedService<LobbyNetworkService>();
builder.Services.AddSingleton<ConnectionTicketService>();
builder.Services.AddSingleton<PlayerPositionService>();

var app = builder.Build();

var revokedTokens = app.Services
    .GetRequiredService<IMongoCollection<RevokedAccessToken>>();

await revokedTokens.Indexes.CreateOneAsync(
    new CreateIndexModel<RevokedAccessToken>(
        Builders<RevokedAccessToken>.IndexKeys.Ascending(t => t.Jti),
        new CreateIndexOptions { Unique = true }));

//Only a account an Email
var database = app.Services.GetRequiredService<IMongoDatabase>();

var users = database.GetCollection<User>("Users");

await users.Indexes.CreateOneAsync(
    new CreateIndexModel<User>(
        Builders<User>.IndexKeys.Ascending(u => u.Email),
        new CreateIndexOptions { Unique = true }));


var characterSelections =
    database.GetCollection<CharacterSelection>(
        "CharacterSelections");

await characterSelections.Indexes.CreateOneAsync(
    new CreateIndexModel<CharacterSelection>(
        Builders<CharacterSelection>.IndexKeys
            .Ascending(s => s.UserId),
        new CreateIndexOptions { Unique = true }));


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "LiarCard API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
