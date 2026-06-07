import type {
  ApiResponse,
  PaginatedData,
  Pagination,
  UserProfile,
  OrderBrief,
  OrderDetail,
  QueryOrderResult,
  OrderStatus,
  OrderType,
  PointRecord,
  PointsData,
  ProductCard,
  ProductDetail,
  ProductSpec,
  Category,
  Cart,
  CreateOrderRequest,
  CreateCartOrderRequest,
  CreateOrderResult,
  DeliverResult,
  SiteConfig,
  SiteConfigKV,
  DashboardStats,
  SalesTrend,
  CardKeyStockSummary,
  CardKeyListItem,
  CardImportBatch,
  OrderCardKey,
  AdminUserItem,
  AdminOrderItem,
  PaymentChannelItem,
  OperationLog,
  RiskConfig,
  WholesaleRule,
  CaptchaResult,
  AuthResult,
  CurrencyItem,
  TxidVerifyResult,
} from "@/types"

// ============================================================
// Config
// ============================================================

// lin-cms 接口直接使用 /cms/*, /store/*, /admin/* 路径，通过 next.config.mjs 代理到后端
const API_BASE = process.env.NEXT_PUBLIC_API_BASE || ""

// ============================================================
// Error
// ============================================================

export class ApiError extends Error {
  code: number
  params?: Record<string, string | number>
  status?: number
  constructor(code: number, message: string, params?: Record<string, string | number>, status?: number) {
    super(message)
    this.code = code
    this.params = params
    this.status = status
    this.name = "ApiError"
  }

  get isNetworkError(): boolean {
    return this.code === 0 || this.status === 0
  }

  get isAuthError(): boolean {
    return this.status === 401 || this.code === 10002
  }

  get isRateLimit(): boolean {
    return this.status === 429 || this.code === 10005
  }

  get isServerError(): boolean {
    return (this.status !== undefined && this.status >= 500) || this.code === 10006
  }
}

// ============================================================
// Token management (lin-cms: access_token + refresh_token)
// ============================================================

function getAccessToken(): string | null {
  if (typeof window === "undefined") return null
  return localStorage.getItem("access_token")
}

function getRefreshToken(): string | null {
  if (typeof window === "undefined") return null
  return localStorage.getItem("refresh_token")
}

export function setTokens(accessToken: string, refreshToken: string) {
  localStorage.setItem("access_token", accessToken)
  localStorage.setItem("refresh_token", refreshToken)
}

export function clearToken() {
  localStorage.removeItem("access_token")
  localStorage.removeItem("refresh_token")
}

// 兼容旧调用
export function setToken(token: string) {
  localStorage.setItem("access_token", token)
}

/**
 * JWT 过期/无效时：清除本地登录态，跳转登录页
 * 使用防抖避免多个并发请求同时触发多次跳转
 */
let redirecting = false
function handleUnauthorized() {
  if (typeof window === "undefined" || redirecting) return
  redirecting = true
  clearToken()
  localStorage.removeItem("userProfile")
  const currentPath = window.location.pathname
  // 已经在登录页则不再跳转
  if (currentPath === "/login") {
    redirecting = false
    return
  }
  window.location.href = `/login?redirect=${encodeURIComponent(currentPath)}`
}

// ============================================================
// Session Token (guest cart)
// ============================================================

function getSessionToken(): string | null {
  if (typeof window === "undefined") return null
  return localStorage.getItem("session_token")
}

function setSessionToken(token: string) {
  localStorage.setItem("session_token", token)
}

export function clearSessionToken() {
  localStorage.removeItem("session_token")
}

// ============================================================
// Query builder
// ============================================================

function buildQuery(params: Record<string, string | number | boolean | undefined | null>): string {
  const sp = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") {
      if (key === "page_size") {
        sp.set("count", String(value))
      } else if (key === "page" && typeof value === "number") {
        // 前端 page 从 1 开始，后端从 0 开始
        sp.set("page", String(Math.max(0, value - 1)))
      } else {
        sp.set(key, String(value))
      }
    }
  }
  return sp.toString()
}

// ============================================================
// Device Fingerprint
// ============================================================

let cachedDeviceId: string | null = null

async function ensureDeviceId(): Promise<string> {
  if (cachedDeviceId) return cachedDeviceId
  if (typeof window === "undefined") return ""
  try {
    const { getDeviceId } = await import("@/lib/fingerprint")
    cachedDeviceId = await getDeviceId()
    return cachedDeviceId
  } catch {
    return ""
  }
}

// 启动时异步预热，不阻塞首屏
if (typeof window !== "undefined") {
  ensureDeviceId()
}

