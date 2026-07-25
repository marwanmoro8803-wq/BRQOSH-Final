using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

var json = File.ReadAllText(Path.Combine(
    Brqosh.Infrastructure.Persistence.BrqoshDataRoot.Path(), "database.json"));
var cs = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("ConnectionString").GetString()!;

Console.WriteLine("=== BRQOSH DB Startup Probe ===");
Console.WriteLine($"Connection: {cs.Split(';').FirstOrDefault(s => s.StartsWith("Host"))}");

await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, cs);

Console.WriteLine("\n--- Step 1: DatabaseInitializer ---");
var company = await new DatabaseInitializer(db).InitializeAsync("marwan", "123456789012345", BusinessProfile.Manufacturing);
Console.WriteLine($"Company: {company.NameAr} (Id={company.Id})");

Console.WriteLine("\n--- Step 2: BrqoshSchemaUpdater ---");
await new BrqoshSchemaUpdater(db).ApplyAsync();
Console.WriteLine("Schema patches applied.");

Console.WriteLine("\n--- Step 3: Admin user for probe ---");
const string probeUser = "probeadmin";
const string probePass = "ProbeAdmin9!";
await new UserAuthService(db).CreateInitialAdminAsync(company.Id, probeUser, probePass);
var admin = await new UserAuthService(db).AuthenticateAsync(company.Id, probeUser, probePass);
Console.WriteLine(admin is null ? "FAIL: probe admin login" : $"OK: {probeUser} authenticated ({admin.DisplayNameAr})");

var tables = await db.Database.SqlQuery<string>(
    $"""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema='public' ORDER BY table_name""")
    .ToListAsync();
Console.WriteLine($"\nTables: {tables.Count}");
Console.WriteLine("\n=== Startup probe SUCCESS ===");
