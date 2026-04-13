FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SeeMusic.Backend.csproj", "./"]
RUN dotnet restore "SeeMusic.Backend.csproj"
COPY . .
RUN dotnet build "SeeMusic.Backend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SeeMusic.Backend.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 5001
ENV ASPNETCORE_URLS=http://+:5001
ENTRYPOINT ["dotnet", "SeeMusic.Backend.dll"]
