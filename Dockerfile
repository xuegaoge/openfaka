# ============ Frontend Build Stage ============
FROM node:22-alpine AS frontend-build
WORKDIR /app/web
COPY apps/web/package.json apps/web/pnpm-lock.yaml ./
RUN corepack enable && pnpm install --frozen-lockfile
COPY apps/web/ ./
ENV NEXT_TELEMETRY_DISABLED=1
RUN pnpm build

# ============ Backend Build Stage ============
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /app

# 复制解决方案和项目文件（利用 Docker 缓存层）
COPY OpenFaka.slnx ./
COPY src/OpenFaka.Core/OpenFaka.Core.csproj src/OpenFaka.Core/
COPY src/OpenFaka.Application/OpenFaka.Application.csproj src/OpenFaka.Application/
COPY src/OpenFaka.Infrastructure/OpenFaka.Infrastructure.csproj src/OpenFaka.Infrastructure/
COPY src/OpenFaka.Web/OpenFaka.Web.csproj src/OpenFaka.Web/
COPY libs/lin-cms-dotnetcore/src/LinCms.Core/LinCms.Core.csproj libs/lin-cms-dotnetcore/src/LinCms.Core/
COPY libs/lin-cms-dotnetcore/src/LinCms.Application/LinCms.Application.csproj libs/lin-cms-dotnetcore/src/LinCms.Application/
COPY libs/lin-cms-dotnetcore/src/LinCms.Application.Contracts/LinCms.Application.Contracts.csproj libs/lin-cms-dotnetcore/src/LinCms.Application.Contracts/
COPY libs/lin-cms-dotnetcore/src/LinCms.Infrastructure/LinCms.Infrastructure.csproj libs/lin-cms-dotnetcore/src/LinCms.Infrastructure/

RUN dotnet restore src/OpenFaka.Web/OpenFaka.Web.csproj

# 复制全部源码并发布
COPY src/ src/
COPY libs/ libs/
RUN dotnet publish src/OpenFaka.Web/OpenFaka.Web.csproj -c Release -o /app/publish --no-restore

# ============ Runtime Stage ============
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 安装 curl 用于健康检查
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# 复制后端发布产物
COPY --from=backend-build /app/publish .

# 复制前端静态文件到 wwwroot（供 .NET 静态文件服务）
COPY --from=frontend-build /app/web/.next/standalone/ ./wwwroot/
COPY --from=frontend-build /app/web/.next/static/ ./wwwroot/.next/static/
COPY --from=frontend-build /app/web/public/ ./wwwroot/public/

# 数据持久化目录
VOLUME /app/data

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/openfaka.db"

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s \
    CMD curl -f http://localhost:5000/store/categories || exit 1

ENTRYPOINT ["dotnet", "OpenFaka.Web.dll"]
