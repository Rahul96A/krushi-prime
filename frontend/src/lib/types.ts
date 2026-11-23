export type Role = 'SuperAdmin' | 'Owner' | 'Manager' | 'Cashier'
export type PaymentMode = 'Cash' | 'Upi' | 'Card' | 'Bank' | 'Credit' | 'Mixed' | 'Cheque'
export type Category = 'Fertilizer' | 'Seed' | 'Pesticide' | 'Herbicide' | 'Fungicide' | 'GrowthPromoter' | 'Equipment' | 'Other'
export const CATEGORIES: Category[] = ['Fertilizer', 'Seed', 'Pesticide', 'Herbicide', 'Fungicide', 'GrowthPromoter', 'Equipment', 'Other']
export const UNITS = ['Bag', 'Kg', 'Ltr', 'Ml', 'Btl', 'Pkt', 'Nos', 'Gm']
export const GST_RATES = [0, 5, 12, 18, 28]

export interface TenantSummary { id: number; shopName: string; plan: string; subscriptionEnd: string; daysLeft: number; isActive: boolean; isExpired: boolean }
export interface UserInfo { id: number; fullName: string; username: string; role: Role; tenant?: TenantSummary }
export interface AuthResponse { token: string; expiresAt: string; user: UserInfo }

export interface Batch { id: number; batchNo: string; mfgDate?: string; expiryDate?: string; quantity: number; purchasePrice: number; salePrice: number; mrp: number }
export interface Product {
  id: number; name: string; localName?: string; category: Category; company?: string; hsnCode?: string; gstRate: number; unit: string; packSize?: string
  barcode?: string; salePrice: number; mrp: number; purchasePrice: number; reorderLevel: number; trackExpiry: boolean; isActive: boolean
  stock: number; nearestExpiry?: string; batches?: Batch[]
}
export interface Customer {
  id: number; name: string; mobile?: string; village?: string; address?: string; gstin?: string; openingBalance: number; balance: number
  creditLimit: number; landAcres?: number; crops?: string; isActive: boolean
}
export interface Supplier { id: number; name: string; mobile?: string; gstin?: string; address?: string; balance: number; isActive: boolean }

export interface SaleListItem {
  id: number; invoiceNo: string; invoiceDate: string; customerName: string; customerMobile?: string; isGst: boolean
  grandTotal: number; paidAmount: number; balanceAmount: number; paymentMode: PaymentMode; status: string; itemCount: number
}
export interface InvoiceItem {
  id: number; productId: number; productName: string; hsnCode?: string; batchNo?: string; expiryDate?: string; unit: string; quantity: number
  rate: number; discountAmount: number; taxableValue: number; gstRate: number; cgst: number; sgst: number; igst: number; total: number
}
export interface Invoice {
  id: number; invoiceNo: string; invoiceDate: string; customerId?: number; customerName: string; customerMobile?: string; customerVillage?: string
  customerGstin?: string; isGst: boolean; isInterState: boolean; subTotal: number; discount: number; cgstAmount: number; sgstAmount: number
  igstAmount: number; roundOff: number; grandTotal: number; paidAmount: number; balanceAmount: number; paymentMode: PaymentMode; status: string
  notes?: string; cancelReason?: string; items: InvoiceItem[]
}
export interface ShopSettings {
  shopName: string; ownerName: string; mobile: string; email?: string; addressLine?: string; village?: string; taluka?: string; district?: string
  state: string; stateCode: string; pincode?: string; gstin?: string; isGstRegistered: boolean; fertilizerLicenseNo?: string
  pesticideLicenseNo?: string; seedLicenseNo?: string; upiId?: string; invoicePrefix: string; invoiceFooter?: string
  pricesIncludeTax: boolean; allowNegativeStock: boolean
}
export interface NameValue { name: string; quantity: number; amount: number }
export interface DayValue { date: string; sales: number; bills: number }
export interface Dashboard {
  todaySales: number; todayBills: number; todayCollection: number; monthSales: number; monthProfit: number; totalUdhari: number
  udhariCustomers: number; lowStockCount: number; expiringSoonCount: number; expiredCount: number; stockValue: number
  last7Days: DayValue[]; topProducts: NameValue[]; categorySales: NameValue[]; recentInvoices: SaleListItem[]
}
export interface LedgerEntry { date: string; type: string; reference: string; debit: number; credit: number; balance: number }
export interface Ledger { partyId: number; name: string; mobile?: string; openingBalance: number; closingBalance: number; entries: LedgerEntry[] }
export interface WhatsAppResult { mode: string; sent: boolean; url?: string; message: string; error?: string }
export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number }

export interface PaymentLink {
  id: number; publicId: string; purpose: 'Udhari' | 'Invoice' | 'Subscription'; provider: 'Simulator' | 'Cashfree' | 'NttData' | 'UpiDirect'; liveMode: boolean
  payerName: string; payerMobile?: string; description: string; amount: number; amountPaid: number
  status: 'Created' | 'Paid' | 'PartiallyPaid' | 'Failed' | 'Expired' | 'Cancelled'; payUrl: string; whatsAppUrl?: string
  createdAt: string; paidAt?: string; expiresAt?: string; lastMessage?: string; customerId?: number; salesInvoiceId?: number; upiUri?: string
}
export type ProviderName = 'None' | 'Simulator' | 'Cashfree' | 'NttData' | 'UpiDirect'
export interface PaymentSettings {
  provider: ProviderName; liveMode: boolean; cashfreeAppId?: string; cashfreeSecret?: string; nttMerchId?: string; nttPassword?: string
  nttProductId?: string; nttRequestKey?: string; nttResponseKey?: string; webhookUrl?: string
}
export interface PlanOption { plan: string; months: number; price: number; label: string }
