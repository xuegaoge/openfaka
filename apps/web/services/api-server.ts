/**
 * 服务端数据获取工具（仅用于 Server Component）
 *
 * 与客户端 api.ts 的区别：
 * - 直接使用 BACKEND_URL 调用后端，不走 Next.js rewrites 代理
 * - 不依赖 localStorage（服务端无浏览器环境）
 * - 仅封装公开接口（不需要 auth token）
 */

import type {
  PaginatedData,
  ProductCard,
  ProductDetail,
  Category,
  PaymentChannelItem,
  SiteConfig,
  CurrencyItem,
} from "@/types"

// 后端直连地址（Docker 内部网络或本地开发）
const BACKEND_URL = process.env.BACKEND_URL || "http://localhost:5120"

// ============================================================
// Response unwrapper (handles lin-cms mixed format)
// ============================================================

function unwrapResponse<T>(body: unknown): T {
  if (body && typeof body === 'object' && 'code' in body) {
    const res = body as { code: number; message?: string; data?: T }
    if (res.code !== 0) {
      throw new Error(res.message || `API error code: ${res.code}`)
    }
    if ('data' in res) {
      return res.data as T
    }
    return body as T
  }
  return body as T
}

// ============================================================
// Core request
// ============================================================

async function serverRequest<T>(path: string): Promise<T> {
  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), 15_000) // 15s timeout for SSR

  let res: Response
  try {
    res = await fetch(`${BACKEND_URL}${path}`, {
      headers: { "Content-Type": "application/json" },
      cache: "no-store",
      signal: controller.signal,
    })
  } catch (err) {
    clearTimeout(timeoutId)
    if (err instanceof DOMException && err.name === "AbortError") {
      throw new Error(`Server API timeout: ${path}`)
    }
    throw new Error(`Server API network error: ${err instanceof Error ? err.message : "unknown"} [${path}]`)
  } finally {
    clearTimeout(timeoutId)
  }

  if (!res.ok) {
    throw new Error(`Server API error: ${res.status} ${res.statusText} [${path}]`)
  }

  const body = await res.json()
  return unwrapResponse<T>(body)
}

// ============================================================
// Public API — 商品
// ============================================================

export async function getProducts(params?: {
  page?: number
  page_size?: number
  category_id?: string
  keyword?: string
}): Promise<PaginatedData<ProductCard>> {
  const sp = new URLSearchParams()
  // 前端 page 从 1 开始，后端从 0 开始
  if (params?.page) sp.set("page", String(Math.max(0, params.page - 1)))
  // 后端使用 count 参数
  if (params?.page_size) sp.set("count", String(params.page_size))
  if (params?.category_id) sp.set("category_id", params.category_id)
  if (params?.keyword) sp.set("keyword", params.keyword)
  const qs = sp.toString()
  return serverRequest<PaginatedData<ProductCard>>(
    `/store/products${qs ? `?${qs}` : ""}`
  )
}

export async function getProductDetail(id: string): Promise<ProductDetail> {
  return serverRequest<ProductDetail>(`/store/products/${id}`)
}

// ============================================================
// Public API — 分类
// ============================================================

export async function getCategories(): Promise<Category[]> {
  return serverRequest<Category[]>("/store/categories")
}

// ============================================================
// Public API — 支付渠道
// ============================================================

export async function getPaymentChannels(): Promise<PaymentChannelItem[]> {
  return serverRequest<PaymentChannelItem[]>("/store/payment-channels")
}

// ============================================================
// Public API — 站点配置
// ============================================================

export async function getSiteConfig(): Promise<SiteConfig> {
  return serverRequest<SiteConfig>("/store/config")
}

// ============================================================
// Public API — 货币
// ============================================================

export async function getCurrencies(): Promise<CurrencyItem[]> {
  // 使用固定货币列表，后续可从后端获取
  return [
    { code: "CNY", name: "人民币", symbol: "¥" },
    { code: "USD", name: "美元", symbol: "$" },
    { code: "USDT", name: "USDT", symbol: "₮" },
  ]
}
