using example.client;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace example;
class Program
{
    const string environmentUrl = "https://api.accept.madaster.com";

    const string token = "-- replace me --";

    private static DatabaseClient databaseClient;

    private static CommodityClient commodityClient;

    static async Task Main(string[] args)
    {
        using var httpClient = new HttpClient() { BaseAddress = new Uri(environmentUrl) };
        httpClient.DefaultRequestHeaders.Add("X-API-Key", token);
        databaseClient = new DatabaseClient(httpClient);
        commodityClient = new CommodityClient(httpClient);

        await EnsureDatabaseExists("Lindner", "1234");
        await EnsureDatabaseExists("Mosa", "7850");


        var database = await databaseClient.GetDatabaseByExternalIdAsync("1234"); // Lindner DB
        await EnsureMaterialExists(database.Id);
        await EnsureAreaProductExists(database.Id);
    }

    /// <summary>
    /// Ensures a database with the specified externalId exists.
    /// </summary>
    static async Task EnsureDatabaseExists(string name, string externalId)
    {
        Database database;
        try
        {
            database = await databaseClient.GetDatabaseByExternalIdAsync(externalId);
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == 404)
            {
                database = await databaseClient.CreateDatabaseAsync(new Database()
                {
                    Names = new LocalizedString()
                    {
                        En = name,
                        De = name
                    },
                    ExternalId = externalId,
                    InitiallySelectedForEnrichment = true,
                    AvailableInCountries = new[] { "de" },
                    IsAvailableForEverybody = true
                });

                return;
            }

            throw;
        }
    }

    static async Task EnsureMaterialExists(Guid databaseId)
    {
        if (await CommodityExists(databaseId, "material-1234"))
        {
            return;
        }

        var material = new Material()
        {
            DatabaseId = databaseId,
            Names = new()
            {
                En = "Aluminium",
            },
            ExternalId = "material-1234",
            Density = 2800, // specified in kg/m3
            Supplier = "Lindner",
            IsActive = true, // allow it to be used for mapping,
            MaterialFamilies = new Dictionary<string, string>() {
                { "madaster", "metal" }
            },
        };

        await commodityClient.CreateCommodityAsync(material);
    }

    /// <summary>
    /// Creates a product that scales on the area when matched in Madaster
    /// </summary>
    /// <param name="databaseId"></param>
    /// <returns></returns>
    static async Task EnsureAreaProductExists(Guid databaseId)
    {
        if (await CommodityExists(databaseId, "7da4b28d-ba0c-4d7e-bcef-7cd4e1cb222b"))
        {
            return;
        }

        var product = new Product()
        {
            DatabaseId = databaseId,
            FunctionalUnit = FunctionalUnit.Area,
            DefaultFunctionalUnitAmount = 1, //m2
            FixedHeight = 0.1, // the height/depth of the product is 10 cm
            Names = new()
            {
                En = "Plafotherm® Heated and Chilled Ceilings",
            },
            ExternalId = "7da4b28d-ba0c-4d7e-bcef-7cd4e1cb222b",
            IsActive = true, // allow it to be used for mapping,
            CalculateEnvironmentValuesFromBillOfMaterials = false,
            DoNotUseBillOfMaterals = true,
            Density = 16, // 16kg/m2
            Lifespan = 50, //Expected lifespan of 50 years
            Url = new Uri("https://www.lindner-group.com/en/fit-out-products/heated-and-chilled-ceilings/heated-and-chilled-hook-on-ceilings/plafothermr-e-200/"),
        };

        await commodityClient.CreateCommodityAsync(product);
    }

    /// <summary>
    /// Checks if a commodity (material or product) can be found in the database with the external Id.
    /// </summary>
    /// <param name="databaseId"></param>
    /// <param name="externalId"></param>
    /// <returns></returns>
    static async Task<bool> CommodityExists(Guid databaseId, string externalId)
    {
        try
        {
            var c = await commodityClient.GetCommodityByExternalIdAsync(databaseId, externalId);
            return c != null;
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == (int)HttpStatusCode.NotFound)
            {
                return false;
            }

            throw;
        }
    }
}
