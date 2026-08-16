# STAGE 1: Build (build context = repo root — see Global Constraints)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /src

# Copy only the .csproj files first for layer caching — sibling projects included
COPY ADSUS_BE/ADSUS_BE.csproj ADSUS_BE/
COPY ADSUS_BE.BLL/ADSUS_BE.BLL.csproj ADSUS_BE.BLL/
COPY ADSUS_BE.DAL/ADSUS_BE.DAL.csproj ADSUS_BE.DAL/
RUN dotnet restore ADSUS_BE/ADSUS_BE.csproj

# Now copy the rest of the source and publish
COPY ADSUS_BE/ ADSUS_BE/
COPY ADSUS_BE.BLL/ ADSUS_BE.BLL/
COPY ADSUS_BE.DAL/ ADSUS_BE.DAL/
RUN dotnet publish ADSUS_BE/ADSUS_BE.csproj -c Release -o /app/publish --no-restore

# STAGE 2: Run
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=builder /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ADSUS_BE.dll"]