// ============================================================
// Turnstile token（模块级别，由页面组件设置）
// ============================================================

let pendingTurnstileToken: string | null = null

/** 设置 Turnstile token（在调用受保护 API 前由页面组件调用） */
export function setTurnstileHeaders(token: string) {
  pendingTurnstileToken = token
}

/** 消费并清除 Turnstile token（request 内部使用） */
function consumeTurnstileToken(): string | null {
  const t = pendingTurnstileToken
  pendingTurnstileToken = null
  return t
}

// ============================================================
// Response unwrapper (handles lin-cms mixed format)
// ============================================================

function unwrapResponse<T>(body: unknown): T {
  // lin-cms 可能直接返回业务对象，也可能返回 { code, message, data }
  if (body && typeof body === 'object' && 'code' in body) {
    const res = body as { code: number; message?: string; data?: T }

    if (res.code !== 0) {
      throw new ApiError(res.code, res.message || 'Request failed')
    }

    // 必须用 'data' in res 判断，避免 data 为 null / false / 0 时被误判
    if ('data' in res) {
      return res.data as T
    }

    // 兼容 { code: 0, message: '操作成功' } 这类空操作响应
    return body as T
  }

  // Controller直接返回T时原样返回
  return body as T
}

function normalizeOrderStatus(status: unknown): OrderStatus {
  switch (String(status ?? 'pending').toUpperCase()) {
    case 'PENDING':
      return 'PENDING'
    case 'PAID':
    case 'DELIVERING':
      return 'PAID'
    case 'DELIVERED':
      return 'DELIVERED'
    case 'EXPIRED':
    case 'CANCELLED':
    case 'REFUNDED':
      return 'EXPIRED'
    default:
      return 'PENDING'
  }
}

// ============================================================
// Token refresh
// ============================================================

let isRefreshing = false
let refreshPromise: Promise<void> | null = null

async function doRefreshToken(): Promise<void> {
  const refreshToken = getRefreshToken()
  if (!refreshToken) {
    throw new Error('No refresh token')
  }

  const res = await fetch(`${API_BASE}/cms/user/refresh`, {
    headers: { 'Authorization': `Bearer ${refreshToken}` },
  })

  if (!res.ok) {
    throw new Error('Refresh failed')
  }

  const body = await res.json()
  // lin-cms refresh 直接返回 tokens 对象
  const tokens = 'access_token' in body ? body : body.data
  if (tokens?.access_token) {
    setTokens(tokens.access_token, tokens.refresh_token || refreshToken)
  }
}

// ============================================================
// Core request
// ============================================================

async function request<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const token = getAccessToken()
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string>),
  }

  if (token) {
    headers["Authorization"] = `Bearer ${token}`
  }
  // 始终发送 session token（购物车等功能需要：JWT 无效/过期时作为身份回退）
  const sessionToken = getSessionToken()
  if (sessionToken) {
    headers["X-Session-Token"] = sessionToken
  }

  // 设备指纹 — 始终发送
  const deviceId = cachedDeviceId || (await ensureDeviceId())
  if (deviceId) {
    headers["X-Device-Id"] = deviceId
  }

  // Turnstile token — 如果有 pending token 则发送（单次消费）
  const turnstileToken = consumeTurnstileToken()
  if (turnstileToken) {
    headers["X-Turnstile-Token"] = turnstileToken
  }

  // 超时控制：30 秒
  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), 30_000)

  let res: Response
  try {
    res = await fetch(`${API_BASE}${path}`, {
      ...options,
      headers,
      signal: controller.signal,
    })
  } catch (err) {
    clearTimeout(timeoutId)
    if (err instanceof DOMException && err.name === "AbortError") {
      throw new ApiError(0, "Request timeout", undefined, 0)
    }
    // 网络错误（断网、DNS 失败等）
    throw new ApiError(0, err instanceof Error ? err.message : "Network error", undefined, 0)
  } finally {
    clearTimeout(timeoutId)
  }

  // 401 时尝试刷新 token
  if (res.status === 401 && getRefreshToken() && !path.includes('/cms/user/refresh')) {
    try {
      if (!isRefreshing) {
        isRefreshing = true
        refreshPromise = doRefreshToken()
      }
      await refreshPromise
      isRefreshing = false
      refreshPromise = null

      // 用新 token 重试
      const newToken = getAccessToken()
      if (newToken) {
        headers["Authorization"] = `Bearer ${newToken}`
        res = await fetch(`${API_BASE}${path}`, { ...options, headers })
      }
    } catch {
      isRefreshing = false
      refreshPromise = null
      handleUnauthorized()
      throw new ApiError(401, 'Unauthorized', undefined, 401)
    }
  }

  // Capture session token from response
  const newSessionToken = res.headers.get("X-Session-Token")
  if (newSessionToken) {
    setSessionToken(newSessionToken)
  }

  if (!res.ok) {
    if (res.status === 401) {
      handleUnauthorized()
    }
    const body = await res.json().catch(() => ({ code: res.status, message: res.statusText }))
    throw new ApiError(body.code || res.status, body.message || res.statusText, body.params, res.status)
  }

  const text = await res.text()
  if (!text.trim()) {
    return null as T
  }

  const body = JSON.parse(text)
  return unwrapResponse<T>(body)
}

