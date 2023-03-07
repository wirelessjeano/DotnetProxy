FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY . .
RUN dotnet restore

WORKDIR /app/DotnetProxy.SecureProxy
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app/DotnetProxy.SecureProxy/out ./DotnetProxy.SecureProxy
COPY --from=build /app/configs ./configs
COPY --from=build /app/version ./

EXPOSE 443
ENTRYPOINT ["dotnet", "DotnetProxy.SecureProxy/DotnetProxy.SecureProxy.dll"]