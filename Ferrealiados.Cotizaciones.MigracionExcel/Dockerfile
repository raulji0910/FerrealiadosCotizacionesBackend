FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Ferrealiados.Cotizaciones.slnx .
COPY Ferrealiados.Cotizaciones.Modelo/Ferrealiados.Cotizaciones.Modelo.csproj Ferrealiados.Cotizaciones.Modelo/
COPY Ferrealiados.Cotizaciones.MigracionExcel/Ferrealiados.Cotizaciones.MigracionExcel.csproj Ferrealiados.Cotizaciones.MigracionExcel/
RUN dotnet restore Ferrealiados.Cotizaciones.MigracionExcel/Ferrealiados.Cotizaciones.MigracionExcel.csproj

COPY Ferrealiados.Cotizaciones.Modelo/ Ferrealiados.Cotizaciones.Modelo/
COPY Ferrealiados.Cotizaciones.MigracionExcel/ Ferrealiados.Cotizaciones.MigracionExcel/

RUN dotnet publish Ferrealiados.Cotizaciones.MigracionExcel/Ferrealiados.Cotizaciones.MigracionExcel.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

USER $APP_UID
ENTRYPOINT ["dotnet", "Ferrealiados.Cotizaciones.MigracionExcel.dll"]
