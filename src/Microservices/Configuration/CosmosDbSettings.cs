namespace Microservices.Configuration;

/// <summary>
/// Cosmos DB configuration settings
/// </summary>
public class CosmosDbSettings
{
    public const string SectionName = "CosmosDb";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string UsersContainerName { get; set; } = "Users";
    public string UsersPartitionKeyPath { get; set; } = "/orgId";
    public string OrganizationsContainerName { get; set; } = "Organizations";
    public string OrganizationsPartitionKeyPath { get; set; } = "/id";
}
