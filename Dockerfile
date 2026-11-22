FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Ashford.Web/Ashford.Web.csproj Ashford.Web/
RUN dotnet restore Ashford.Web/Ashford.Web.csproj
COPY src/ .
RUN dotnet publish Ashford.Web/Ashford.Web.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
COPY data/ /app/data/
COPY staging/ /app/staging/
RUN mkdir -p /app/logs /app/data/exports /app/data/manuals /app/data/service \
 && echo "FLAG{tar_balls_and_shell_calls}" > /app/data/service/.yard-token \
 && chown -R app:app /app/logs /app/data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ashford.Web.dll"]
