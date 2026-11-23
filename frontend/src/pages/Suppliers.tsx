import { useState, type FormEvent } from 'react'
import { api } from '../lib/api'
import type { Ledger, Supplier } from '../lib/types'
import { inr } from '../lib/format'
import { useI18n } from '../lib/i18n'
import { Button, Card, Empty, Field, Modal, PageHeader, Spinner, useLoad, useToast } from '../components/ui'
import { LedgerView, PaymentForm } from './Customers'

export default function Suppliers() {
  const { t } = useI18n()
  const toast = useToast()
  const { data, loading, reload } = useLoad(() => api.get<Supplier[]>('/api/suppliers'))
  const [edit, setEdit] = useState<Partial<Supplier> | null>(null)
  const [pay, setPay] = useState<Supplier | null>(null)
  const [ledgerFor, setLedgerFor] = useState<Supplier | null>(null)
  const ledger = useLoad(() => (ledgerFor ? api.get<Ledger>(`/api/suppliers/${ledgerFor.id}/ledger`) : Promise.resolve(null)), [ledgerFor?.id])

  const save = async (e: FormEvent) => {
    e.preventDefault()
    try {
      const body = { name: edit?.name, mobile: edit?.mobile || null, gstin: edit?.gstin || null, address: edit?.address || null, isActive: true }
      edit?.id ? await api.put(`/api/suppliers/${edit.id}`, body) : await api.post('/api/suppliers', body)
      toast.ok('Supplier saved'); setEdit(null); reload()
    } catch (err) { toast.err(err) }
  }

  return (
    <div>
      <PageHeader title={`🏭 ${t('suppliers')}`} actions={<Button onClick={() => setEdit({})}>+ Add supplier</Button>} />
      <Card>
        {loading ? <Spinner /> : !data?.length ? <Empty /> : (
          <div className="-mx-4 overflow-x-auto">
            <table className="tbl">
              <thead><tr><th>Supplier</th><th>GSTIN</th><th>Address</th><th className="num">Payable</th><th /></tr></thead>
              <tbody>{data.map(s => (
                <tr key={s.id}>
                  <td><button className="font-semibold text-brand-800 hover:underline" onClick={() => setLedgerFor(s)}>{s.name}</button><div className="text-xs text-stone-500">{s.mobile}</div></td>
                  <td className="text-xs">{s.gstin}</td><td className="text-xs">{s.address}</td>
                  <td className={`num font-bold ${s.balance > 0 ? 'text-amber-700' : ''}`}>{inr(s.balance)}</td>
                  <td className="whitespace-nowrap text-right">
                    <Button variant="ghost" className="px-2 py-1" onClick={() => setPay(s)}>Pay</Button>
                    <Button variant="ghost" className="px-2 py-1" onClick={() => setEdit(s)}>Edit</Button>
                  </td>
                </tr>))}</tbody>
            </table>
          </div>
        )}
      </Card>
      <Modal open={!!edit} onClose={() => setEdit(null)} title={edit?.id ? 'Edit supplier' : 'Add supplier'}>
        {edit && (
          <form onSubmit={save} className="space-y-3">
            <Field label="Name *"><input className="input" required value={edit.name ?? ''} onChange={e => setEdit({ ...edit, name: e.target.value })} /></Field>
            <div className="grid grid-cols-2 gap-3">
              <Field label="Mobile"><input className="input" value={edit.mobile ?? ''} onChange={e => setEdit({ ...edit, mobile: e.target.value })} /></Field>
              <Field label="GSTIN"><input className="input uppercase" value={edit.gstin ?? ''} onChange={e => setEdit({ ...edit, gstin: e.target.value })} /></Field>
            </div>
            <Field label="Address"><input className="input" value={edit.address ?? ''} onChange={e => setEdit({ ...edit, address: e.target.value })} /></Field>
            <div className="flex justify-end"><Button>Save</Button></div>
          </form>
        )}
      </Modal>
      <Modal open={!!pay} onClose={() => setPay(null)} title={`Pay supplier – ${pay?.name}`}>
        {pay && <PaymentForm partyType="Supplier" partyId={pay.id} due={pay.balance} onDone={() => { setPay(null); reload() }} />}
      </Modal>
      <Modal open={!!ledgerFor} onClose={() => setLedgerFor(null)} title={`Ledger – ${ledgerFor?.name}`} wide>
        {ledger.loading || !ledger.data ? <Spinner /> : <LedgerView ledger={ledger.data} />}
      </Modal>
    </div>
  )
}
