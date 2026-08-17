FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Ferrealiados.Cotizaciones.slnx .
COPY Ferrealiados.Cotizaciones.Modelo/Ferrealiados.Cotizaciones.Modelo.csproj Ferrealiados.Cotizaciones.Modelo/
COPY Ferrealiados.Cotizaciones.Negocio/Ferrealiados.Cotizaciones.Negocio.csproj Ferrealiados.Cotizaciones.Negocio/
COPY Ferrealiados.Cotizaciones.Api/Ferrealiados.Cotizaciones.Api.csproj Ferrealiados.Cotizaciones.Api/
COPY Ferrealiados.Cotizaciones.MigracionExcel/Ferrealiados.Cotizaciones.MigracionExcel.csproj Ferrealiados.Cotizaciones.MigracionExcel/
COPY Ferrealiados.Cotizaciones.TestUnitarios/Ferrealiados.Cotizaciones.TestUnitarios.csproj Ferrealiados.Cotizaciones.TestUnitarios/
RUN dotnet restore Ferrealiados.Cotizaciones.Api/Ferrealiados.Cotizaciones.Api.csproj

COPY Ferrealiados.Cotizaciones.Modelo/ Ferrealiados.Cotizaciones.Modelo/
COPY Ferrealiados.Cotizaciones.Negocio/ Ferrealiados.Cotizaciones.Negocio/
COPY Ferrealiados.Cotizaciones.Api/ Ferrealiados.Cotizaciones.Api/

RUN dotnet publish Ferrealiados.Cotizaciones.Api/Ferrealiados.Cotizaciones.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "Ferrealiados.Cotizaciones.Api.dll"]
