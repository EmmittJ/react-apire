var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("aca");

builder
    .AddNpmApp("web", "../web", "dev")
    .WithEnvironment("BROWSER", "none")
    .WithEnvironment("VITE_APP_DEPLOYMENT", "local")
    .WithHttpEndpoint(env: "VITE_PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile(c =>
    {
        c.WithEndpoint("http", e => e.TargetPort = 80);
        c.WithViteEnvironmentExposed(overrides: new()
        {
            ["VITE_APP_DEPLOYMENT"] = "aca"
        });
    });

builder
    .AddDockerfile("web-container", "../web")
    .WithHttpEndpoint(targetPort: 80)
    .WithEnvironment("EXPOSE_APP_DEPLOYMENT", "aca")
    .WithExternalHttpEndpoints()
    .WithExplicitStart();

builder.Build().Run();

public static partial class Extensions
{
    public static IResourceBuilder<T> WithViteEnvironmentExposed<T>(this IResourceBuilder<T> resourceBuilder, string sourcePrefix = "VITE_APP_", string targetPrefix = "EXPOSE_APP_", Dictionary<string, object>? overrides = null) where T : IResourceWithEnvironment
    {
        return resourceBuilder.WithEnvironment(context =>
        {
            foreach (var (key, value) in context.EnvironmentVariables.ToArray())
            {
                if (key.StartsWith(sourcePrefix))
                {
                    context.EnvironmentVariables.Remove(key);
                    var newKey = key.Replace(sourcePrefix, targetPrefix);
                    context.EnvironmentVariables[newKey] = overrides?.TryGetValue(key, out var overrideValue) == true ? overrideValue : value;
                }
            }
        });
    }
}