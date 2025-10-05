# React + Aspire Environment Variables Setup

This project in an example of managing environment variables in a React application that can be deployed both locally (with Vite) and in production (as a containerized application). The key feature is the `runtime.env` object that provides access to environment variables at runtime, even in production builds.

## Overview

The `runtime.env` pattern allows React components to access environment variables that can be:

- **Build-time injected** during local development with Vite
- **Runtime replaced** in production Docker containers using a shell script

This is particularly useful for .NET Aspire deployments where you want the same React build to work across different environments (local, Azure Container Apps, etc.) with different configuration values.

## How It Works

### 1. Type Definitions (`web/vite-env.d.ts`)

The TypeScript declarations define the `runtime.env` global object:

```typescript
interface ImportMetaEnv {
  readonly VITE_APP_DEPLOYMENT: string;
}

declare const runtime: {
  env: ImportMetaEnv;
};
```

This provides type-safe access to environment variables throughout your React application.

### 2. Vite Configuration (`web/vite.config.ts`)

During development, Vite's `define` option replaces `runtime.env` with actual environment variables:

```typescript
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd());
  return {
    plugins: [react()],
    define: {
      "runtime.env": env,
    },
  };
});
```

This means during development, `runtime.env.VITE_APP_DEPLOYMENT` is replaced at build time with the actual value from your `.env` files.

### 3. Production Build Placeholder (`web/.env.production`)

The production environment file contains placeholders that will be replaced at container runtime:

```
VITE_APP_DEPLOYMENT=EXPOSE_APP_DEPLOYMENT
```

The `EXPOSE_APP_` prefix indicates this value should be replaced by the `env.sh` script.

### 4. Runtime Replacement Script (`web/env.sh`)

