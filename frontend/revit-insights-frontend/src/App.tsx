import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { LiveGeometryView } from './components/LiveGeometryView'
import { useLatestGeometry } from './hooks/useLatestGeometry'
const qc = new QueryClient()

function AppInner() {
  const geometryQuery = useLatestGeometry()
  return (
    <div className="min-h-screen p-6 grid gap-4">
      <div className="card">
        <h1 className="text-2xl font-semibold mb-2">Revit Geometry Stream</h1>
        <div className="text-sm text-slate-500 mb-3">
          Click <b>Generate Column Grid</b> (optional) to quickly create geometry, then click <b>Export Geometry</b> in Revit.
        </div>
        {geometryQuery.isLoading && (
          <div className="text-slate-500 text-sm">
            Waiting for geometry export from Revit...
          </div>
        )}

        {geometryQuery.isError && (
          <div className="text-red-600 text-sm">
            No geometry yet. Use the <b>Export Geometry</b> button in Revit.
          </div>
        )}

        {geometryQuery.data && (
          <>
            <div className="mb-2 text-xs text-slate-500">
              Showing geometry for <b>{geometryQuery.data.projectName}</b> • {new Date(geometryQuery.data.timestampUtc).toLocaleString()}
            </div>
            <LiveGeometryView snapshot={geometryQuery.data} />
          </>
        )}
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
