using EmojiEstimator.Web.Data;
using EmojiEstimator.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = ResolveSqliteConnectionString(
    builder.Configuration.GetConnectionString("EmojiEstimatorDatabase"),
    builder.Environment.ContentRootPath);

builder.Services.AddOptions<GitHubOptions>()
    .Bind(builder.Configuration.GetSection(GitHubOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Token), $"{GitHubOptions.SectionName}:Token is required.")
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), $"{GitHubOptions.SectionName}:BaseUrl must be an absolute URI.")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEmojiCounter, UnicodeEmojiCounter>();
builder.Services.AddSingleton<IEmDashCounter, CanonicalEmDashCounter>();
builder.Services.AddSingleton<IRepositoryScanBackgroundQueue, RepositoryScanBackgroundQueue>();
builder.Services.AddSingleton<IRepositoryScanProgressNotifier, ServerSentEventRepositoryScanProgressNotifier>();
builder.Services.AddSingleton<RepositoryScanCoordinator>();
builder.Services.AddSingleton<IRepositoryScanCoordinator>(serviceProvider => serviceProvider.GetRequiredService<RepositoryScanCoordinator>());
builder.Services.AddHostedService<RepositoryScanBackgroundService>();
builder.Services.AddDbContext<EmojiEstimatorDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IGitHubPullRequestPageSource, OctokitGitHubPullRequestPageSource>();
builder.Services.AddScoped<IGitHubIssuePageSource, OctokitGitHubIssuePageSource>();
builder.Services.AddScoped<IGitHubContentReader, GitHubContentReader>();
builder.Services.AddScoped<IRepositoryScanAggregator, RepositoryScanAggregator>();
builder.Services.AddScoped<IRepositoryScanner, RepositoryScanner>();
builder.Services.AddScoped<IRepositoryScanStore, RepositoryScanStore>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EmojiEstimatorDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

static string ResolveSqliteConnectionString(string? connectionString, string contentRootPath)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

    var sqliteConnectionStringBuilder = new SqliteConnectionStringBuilder(connectionString);
    ArgumentException.ThrowIfNullOrWhiteSpace(sqliteConnectionStringBuilder.DataSource);

    if (!Path.IsPathRooted(sqliteConnectionStringBuilder.DataSource))
    {
        sqliteConnectionStringBuilder.DataSource = Path.Combine(contentRootPath, sqliteConnectionStringBuilder.DataSource);
    }

    return sqliteConnectionStringBuilder.ToString();
}

public partial class Program
{
}
