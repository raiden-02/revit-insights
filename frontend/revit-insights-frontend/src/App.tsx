import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query'
import CategoryBarChart from './components/CategoryBarChart'
import Category3D from './components/Category3D'
const qc = new QueryClient()

function useLatest() {
  return useQuery({
    queryKey: ['latest'],
    queryFn: async () => {
      const r = await fetch('http://localhost:5245/api/modeldata/latest')
      if (!r.ok) throw new Error('No data yet')
      return r.json()
    },
    retry: false
  })
}

function AppInner() {
  const { data, isLoading, error } = useLatest()
  return (
    <div className="min-h-screen p-6 grid gap-4 lg:grid-cols-2">
      <div className="card">
        <h1 className="text-2xl font-semibold mb-2">Revit Element Insights</h1>
        {isLoading && <div>Loading…</div>}
        {error && <div className="text-red-600">No data yet — export from Revit.</div>}
        {data && (
          <div className="space-y-1">
            <div><b>Project:</b> {data.projectName}</div>
            <div><b>Revit:</b> {data.revitVersion}</div>
            <div><b>Timestamp:</b> {new Date(data.timestampUtc).toLocaleString()}</div>
          </div>
        )}
      </div>

      <div className="card">
        <h2 className="text-xl font-semibold mb-2">Elements by Category</h2>
        {data ? <CategoryBarChart data={data.categories} /> : <div className="text-slate-500">No data yet — export from Revit.</div>}
      </div>

      <div className="card">
        <h2 className="text-xl font-semibold mb-2">3D Category Bars</h2>
        {data ? <Category3D data={data.categories} /> : <div className="text-slate-500">No data yet — export from Revit.</div>}
      </div>
    </div>
  )
}

export default function App() {
  return (
    <QueryClientProvider client={qc}>
      <AppInner />
    </QueryClientProvider>
  )
}
