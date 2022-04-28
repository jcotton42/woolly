#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base

RUN groupadd --gid 1000 woolly && useradd --uid 1000 --gid woolly --shell /bin/false --create-home woolly
RUN mkdir /app
RUN chown woolly:woolly /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["src/Woolly/Woolly.csproj", "src/Woolly/"]
RUN dotnet restore "src/Woolly/Woolly.csproj"
COPY . .
WORKDIR "/src/src/Woolly"
RUN dotnet build "Woolly.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Woolly.csproj" /p:DebugType=None -c Release -o /app/publish

FROM base AS final
USER woolly
WORKDIR /app
COPY --from=publish --chown=woolly:woolly /app/publish .
ENTRYPOINT ["dotnet", "woolly.dll"]