async function uploadRequest<T>(path: string, formData: FormData): Promise<T> {
  const token = getAccessToken()
  const headers: Record<string, string> = {}
  if (token) {
    headers["Authorization"] = `Bearer ${token}`
  }

  const res = await fetch(`${API_BASE}${path}`, {
    method: "POST",
    headers,
    body: formData,
  })

  if (!res.ok) {
    if (res.status === 401) {
      handleUnauthorized()
    }
    const body = await res.json().catch(() => ({ code: res.status, message: res.statusText }))
    throw new ApiError(body.code || res.status, body.message || res.statusText, body.params)
  }

  const text = await res.text()
  if (!text.trim()) {
    return null as T
  }

  const body = JSON.parse(text)
  return unwrapResponse<T>(body)
}

// ============================================================
// Mock Fallback
// ============================================================

/**
 * Wraps an API call with a mock fallback.
 * Only network-level errors (TypeError from fetch) trigger fallback.
 * Business errors (ApiError with code!=0) propagate normally.
 */
export async function withMockFallback<T>(
  apiCall: () => Promise<T>,
  mockFn: () => T
): Promise<T> {
  try {
    return await apiCall()
  } catch (err) {
    if (err instanceof ApiError) {
      throw err // business error — let UI handle it
    }
    // Network error (TypeError) or unexpected — fallback to mock
    console.warn("[API] Network error, falling back to mock data:", err)
    return mockFn()
  }
}


// ============================================================
// Auth
// ============================================================

export const authApi = {
  getCaptcha: () =>
    request<CaptchaResult>("/cms/user/captcha"),
  register: (data: { username: string; password: string; email: string; captcha_id: string; captcha: string }) =>
    request<AuthResult>("/cms/user/account/register", { method: "POST", body: JSON.stringify(data) }),
  login: (data: { account: string; password: string }) =>
    request<AuthResult>("/cms/user/login", { method: "POST", body: JSON.stringify({ username: data.account, password: data.password }) }),
  logout: () =>
    request<null>("/cms/user/logout"),
}

// ============================================================
// User
// ============================================================

export const userApi = {
  getProfile: () =>
    request<UserProfile>("/cms/user/information"),
  updatePassword: (data: { old_password: string; new_password: string }) =>
    request<null>("/cms/user/change_password", { method: "PUT", body: JSON.stringify(data) }),
  getOrders: async (params: { page?: number; page_size?: number; status?: string }) => {
    const qs = buildQuery(params)
    const result = await request<{ list: Array<Record<string, unknown>>; pagination: Pagination }>(`/user/orders?${qs}`)
    return {
      list: result.list.map(r => ({
        id: String(r.id ?? r.order_no ?? ""),
        total_amount: Number(r.total_amount ?? 0),
        actual_amount: Number(r.actual_amount ?? 0),
        status: normalizeOrderStatus(r.status),
        order_type: String(r.order_type ?? "DIRECT").toUpperCase() as OrderType,
        payment_method: String(r.payment_method ?? ""),
        created_at: String(r.created_at ?? ""),
        usdt_tx_id: r.usdt_tx_id as string | undefined,
      })),
      pagination: result.pagination,
    } as PaginatedData<OrderBrief>
  },
  getPoints: (params: { page?: number; page_size?: number }) => {
    const qs = buildQuery(params)
    return request<PointsData>(`/user/points?${qs}`)
  },
}

// ============================================================
// Product
// ============================================================

export const productApi = {
  getList: (params: { page?: number; page_size?: number; category_id?: string; keyword?: string }) => {
    const qs = buildQuery(params)
    return request<PaginatedData<ProductCard>>(`/store/products?${qs}`)
  },
  getDetail: (id: string) =>
    request<ProductDetail>(`/store/products/${id}`),
  getCategories: () =>
    request<Category[]>("/store/categories"),
}

// ============================================================
// Cart
// ============================================================

