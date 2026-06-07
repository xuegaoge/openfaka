// 屏蔽 Node 24+ 的 DEP0060 警告 (util._extend，来自内部依赖，无法从源头修复)
const _origWarn = process.emitWarning
process.emitWarning = function (warning, ...args) {
  if (args[0] === "DeprecationWarning" && args[1] === "DEP0060") return
  if (typeof warning === "object" && warning?.code === "DEP0060") return
  return _origWarn.call(this, warning, ...args)
}

/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'standalone',   // Docker 部署必须：生成独立运行的 server.js，不依赖完整 node_modules
  images: {
    unoptimized: true,
  },
  // 开发模式: 将请求代理到 .NET 后端
  async rewrites() {
    const backendUrl = process.env.BACKEND_URL || "http://localhost:5120"
    return [
      // lin-cms CMS 接口
      { source: "/cms/:path*", destination: `${backendUrl}/cms/:path*` },
      // 发卡网用户端接口
      { source: "/store/:path*", destination: `${backendUrl}/store/:path*` },
      // 发卡网管理端接口
      { source: "/admin/:path*", destination: `${backendUrl}/admin/:path*` },
      // 博客接口
      { source: "/blog/:path*", destination: `${backendUrl}/blog/:path*` },
      // 文件上传
      { source: "/uploads/:path*", destination: `${backendUrl}/uploads/:path*` },
      // 兼容旧 /api/* 路径
      { source: "/api/:path*", destination: `${backendUrl}/:path*` },
    ]
  },
}

export default nextConfig
