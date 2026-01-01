import { useQuery } from "@tanstack/react-query";
import type { GeometrySnapshot } from "../components/LiveGeometryView";

export function useLatestGeometry(projectName?: string) {
    return useQuery<GeometrySnapshot, Error>({
        queryKey: ['geometry-latest', projectName],
        
        queryFn: async () => {
            const baseUrl = "http://localhost:5245/api/geometry/latest";
            const url = projectName
                ? `${baseUrl}?projectName=${encodeURIComponent(projectName)}`
                : baseUrl;

            let response = await fetch(url);

            // Fallback to latest across all projects if specific project not found
            if (response.status === 404 && projectName) {
                response = await fetch(baseUrl);
            }

            if (!response.ok) {
                throw new Error(await response.text());
            }

            return response.json();
        },

        // Data fresh until next poll - prevents extra refetches from focus/re-renders
        staleTime: 2000,
        
        // Poll every 2s for real-time updates
        refetchInterval: 2000,
        
        // Don't retry on error - polling is frequent enough
        retry: false,
    });
}