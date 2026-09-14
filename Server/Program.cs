using Microsoft.EntityFrameworkCore;
using Server.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Server.Infrastructure;
using Server.Interface;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace Server
{
	public class Program
	{
		/// <summary>
		/// Rate limiting policy applied to the credential endpoints.
		/// </summary>
		public const string AuthRateLimitPolicy = "auth";

		/// <summary>
		/// Permitted requests per minute, per client IP, per auth endpoint.
		/// </summary>
		private const int AuthRequestsPerMinute = 10;

		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// Add services to the container.

			builder.Services.AddControllers();
			// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen();

			// Configuration sources are layered: appsettings.json < appsettings.{Env}.json <
			// user secrets < environment variables. An environment variable named
			// ConnectionStrings__PostgreSQL therefore silently wins over appsettings.json,
			// which is easy to overlook - so log where we actually connect to.
			var connectionString = builder.Configuration.GetConnectionString("PostgreSQL");
			if (string.IsNullOrWhiteSpace(connectionString))
				throw new InvalidOperationException(
					"Missing configuration value: ConnectionStrings:PostgreSQL. " +
					"Set it in appsettings.json, via user secrets, or via the environment " +
					"variable ConnectionStrings__PostgreSQL.");

			Console.WriteLine($"[config] Database: {DescribeConnection(connectionString)}");

			builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
			builder.Services.AddScoped<IUserStore, DatabaseUserStore>();

			var jwtIssuer = builder.Configuration["Jwt:Issuer"];
			var jwtAudience = builder.Configuration["Jwt:Audience"];
			var jwtKey = builder.Configuration["Jwt:Key"];
			if (string.IsNullOrWhiteSpace(jwtKey))
				throw new InvalidOperationException("Missing configuration value: Jwt:Key");

			builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
			{
				options.TokenValidationParameters = new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidateAudience = true,
					ValidateLifetime = true,
					ValidateIssuerSigningKey = true,
					ValidIssuer = jwtIssuer,
					ValidAudience = jwtAudience,
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!)),
					ClockSkew = TimeSpan.FromSeconds(30)
				};
			});

			// Throttle credential endpoints. PBKDF2 protects stored hashes, but nothing
			// stopped online guessing before this.
			// Budgets are per client IP and per endpoint, so a burst of registrations
			// cannot lock a user out of logging in. This caps guessing rates; it is not
			// a substitute for per-account lockout, which is still missing.
			builder.Services.AddRateLimiter(options =>
			{
				options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

				options.AddPolicy(AuthRateLimitPolicy, context =>
					RateLimitPartition.GetFixedWindowLimiter(
						partitionKey: $"{context.Connection.RemoteIpAddress}|{context.Request.Path}",
						factory: _ => new FixedWindowRateLimiterOptions
						{
							PermitLimit = AuthRequestsPerMinute,
							Window = TimeSpan.FromMinutes(1),
							QueueLimit = 0
						}));

				// Without Retry-After a client cannot tell how long to back off.
				options.OnRejected = (context, _) =>
				{
					context.HttpContext.Response.Headers.RetryAfter = "60";
					return ValueTask.CompletedTask;
				};
			});

			builder.Services.AddSingleton<JwtTokenService>();//JWTToken erzeugen
			builder.Services.AddSingleton<IChatStore>(sp =>
								new JsonChatStore(
								Path.Combine(builder.Environment.ContentRootPath, "Data", "conversations.json"),
								Path.Combine(builder.Environment.ContentRootPath, "Data", "messages.json")));

			var app = builder.Build();

			// Apply pending EF migrations so a fresh checkout works without manual steps.
			// Disabled by default in Production, where schema changes should be deliberate.
			var autoMigrate = builder.Configuration.GetValue<bool?>("Database:AutoMigrate")
							  ?? !app.Environment.IsProduction();
			if (autoMigrate)
				ApplyMigrations(app);

			// Configure the HTTP request pipeline.
			if (app.Environment.IsDevelopment())
			{
				// Swagger and the developer exception page expose route maps and full
				// stack traces (including the connection string) - development only.
				app.UseSwagger();
				app.UseSwaggerUI();
			}
			else
			{
				app.UseExceptionHandler("/error");
				app.UseHsts();
				app.UseHttpsRedirection();
			}

			app.UseRateLimiter();

			app.UseAuthentication();
			app.UseAuthorization();

			app.Map("/error", () => Results.Problem(
				title: "An unexpected error occurred.",
				statusCode: StatusCodes.Status500InternalServerError));

			app.MapGet("/", () => Results.Ok("OK"));
			app.MapGet("/health", () => Results.Ok("Healthy"));

			app.MapControllers();

			app.Run();
		}

		/// <summary>
		/// Applies pending EF Core migrations, failing fast with an actionable message
		/// when the database cannot be reached.
		/// </summary>
		private static void ApplyMigrations(WebApplication app)
		{
			using var scope = app.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

			try
			{
				var pending = db.Database.GetPendingMigrations().ToList();
				if (pending.Count > 0)
				{
					Console.WriteLine($"[config] Applying {pending.Count} pending migration(s): {string.Join(", ", pending)}");
					db.Database.Migrate();
				}
				Console.WriteLine("[config] Database schema is up to date.");
			}
			catch (Exception ex)
			{
				// Without this the failure only surfaces later as a HTTP 500 on every
				// request, which hides the real cause.
				throw new InvalidOperationException(
					$"Cannot reach the database ({DescribeConnection(db.Database.GetConnectionString())}). " +
					"Check that PostgreSQL is running and that ConnectionStrings:PostgreSQL is correct - " +
					"note that an environment variable named ConnectionStrings__PostgreSQL overrides appsettings.json. " +
					$"Underlying error: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Renders a connection string for logging with the password removed.
		/// </summary>
		private static string DescribeConnection(string? connectionString)
		{
			if (string.IsNullOrWhiteSpace(connectionString))
				return "<not configured>";

			var parts = connectionString
				.Split(';', StringSplitOptions.RemoveEmptyEntries)
				.Where(p => !p.TrimStart().StartsWith("Password", StringComparison.OrdinalIgnoreCase));

			return string.Join(";", parts);
		}
	}
}