This shell script runs when the Docker container starts (via nginx's `docker-entrypoint.d` mechanism):

```bash
#!/bin/sh
# Finds all environment variables starting with 'EXPOSE_APP_'
# and replaces them in the built JavaScript files

directory=/usr/share/nginx/html

env | grep -E '^EXPOSE_APP_' | while IFS= read -r i
do
    key=$(echo "$i" | cut -d '=' -f 1)
    value=$(echo "$i" | cut -d '=' -f 2-)
    echo "$key"="$value"
    find $directory -type f -exec sed -i "s|${key}|${value}|g" '{}' +
done
```

This script:

1. Searches for all environment variables with the `EXPOSE_APP_` prefix
2. Uses `sed` to find and replace these placeholders in all built files
3. Runs before nginx starts serving the application

### 5. Dockerfile Setup (`web/Dockerfile`)

The Dockerfile copies the script and makes it executable:

```dockerfile
# Production stage
FROM nginx:alpine

COPY --from=build /app/dist /usr/share/nginx/html/
COPY ./env.sh /docker-entrypoint.d/env.sh
RUN chmod +x /docker-entrypoint.d/env.sh

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

The `/docker-entrypoint.d/` directory is special in the nginx image - scripts placed here run automatically before nginx starts.

### 6. Aspire Configuration (`apphost/AppHost.cs`)

The Aspire AppHost configures the application to run differently based on the environment:

```csharp
// Local development - uses Vite dev server
builder
    .AddNpmApp("web", "../web", "dev")
    .WithEnvironment("VITE_APP_DEPLOYMENT", "local")

// Production deploy - uses nginx with runtime replacement
    .PublishAsDockerFile(c =>
    {
        c.WithEndpoint("http", e => e.TargetPort = 80);
        c.WithViteEnvironmentExposed(overrides: new()
        {
            ["VITE_APP_DEPLOYMENT"] = "aca"
        });
    });
```

The `WithViteEnvironmentExposed` extension method transforms `VITE_APP_*` variables to `EXPOSE_APP_*` for the container deployment:

```csharp
public static IResourceBuilder<T> WithViteEnvironmentExposed<T>(
    this IResourceBuilder<T> resourceBuilder,
    string sourcePrefix = "VITE_APP_",
    string targetPrefix = "EXPOSE_APP_",
    Dictionary<string, object>? overrides = null)
    where T : IResourceWithEnvironment
{
    return resourceBuilder.WithEnvironment(context =>
    {
        foreach (var (key, value) in context.EnvironmentVariables.ToArray())
        {
            if (key.StartsWith(sourcePrefix))
            {
                context.EnvironmentVariables.Remove(key);
                var newKey = key.Replace(sourcePrefix, targetPrefix);
                context.EnvironmentVariables[newKey] =
                    overrides?.TryGetValue(key, out var overrideValue) == true
                        ? overrideValue
                        : value;
            }
        }
    });
}
```

### 7. Usage in React Components (`web/src/App.tsx`)

Access environment variables through the `runtime.env` object:

```typescript
function App() {
  const isAca = runtime.env.VITE_APP_DEPLOYMENT === "aca";

  return (
    <>
      <h1>Aspire + React{!isAca ? " + Vite" : ""}</h1>
      {/* Conditional rendering based on environment */}
    </>
  );
}
```

## Environment Variable Flow

### Local Development (Vite Dev Server)

1. Aspire starts the npm app with `VITE_APP_DEPLOYMENT=local`
2. Vite dev server loads environment variables
3. Vite's `define` replaces `runtime.env` → `{ VITE_APP_DEPLOYMENT: "local" }`
4. TypeScript code can access `runtime.env.VITE_APP_DEPLOYMENT` which equals `"local"`
5. Hot Module Replacement (HMR) works as expected

### Production Container (Deployed via Aspire)

1. During deploy, Aspire uses `PublishAsDockerFile` to build a Docker container
2. Vite build uses `.env.production` → `runtime.env.VITE_APP_DEPLOYMENT` becomes `"EXPOSE_APP_DEPLOYMENT"` (a placeholder)
3. `WithViteEnvironmentExposed` transforms environment variables (with override `VITE_APP_DEPLOYMENT` → `aca`)
4. Docker container starts with environment variable `EXPOSE_APP_DEPLOYMENT=aca`
5. `env.sh` script runs and replaces the string `"EXPOSE_APP_DEPLOYMENT"` with `"aca"` in built files
6. React app can now access `runtime.env.VITE_APP_DEPLOYMENT` which equals `"aca"`

## Adding New Environment Variables

To add a new environment variable:

1. **Update TypeScript types** (`web/vite-env.d.ts`):

   ```typescript
   interface ImportMetaEnv {
     readonly VITE_APP_DEPLOYMENT: string;
     readonly VITE_APP_API_URL: string; // Add new variable
   }
   ```

2. **Add to production env** (`web/.env.production`):

   ```
   VITE_APP_DEPLOYMENT=EXPOSE_APP_DEPLOYMENT
   VITE_APP_API_URL=EXPOSE_APP_API_URL
   ```

3. **Set in local development** (`apphost/AppHost.cs`):

   ```csharp
   builder
       .AddNpmApp("web", "../web", "dev")
       .WithEnvironment("VITE_APP_API_URL", "http://localhost:5000")
   ```

4. **Set in production deployment** (`apphost/AppHost.cs`):

   ```csharp
       .PublishAsDockerFile(c =>
       {
           c.WithViteEnvironmentExposed(overrides: new()
           {
               ["VITE_APP_API_URL"] = "https://api.example.com"
           });
       });
   ```

5. **Use in your React code**:
   ```typescript
   const apiUrl = runtime.env.VITE_APP_API_URL;
   ```

## Benefits

✅ **Type Safety**: Full TypeScript support for environment variables  
✅ **Runtime Configuration**: Change values without rebuilding the container  
✅ **Single Build**: Same Docker image can be deployed to multiple environments  
✅ **Aspire Integration**: Seamlessly works with .NET Aspire orchestration  
✅ **Development Parity**: Same variable access pattern in dev and production

## Conventions

- All environment variables use the `VITE_APP_` prefix in React code
- Production placeholders use the `EXPOSE_APP_` prefix
- The `env.sh` script automatically handles the transformation
- Type all variables in `ImportMetaEnv` interface for IntelliSense support
