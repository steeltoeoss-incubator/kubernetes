FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR src
# copy nuget.config files at solution and project levels
# copy any local nuget sources that are subfolders of the solution
COPY ["src/InformersBase/Steeltoe.Informers.InformersBase.csproj", "src/InformersBase/Steeltoe.Informers.InformersBase.csproj"]
COPY ["src/KubernetesBase/Steeltoe.Informers.KubernetesBase.csproj", "src/KubernetesBase/Steeltoe.Informers.KubernetesBase.csproj"]
COPY ["src/KubernetesCore/Steeltoe.Informers.KubernetesCore.csproj", "src/KubernetesCore/Steeltoe.Informers.KubernetesCore.csproj"]
COPY ["samples/informers/informers.csproj", "samples/informers/informers.csproj"]
RUN dotnet restore "samples\informers\informers.csproj"
COPY . .
RUN dotnet msbuild /p:RestorePackages=false /t:PublishLayer /p:PublishDir=/layer/ /p:DockerLayer=All "samples/informers/informers.csproj"
FROM mcr.microsoft.com/dotnet/core/runtime:3.1 AS run
WORKDIR /app
COPY --from=build /layer/package ./
COPY --from=build /layer/earlypackage ./
COPY --from=build /layer/project ./
COPY --from=build /layer/app ./
ENTRYPOINT ["dotnet", "informers.dll"]
