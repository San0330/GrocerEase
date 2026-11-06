# ─────────────────────────────────────────────
# Stage 1: Build
# Use the full .NET SDK image to restore and build the app
# ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build

# Set the working directory inside the container
WORKDIR /src

# Package versions are central, so restore needs this file next to the project files.
COPY Directory.Packages.props .

# Copy all project files first (for layer caching — avoids re-restoring on code-only changes)
COPY GrocerEase.Api/GrocerEase.Api.csproj             GrocerEase.Api/
COPY GrocerEase.Application/GrocerEase.Application.csproj   GrocerEase.Application/
COPY GrocerEase.Domain/GrocerEase.Domain.csproj           GrocerEase.Domain/
COPY GrocerEase.Infrastructure/GrocerEase.Infrastructure.csproj GrocerEase.Infrastructure/
COPY GrocerEase.Contracts/GrocerEase.Contracts.csproj       GrocerEase.Contracts/

# Restore NuGet dependencies for the API project (pulls in all referenced projects too)
RUN dotnet restore GrocerEase.Api/GrocerEase.Api.csproj

# Copy the rest of the source code
COPY . .

# Publish the API in Release mode into /app/publish
# --no-restore: skip restore since we already did it above
RUN dotnet publish GrocerEase.Api/GrocerEase.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─────────────────────────────────────────────
# Stage 2: Runtime
# Use the smaller ASP.NET runtime image (no SDK, smaller attack surface)
# ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c AS runtime

# Set the working directory for the running app
WORKDIR /app

# Copy only the published output from the build stage
COPY --from=build /app/publish .

# Expose port 8080 (ASP.NET Core default in containers)
EXPOSE 8080

# Run as the non-root user the base image provides (APP_UID), so a compromised process is not root in the container.
# Port 8080 is above 1024, so no privilege is needed to listen on it.
USER $APP_UID

# Set the entry point — runs the compiled API dll
ENTRYPOINT ["dotnet", "GrocerEase.Api.dll"]
