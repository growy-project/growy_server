using growy_server.Data;
using growy_server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);



builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000") // Allow frontend URL
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// DbContext for SQL Server (used by SqlServerStatisticsService)
builder.Services.AddDbContext<GrowyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MssqlConnection")));

// Add services to the container.
//builder.Services.AddTransient<IStatisticsService, StatisticsService>();            // PostgreSQL + Npgsql
builder.Services.AddTransient<IStatisticsService, SqlServerStatisticsService>();  // SQL Server + EF Core
builder.Services.AddTransient<IStatisticsJobService, StatisticsJobService>();
builder.Services.AddTransient<IUserService, UserService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowLocalhost");


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
