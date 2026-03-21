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

var provider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
//Here SqlServer or Postgres
if (provider == "Postgres")
{
    builder.Services.AddDbContext<GrowyDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddScoped<IStatisticsService, StatisticsService>();
    builder.Services.AddScoped<ISymbolService, PostgresSymbolService>();
}
else
{
    builder.Services.AddDbContext<GrowyDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("MssqlConnection")));
    builder.Services.AddScoped<IStatisticsService, SqlServerStatisticsService>();
    builder.Services.AddScoped<ISymbolService, SqlServerSymbolService>();
}

builder.Services.AddSingleton<IStatisticsJobService, StatisticsJobService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddTransient<IEmailService, EmailService>();


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