export const cartApi = {
  get: () =>
    request<Cart>("/store/cart"),
  addItem: (data: { product_id: string; spec_id: string | null; quantity: number }) =>
    request<null>("/store/cart", { method: "POST", body: JSON.stringify(data) }),
  updateItem: (itemId: number, quantity: number) =>
    request<null>(`/store/cart/${itemId}`, { method: "PUT", body: JSON.stringify({ quantity }) }),
  removeItem: (itemId: number) =>
    request<null>(`/store/cart/${itemId}`, { method: "DELETE" }),
}

// ============================================================
// Order
// ============================================================

/**
 * 将后端 OrderDto 适配为前端 CreateOrderResult 格式
 */
function adaptOrderResponse(order: Record<string, unknown>): CreateOrderResult {
  // 后端 SnakeCaseLower 序列化：total_amount, actual_amount, payment_method 等
  return {
    order: {
      id: String(order.id ?? order.order_no ?? ""),
      total_amount: Number(order.total_amount ?? 0),
      actual_amount: Number(order.actual_amount ?? 0),
      status: normalizeOrderStatus(order.status),
      order_type: String(order.order_type ?? "DIRECT").toUpperCase() as OrderType,
      payment_method: String(order.payment_method ?? ""),
      email: String(order.email ?? ""),
      created_at: String(order.created_at ?? ""),
      paid_at: order.paid_at as string | null ?? null,
      delivered_at: order.delivered_at as string | null ?? null,
      expires_at: String(order.expires_at ?? ""),
      points_deducted: Number(order.points_deducted ?? 0),
      points_discount: Number(order.points_discount ?? 0),
      items: (order.items as Array<Record<string, unknown>> ?? []).map(i => ({
        id: String(i.id ?? ""),
        product_id: String(i.product_id ?? ""),
        product_title: String(i.product_title ?? ""),
        spec_name: i.spec_name as string | null ?? null,
        quantity: Number(i.quantity ?? 0),
        unit_price: Number(i.unit_price ?? 0),
        subtotal: Number(i.subtotal ?? 0),
      })),
    },
    payment: {
      order_id: String(order.order_no ?? order.id ?? ""),
      payment_url: String(order.payment_url ?? ""),
      qrcode_url: order.qrcode_url as string | undefined,
      pay_url: order.payment_url as string | undefined,
      expires_at: String(order.expires_at ?? ""),
      wallet_address: String(order.payment_method ?? "").startsWith("usdt_") ? order.qrcode_url as string | undefined : undefined,
      crypto_amount: order.usdt_crypto_amount ? String(order.usdt_crypto_amount) : undefined,
      chain: order.usdt_chain ? String(order.usdt_chain).toUpperCase() : undefined,
    },
  }
}

