namespace GrocerEase.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<GrocerEaseApiFactory>
{
    public const string Name = "GrocerEase integration";
}
