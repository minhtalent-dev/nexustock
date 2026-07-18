FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution và csproj trước để cache restore layer
COPY ["Nexustock.sln", "./"]
COPY ["backend/Nexustock.Api/Nexustock.Api.csproj", "backend/Nexustock.Api/"]

# Copy module projects
COPY ["backend/modules/Nexustock.Modules.Identity/Nexustock.Modules.Identity.csproj", "backend/modules/Nexustock.Modules.Identity/"]
COPY ["backend/modules/Nexustock.Modules.MasterData/Nexustock.Modules.MasterData.csproj", "backend/modules/Nexustock.Modules.MasterData/"]
COPY ["backend/modules/Nexustock.Modules.Inbound/Nexustock.Modules.Inbound.csproj", "backend/modules/Nexustock.Modules.Inbound/"]
COPY ["backend/modules/Nexustock.Modules.Qc/Nexustock.Modules.Qc.csproj", "backend/modules/Nexustock.Modules.Qc/"]
COPY ["backend/modules/Nexustock.Modules.Inventory/Nexustock.Modules.Inventory.csproj", "backend/modules/Nexustock.Modules.Inventory/"]
COPY ["backend/modules/Nexustock.Modules.Exceptions/Nexustock.Modules.Exceptions.csproj", "backend/modules/Nexustock.Modules.Exceptions/"]
COPY ["backend/modules/Nexustock.Modules.Rules/Nexustock.Modules.Rules.csproj", "backend/modules/Nexustock.Modules.Rules/"]
COPY ["backend/modules/Nexustock.Modules.Putaway/Nexustock.Modules.Putaway.csproj", "backend/modules/Nexustock.Modules.Putaway/"]
COPY ["backend/modules/Nexustock.Modules.Allocation/Nexustock.Modules.Allocation.csproj", "backend/modules/Nexustock.Modules.Allocation/"]
COPY ["backend/modules/Nexustock.Modules.Replenishment/Nexustock.Modules.Replenishment.csproj", "backend/modules/Nexustock.Modules.Replenishment/"]
COPY ["backend/modules/Nexustock.Modules.Lpn/Nexustock.Modules.Lpn.csproj", "backend/modules/Nexustock.Modules.Lpn/"]
COPY ["backend/modules/Nexustock.Modules.Serial/Nexustock.Modules.Serial.csproj", "backend/modules/Nexustock.Modules.Serial/"]
COPY ["backend/modules/Nexustock.Modules.Rma/Nexustock.Modules.Rma.csproj", "backend/modules/Nexustock.Modules.Rma/"]
COPY ["backend/modules/Nexustock.Modules.Wave/Nexustock.Modules.Wave.csproj", "backend/modules/Nexustock.Modules.Wave/"]
COPY ["backend/modules/Nexustock.Modules.MaterialGenealogy/Nexustock.Modules.MaterialGenealogy.csproj", "backend/modules/Nexustock.Modules.MaterialGenealogy/"]
COPY ["backend/modules/Nexustock.Modules.LocalAgent/Nexustock.Modules.LocalAgent.csproj", "backend/modules/Nexustock.Modules.LocalAgent/"]
COPY ["backend/modules/Nexustock.Modules.LabelPrinting/Nexustock.Modules.LabelPrinting.csproj", "backend/modules/Nexustock.Modules.LabelPrinting/"]
COPY ["backend/modules/Nexustock.Modules.ErpIntegration/Nexustock.Modules.ErpIntegration.csproj", "backend/modules/Nexustock.Modules.ErpIntegration/"]
COPY ["backend/modules/Nexustock.Modules.Webhook/Nexustock.Modules.Webhook.csproj", "backend/modules/Nexustock.Modules.Webhook/"]
COPY ["backend/modules/Nexustock.Modules.Observability/Nexustock.Modules.Observability.csproj", "backend/modules/Nexustock.Modules.Observability/"]

RUN dotnet restore "backend/Nexustock.Api/Nexustock.Api.csproj"

# Copy toàn bộ code
COPY . .
WORKDIR "/src/backend/Nexustock.Api"
RUN dotnet build "Nexustock.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Nexustock.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080
ENTRYPOINT ["dotnet", "Nexustock.Api.dll"]