export const orderApi = {
  create: async (data: CreateOrderRequest) => {
    const body = {
      email: data.email,
      payment_method: data.payment_method,
      idempotency_key: data.idempotency_key,
      items: [{
        product_id: Number(data.product_id),
        spec_id: data.spec_id ? Number(data.spec_id) : null,
        quantity: data.quantity,
      }],
    }
    const order = await request<Record<string, unknown>>("/store/orders", { method: "POST", body: JSON.stringify(body) })
    return adaptOrderResponse(order)
  },
  createFromCart: async (data: CreateCartOrderRequest) => {
    const order = await request<Record<string, unknown>>("/store/orders/from-cart", { method: "POST", body: JSON.stringify({
      email: data.email,
      payment_method: data.payment_method,
      idempotency_key: data.idempotency_key,
      device: data.device,
    }) })
    return adaptOrderResponse(order)
  },
  getStatus: async (orderId: string) => {
    const result = await request<{ order_id: string; status: string; expires_at: string; remaining_seconds: number; payment_url?: string }>(`/store/orders/${orderId}/status`)
    return { ...result, status: normalizeOrderStatus(result.status) }
  },
  refreshStatus: async (orderId: string) => {
    const result = await request<{ status: string }>(`/store/orders/${orderId}/refresh`, { method: "POST" })
    return { status: normalizeOrderStatus(result.status) }
  },
  query: async (data: { order_ids?: string[]; emails?: string[] }) => {
    const results = await request<Array<Record<string, unknown>>>("/store/orders/query", { method: "POST", body: JSON.stringify(data) })
    return results.map(r => ({
      id: String(r.order_no ?? r.id ?? ""),
      total_amount: Number(r.total_amount ?? 0),
      actual_amount: Number(r.actual_amount ?? 0),
      status: normalizeOrderStatus(r.status),
      order_type: String(r.order_type ?? "DIRECT").toUpperCase(),
      payment_method: String(r.payment_method ?? ""),
      created_at: String(r.created_at ?? ""),
      email: String(r.email ?? ""),
      points_deducted: Number(r.points_deducted ?? 0),
      points_discount: Number(r.points_discount ?? 0),
      expires_at: String(r.expires_at ?? ""),
      paid_at: r.paid_at as string | null ?? null,
      delivered_at: r.delivered_at as string | null ?? null,
      usdt_tx_id: r.usdt_tx_id as string | undefined,
      txid_review_status: r.txid_review_status as string | undefined,
      txid_review_reason: r.txid_review_reason as string | undefined,
      items: (r.items as Array<Record<string, unknown>> ?? []).map(i => ({
        id: String(i.id ?? ""),
        product_id: String(i.product_id ?? ""),
        product_title: String(i.product_title ?? ""),
        spec_name: i.spec_name as string | null ?? null,
        quantity: Number(i.quantity ?? 0),
        unit_price: Number(i.unit_price ?? 0),
        subtotal: Number(i.subtotal ?? 0),
      })),
      card_keys: (r.card_keys as Array<Record<string, unknown>> ?? []).map(k => ({
        id: String(k.id ?? ""),
        content: String(k.content ?? ""),
        product_title: k.product_title as string | undefined,
        spec_name: k.spec_name as string | null | undefined,
        status: String(k.status ?? ""),
      })),
    })) as QueryOrderResult[]
  },
  deliver: (data: { order_ids: string[] }) =>
    request<DeliverResult[]>("/store/orders/deliver", { method: "POST", body: JSON.stringify(data) }),
  exportKeys: (orderId: string) =>
    request<string>(`/store/orders/${orderId}/export`),
  submitTxid: (orderId: string, txid: string) =>
    request<TxidVerifyResult>("/store/usdt/submit-txid", {
      method: "POST",
      body: JSON.stringify({ order_no: orderId, txid, chain: "TRC20" }),
    }),
  repay: async (orderId: string, device?: string) => {
    const result = await request<Record<string, unknown>>(`/store/orders/${orderId}/repay`, {
      method: "POST",
      body: JSON.stringify({ device }),
    })
    return {
      order_id: String(result.order_id ?? orderId),
      payment_url: String(result.payment_url ?? ""),
      qrcode_url: result.qrcode_url as string | undefined,
      pay_url: result.pay_url as string | undefined,
      expires_at: String(result.expires_at ?? ""),
      wallet_address: result.wallet_address as string | undefined,
      crypto_amount: result.crypto_amount as string | undefined,
      chain: result.chain as string | undefined,
    }
  },
}

// ============================================================
// Site Config (public)
// ============================================================

export const siteApi = {
  getConfig: () =>
    request<SiteConfig>("/store/config"),
}

// ============================================================
// Payment Channels (public, for store display)
// ============================================================

export const paymentApi = {
  getChannels: () =>
    request<PaymentChannelItem[]>("/store/payment-channels"),
}

// ============================================================
// Currencies (public)
// ============================================================

export const currencyApi = {
  getList: () =>
    request<CurrencyItem[]>("/store/currencies"),
}

// ============================================================
// Admin Dashboard
// ============================================================

export const adminDashboardApi = {
  getStats: async () => {
    const raw = await request<Record<string, unknown>>("/admin/faka/dashboard/stats")
    return {
      today_sales: Number(raw.today_sales ?? 0),
      month_sales: Number(raw.month_sales ?? 0),
      today_orders: Number(raw.today_orders ?? 0),
      month_orders: Number(raw.month_orders ?? 0),
      conversion_rate: Number(raw.conversion_rate ?? 0),
      today_pv: Number(raw.today_pv ?? 0),
      today_uv: Number(raw.today_uv ?? 0),
      low_stock_products: (raw.low_stock_products as Array<Record<string, unknown>> ?? []).map(p => ({
        product_id: String(p.product_id ?? ""),
        title: String(p.title ?? ""),
        available_stock: Number(p.available_stock ?? 0),
        threshold: Number(p.threshold ?? 0),
      })),
    } as DashboardStats
  },
  getSalesTrend: async (params: { period?: string; start_date?: string; end_date?: string; days?: number }) => {
    // 后端只接受 days 参数，将 period 转换为 days
    let days = params.days ?? 7
    if (params.period === "month") days = 30
    else if (params.period === "quarter") days = 90
    else if (params.period === "year") days = 365
    const raw = await request<Array<Record<string, unknown>>>(`/admin/faka/dashboard/sales-trend?days=${days}`)
    return raw.map(r => ({
      date: String(r.date ?? ""),
      sales_amount: Number(r.sales_amount ?? 0),
      order_count: Number(r.order_count ?? 0),
    })) as SalesTrend[]
  },
}

