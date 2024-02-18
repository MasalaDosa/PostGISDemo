using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;
using System.Diagnostics;

namespace PostgisExperiment
{
   // Entity model
    public class City
    {
        public int Id { get; set; }
        public required string Name { get; set; } = string.Empty;

        // Represents longitude and latitude in that order (X, Y)
        public required Point Location { get; set; }
    }

    public class CityWithDistance : City
    {
        public double DistanceInMetres { get; set; }
    }
 
    public class CitiesDbContext : DbContext
    {
        private const string ConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=secret";

        public DbSet<City> Cities { get; set; }
 
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
            dataSourceBuilder.UseNetTopologySuite();
            var dataSource = dataSourceBuilder.Build();
            optionsBuilder.UseNpgsql(dataSource, o => o.UseNetTopologySuite());
        }
 
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Ensure postgis enabled for this db
            modelBuilder.HasPostgresExtension("postgis");
 
            // Ensure EF Core knows how to handle the Point type from NetTopologySuite
            modelBuilder.Entity<City>()
                .Property(e => e.Location)
                // Use geography not geometry
                // From npgsql.org - Once you do this, your column will be created as geography, and spatial operations will behave as expected.
                .HasColumnType("geography(Point)");

            modelBuilder.Entity<City>()
                .HasIndex(p => p.Location)
                .HasMethod("GIST");
        }
 
        public void SeedData()
        {
            if (!Cities.Any())
            {
                // Sample locations in the UK - Note longitude (X) comes before latitude (Y)
                // SRID is the Spatial Reference system used - not convinced it's neccessary here
                // SRID 4326 is for WGS 84
                var sampleLocations = new List<City>
                {
                    new() { Name = "Bristol", Location = new Point(-2.5879, 51.4545) },
                    new() { Name = "Bath", Location = new Point(-2.3590, 51.3758) },
                    new() { Name = "Exeter", Location = new Point(-3.5339, 50.7184) },
                    new() { Name = "Cardiff", Location = new Point(-3.1746, 51.4816) },
                    new() { Name = "Edinburgh", Location = new Point(-3.1883, 55.9533) },
                    new() { Name = "Leeds", Location = new Point(-1.5491, 53.8008) },
                };
 
                Cities.AddRange(sampleLocations);
                SaveChanges(); 
            }
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            using (var dbContext = new CitiesDbContext())
            {
                // Ensure the database is created and migrated
                dbContext.Database.Migrate();
 
                // Populate the database with sample data
                dbContext.SeedData();
 
                // Given radius for searching in meters
                double searchRadius = 600_000;
 
                // Sample search point
                var londonPoint = new Point(-0.1276,  51.5074);
 
                // Query cities ordered by distance from the given point
                 var nearbyCities = dbContext.Cities
                .Where(p => p.Location.Distance(londonPoint) <= searchRadius)
                .OrderBy(p => p.Location.Distance(londonPoint))
                .Select(p => new CityWithDistance { Id = p.Id, Name = p.Name, Location = p.Location, DistanceInMetres = p.Location.Distance(londonPoint) })
                .ToList();

                // Output the nearby cities
                foreach (var city in nearbyCities)
                {
                    Console.WriteLine($"City ID: {city.Id}, Name: {city.Name} Distance: {city.DistanceInMetres / 1000:0.00} KM ");
                }                
            }
        }
    }
}
 