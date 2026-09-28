FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Se copia primero solo el .csproj para que la capa del restore quede en
# caché mientras no cambien los paquetes NuGet.
COPY TicketsSistemas.Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Usuario sin privilegios que ya trae la imagen oficial de .NET 8; la API no
# necesita root para escuchar en el 8080.
USER $APP_UID

# La imagen aspnet no trae curl/wget: se checa /health abriendo el socket
# con bash directamente.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && grep -q "200 OK" <&3'

ENTRYPOINT ["dotnet", "TicketsSistemas.Api.dll"]