// ============================================================
// Admin Product
// ============================================================

export const adminProductApi = {
  getList: (params: { page?: number; page_size?: number; category_id?: string; is_enabled?: number; keyword?: string }) => {
    const qs = buildQuery(params)
    return request<PaginatedData<ProductDetail>>(`/admin/faka/products?${qs}`)
  },
  getDetail: (id: string) =>
    request<ProductDetail>(`/admin/faka/products/${id}`),
  create: (data: {
    title: string; description?: string; detail_md?: string; cover_url?: string;
    base_price: number; category_id: string; low_stock_threshold?: number;
    wholesale_enabled?: boolean; is_enabled?: boolean; sort_order?: number;
    currency?: string; spec_enabled?: boolean; initial_sales?: number; delivery_type?: string
  }) =>
    request<ProductDetail>("/admin/faka/products", { method: "POST", body: JSON.stringify(data) }),
  update: (id: string, data: Partial<{
    title: string; description: string; detail_md: string; cover_url: string;
    base_price: number; category_id: string; low_stock_threshold: number;
    wholesale_enabled: boolean; is_enabled: boolean; sort_order: number;
    currency: string; spec_enabled: boolean; initial_sales: number; delivery_type: string
  }>) =>
    request<null>(`/admin/faka/products/${id}`, { method: "PUT", body: JSON.stringify(data) }),
  delete: (id: string) =>
    request<null>(`/admin/faka/products/${id}`, { method: "DELETE" }),
  // Specs
  getSpecs: (productId: string) =>
    request<ProductSpec[]>(`/admin/faka/products/${productId}/specs`),
  addSpec: (productId: string, data: { name: string; price: number; is_visible?: boolean; sort_order?: number }) =>
    request<ProductSpec>(`/admin/faka/products/${productId}/specs`, { method: "POST", body: JSON.stringify(data) }),
  updateSpec: (productId: string, specId: string, data: Partial<{ name: string; price: number; is_visible: boolean; sort_order: number }>) =>
    request<null>(`/admin/faka/products/${productId}/specs/${specId}`, { method: "PUT", body: JSON.stringify(data) }),
  deleteSpec: (productId: string, specId: string) =>
    request<null>(`/admin/faka/products/${productId}/specs/${specId}`, { method: "DELETE" }),
  // Wholesale rules
  getWholesaleRules: (productId: string) =>
    request<WholesaleRule[]>(`/admin/faka/products/${productId}/wholesale-rules`),
  setWholesaleRules: (productId: string, data: { spec_id?: string | null; rules: { min_quantity: number; unit_price: number }[] }) =>
    request<null>(`/admin/faka/products/${productId}/wholesale-rules`, { method: "POST", body: JSON.stringify(data) }),
  // Image upload
  uploadImage: (file: File) => {
    const formData = new FormData()
    formData.append("file", file)
    return uploadRequest<{ url: string }>("/upload/image", formData)
  },
}

// ============================================================
// Admin Category
// ============================================================

export const adminCategoryApi = {
  getList: () =>
    request<Category[]>("/admin/faka/categories"),
  create: (data: { name: string; sort_order?: number }) =>
    request<null>("/admin/faka/categories", { method: "POST", body: JSON.stringify(data) }),
  update: (id: string, data: { name?: string; sort_order?: number }) =>
    request<null>(`/admin/faka/categories/${id}`, { method: "PUT", body: JSON.stringify(data) }),
  delete: (id: string) =>
    request<null>(`/admin/faka/categories/${id}`, { method: "DELETE" }),
}

// ============================================================
// Admin Card Keys
// ============================================================

