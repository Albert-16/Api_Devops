namespace DockerizeAPI.Tests.Fixtures;

/// <summary>
/// Colección compartida para tests de integración.
/// Todos los tests que usen [Collection("Integration")] comparten
/// la misma instancia de CustomWebApplicationFactory, evitando
/// conflictos de puertos y estado duplicado.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>;
