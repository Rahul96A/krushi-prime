import { safeStorage } from './api'
import { createContext, useContext, useState, type ReactNode } from 'react'

type Lang = 'en' | 'mr'

const dict: Record<string, [string, string]> = {
  dashboard: ['Dashboard', 'डॅशबोर्ड'],
  billing: ['New Bill', 'नवीन बिल'],
  sales: ['Invoices', 'बिले'],
  products: ['Products', 'उत्पादने'],
  stock: ['Stock & Expiry', 'स्टॉक व एक्सपायरी'],
  customers: ['Farmers', 'शेतकरी ग्राहक'],
  udhari: ['Udhari', 'उधारी'],
  onlinePayments: ['Online Payments', 'ऑनलाइन पेमेंट'],
  suppliers: ['Suppliers', 'पुरवठादार'],
  purchases: ['Purchases', 'खरेदी'],
  reports: ['Reports', 'अहवाल'],
  settings: ['Settings', 'सेटिंग्ज'],
  admin: ['Admin Console', 'ॲडमिन'],
  logout: ['Logout', 'बाहेर पडा'],
  todaySales: ['Today\'s Sales', 'आजची विक्री'],
  todayCollection: ['Today\'s Collection', 'आजची वसुली'],
  monthSales: ['This Month', 'या महिन्याची विक्री'],
  totalUdhari: ['Total Udhari', 'एकूण उधारी'],
  lowStock: ['Low Stock', 'कमी स्टॉक'],
  expiring: ['Expiring ≤30 days', '३० दिवसांत एक्सपायरी'],
  save: ['Save', 'जतन करा'],
  saveBill: ['Save Bill', 'बिल जतन करा'],
  search: ['Search', 'शोधा'],
  customer: ['Customer', 'ग्राहक'],
  total: ['Total', 'एकूण'],
  paid: ['Paid', 'जमा'],
  balance: ['Balance', 'बाकी'],
  addProduct: ['Add Product', 'उत्पादन जोडा'],
  addCustomer: ['Add Farmer', 'शेतकरी जोडा'],
  receivePayment: ['Receive Payment', 'रक्कम जमा करा'],
  sendReminder: ['WhatsApp Reminder', 'व्हॉट्सॲप आठवण'],
  trialLeft: ['days free trial left', 'दिवस मोफत ट्रायल बाकी'],
}

const Ctx = createContext<{ lang: Lang; setLang: (l: Lang) => void; t: (k: string) => string }>(null!)

export function I18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>(() => (safeStorage.getItem('kp.lang') as Lang) || 'en')
  const setLang = (l: Lang) => { safeStorage.setItem('kp.lang', l); setLangState(l) }
  const t = (k: string) => dict[k]?.[lang === 'en' ? 0 : 1] ?? k
  return <Ctx.Provider value={{ lang, setLang, t }}>{children}</Ctx.Provider>
}

export const useI18n = () => useContext(Ctx)