export const adminCardKeyApi = {
  getList: async (params: { product_id: string; spec_id?: string | null; page?: number; page_size?: number; include_content?: boolean }) => {
    const qs = buildQuery(params)
    const result = await request<{ list: Array<Record<string, unknown>>; pagination: Pagination }>(`/admin/faka/card-keys?${qs}`)
    return {
      list: result.list.map((item) => ({
        id: String(item.id ?? ""),
        content: String(item.content ?? item.content_masked ?? ""),
        content_masked: String(item.content_masked ?? ""),
        status: String(item.status ?? "").toUpperCase() as CardKeyListItem["status"],
        order_id: item.order_no as string | null ?? null,
        created_at: String(item.created_at ?? ""),
        sold_at: item.sold_at as string | null ?? null,
        product_title: item.product_title as string | undefined,
        spec_name: item.spec_name as string | null | undefined,
      })),
      pagination: result.pagination,
    } as PaginatedData<CardKeyListItem>
  },
  getStock: (params?: { product_id?: string; spec_id?: string }) => {
    const qs = buildQuery(params ?? {})
    return request<CardKeyStockSummary[]>(`/admin/faka/card-keys/stock?${qs}`)
  },
  import: async (data: { product_id: string; spec_id?: string | null; content: string }) => {
    // 将换行分隔的卡密内容拆分为数组，过滤空行
    const cardKeys = data.content.split("\n").map((s) => s.trim()).filter((s) => s.length > 0)
    return request<CardImportBatch>("/admin/faka/card-keys/import", {
      method: "POST",
      body: JSON.stringify({
        product_id: data.product_id,
        spec_id: data.spec_id || null,
        card_keys: cardKeys,
      }),
    })
  },
  getImportBatches: (params: { product_id?: string; page?: number; page_size?: number }) => {
    const qs = buildQuery(params)
    return request<PaginatedData<CardImportBatch>>(`/admin/faka/card-keys/import-batches?${qs}`)
  },
  invalidate: (id: string) =>
    request<null>(`/admin/faka/card-keys/${id}/void`, { method: "PUT" }),
  batchInvalidate: (params: { product_id: string; spec_id?: string | null }) => {
    const qs = buildQuery(params)
    return request<{ invalidated_count: number }>(`/admin/faka/card-keys/batch-invalidate?${qs}`, { method: "POST" })
  },
  getByOrder: (orderId: string) =>
    request<OrderCardKey[]>(`/admin/faka/card-keys/by-order/${orderId}`),
}

// ============================================================
// Admin Order
// ============================================================

export const adminOrderApi = {
  getList: async (params: {
    page?: number; page_size?: number; status?: string; order_type?: string;
    payment_method?: string; is_risk_flagged?: number; keyword?: string
  }) => {
    const qs = buildQuery(params)
    const result = await request<{ list: Array<Record<string, unknown>>; pagination: Pagination }>(`/admin/faka/orders?${qs}`)
    return {
      list: result.list.map(r => ({
        ...r,
        status: String(r.status ?? "pending").toUpperCase(),
        total_amount: Number(r.total_amount ?? 0),
        actual_amount: Number(r.actual_amount ?? 0),
        order_type: r.order_type === 1 ? "CART" : "DIRECT",
        payment_method: String(r.payment_method ?? ""),
        created_at: String(r.created_at ?? ""),
      })),
      pagination: result.pagination,
    } as PaginatedData<AdminOrderItem>
  },
  getDetail: async (id: string) => {
    const result = await request<Record<string, unknown>>(`/admin/faka/orders/${id}`)
    return {
      ...result,
      status: String(result.status ?? "pending").toUpperCase(),
    } as AdminOrderItem
  },
  markPaid: (id: string) =>
    request<null>(`/admin/faka/orders/${id}/mark-paid`, { method: "PUT" }),
}

// ============================================================
// Admin User
// ============================================================

export const adminUserApi = {
  getList: (params: { page?: number; page_size?: number; keyword?: string }) => {
    const qs = buildQuery(params)
    return request<PaginatedData<AdminUserItem>>(`/admin/faka/users?${qs}`)
  },
  toggleStatus: (id: string, isActive: boolean) =>
    request<null>(`/admin/faka/users/${id}/toggle`, { method: "PUT", body: JSON.stringify({ IsActive: isActive }) }),
}

// ============================================================
// Admin Payment
// ============================================================

export const adminPaymentApi = {
  getList: () =>
    request<PaymentChannelItem[]>("/admin/faka/payment-channels"),
  create: (data: { channel_code: string; channel_name: string; provider_type: string; config_data?: Record<string, unknown>; is_enabled?: boolean; sort_order?: number }) =>
    request<null>("/admin/faka/payment-channels", { method: "POST", body: JSON.stringify(data) }),
  update: (id: string, data: Partial<{ channel_name: string; provider_type: string; config_data: Record<string, unknown>; is_enabled: boolean; sort_order: number }>) =>
    request<null>(`/admin/faka/payment-channels/${id}`, { method: "PUT", body: JSON.stringify(data) }),
  delete: (id: string) =>
    request<null>(`/admin/faka/payment-channels/${id}`, { method: "DELETE" }),
}

// ============================================================
// Admin Config
// ============================================================

