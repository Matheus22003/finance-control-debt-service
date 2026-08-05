FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /source

COPY FinanceControl.DebtService/src/FinanceControl.DebtService/FinanceControl.DebtService.csproj \
    FinanceControl.DebtService/src/FinanceControl.DebtService/
COPY FinanceControl.DebtService/src/FinanceControl.DebtService/packages.lock.json \
    FinanceControl.DebtService/src/FinanceControl.DebtService/
RUN dotnet restore \
    FinanceControl.DebtService/src/FinanceControl.DebtService/FinanceControl.DebtService.csproj \
    --locked-mode

COPY FinanceControl.DebtService/src/FinanceControl.DebtService/ \
    FinanceControl.DebtService/src/FinanceControl.DebtService/
RUN dotnet publish \
    FinanceControl.DebtService/src/FinanceControl.DebtService/FinanceControl.DebtService.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8082
EXPOSE 8082

USER $APP_UID
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "FinanceControl.DebtService.dll"]