export const adminConfigApi = {
  get: async () => {
    // 后端返回 { key, value }，适配为 { config_key, config_value }
    const raw = await request<Array<Record<string, unknown>>>("/admin/faka/site-config")
    return raw.map(r => ({
      config_key: String(r.key ?? r.config_key ?? ""),
      config_value: String(r.value ?? r.config_value ?? ""),
      config_group: r.provider_name as string | undefined,
    })) as SiteConfigKV[]
  },
  update: (data: { configs: { config_key: string; config_value: string }[] }) => {
    // 后端期望 { configs: [{ key, value }] }
    const body = {
      configs: data.configs.map(c => ({ key: c.config_key, value: c.config_value }))
    }
    return request<null>("/admin/faka/site-config", { method: "PUT", body: JSON.stringify(body) })
  },
  toggleMaintenance: (enabled: boolean) =>
    request<null>("/admin/faka/site-config/maintenance", { method: "POST", body: JSON.stringify({ enabled }) }),
}

// ============================================================
// Admin Log
// ============================================================

export const adminLogApi = {
  getList: (params: { page?: number; page_size?: number; user_id?: string; action?: string; target_type?: string; start_date?: string; end_date?: string }) => {
    const qs = buildQuery(params)
    return request<PaginatedData<OperationLog>>(`/admin/faka/logs?${qs}`)
  },
}

// ============================================================
// Admin Risk
// ============================================================

export const adminRiskApi = {
  getConfig: () =>
    request<RiskConfig>("/admin/faka/risk"),
  updateConfig: (data: Partial<RiskConfig>) =>
    request<null>("/admin/faka/risk", { method: "PUT", body: JSON.stringify(data) }),
  getFlaggedOrders: (params: { page?: number; page_size?: number }) => {
    const qs = buildQuery(params)
    return request<PaginatedData<AdminOrderItem>>(`/admin/faka/risk/flagged-orders?${qs}`)
  },
}

// ============================================================
// Admin TXID Review
// ============================================================

export const adminTxidReviewApi = {
  getList: (params: { status?: string; page?: number; page_size?: number }) => {
    const qs = buildQuery(params)
    return request<PaginatedData<import("@/types").UnmatchedTransaction>>(`/admin/faka/txid-reviews/pending?${qs}`)
  },
  approve: (id: string) =>
    request<null>(`/admin/faka/txid-reviews/${id}/confirm`, { method: "PUT" }),
  reject: (id: string, reason: string) =>
    request<null>(`/admin/faka/txid-reviews/${id}/reject`, {
      method: "PUT",
      body: JSON.stringify({ reason }),
    }),
}

// ApiError is exported inline with the class declaration

// ============================================================
// Error code → i18n key mapping
// ============================================================

// 只映射用户可见的前台错误码，后台管理页面不映射（后台统一中文界面）
const ERROR_CODE_I18N: Record<number, string> = {
  // 通用
  10002: "error.unauthorized",
  10003: "error.forbidden",
  10005: "error.tooManyRequests",
  10006: "error.serverError",
  10007: "error.maintenance",
  // Auth
  20001: "error.usernameExists",
  20002: "error.emailExists",
  20003: "error.captchaInvalid",
  20004: "error.invalidCredentials",
  20005: "error.oldPasswordWrong",
  20006: "error.accountDisabled",
  // Product
  30001: "error.productNotFound",
  30002: "error.insufficientStock",
  30003: "error.specNotFound",
  30004: "error.purchaseLimitExceeded",
  30005: "error.specHasCardKeys",
  30006: "error.specNameDuplicate",
  // Order
  40001: "error.orderNotFound",
  40002: "error.orderExpired",
  40003: "error.orderNotPaid",
  40004: "error.orderOutOfStock",
  40005: "error.orderProcessing",
  40006: "error.purchaseLimitPerUser",
  40007: "error.unpaidOrderExists",
  40008: "error.cartEmpty",
  // Payment
  50001: "error.channelUnavailable",
  50003: "error.txidInvalidFormat",
  50004: "error.txidAlreadyUsed",
  50005: "error.txidVerifyFailed",
  50006: "error.orderNotUsdt",
}

/**
 * 从 API 错误中提取用户可见的提示文案。
 * 已映射 → i18n 文案 + 参数插值
 * 未映射 → 后端原始 message（兜底，不丢信息）
 * 非 ApiError → 原始 error message
 */
export function getApiErrorMessage(err: unknown, t: (key: any) => string): string {
  if (err instanceof ApiError) {
    const i18nKey = ERROR_CODE_I18N[err.code]
    if (i18nKey) {
      let msg = t(i18nKey)
      // 参数插值: {available} → 实际值
      if (err.params) {
        for (const [k, v] of Object.entries(err.params)) {
          msg = msg.replace(`{${k}}`, String(v))
        }
      }
      return msg
    }
    // 未映射的 code → 直接返回后端原始 message（兜底，不丢信息）
    return err.message
  }
  return err instanceof Error ? err.message : t("common.error")
}
