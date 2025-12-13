import { useQuery } from "@tanstack/react-query";
import type { GeometrySnapshot } from "../components/LiveGeometryView";

export function useLatestGeometry(projectName?: string) {
    return useQuery<GeometrySnapshot, Error>({
        queryKey: ['geometry-latest', projectName],
        enabled: true,
        queryFn: async () => {
            const baseUrl = `http://localhost:5245/api/geometry/latest`;
            const url = projectName
                ? `${baseUrl}?projectName=${encodeURIComponent(projectName)}`
                : baseUrl;

            let response = await fetch(url);

            // If the UI is pointing at a different project than the last geometry export,
            // fall back to the latest geometry snapshot across all projects.
            if (response.status === 404 && projectName) {
                response = await fetch(baseUrl);
            }

            if (!response.ok)
                throw new Error(await response.text());

            return response.json();
        },

        // Poll every 3s so repeated exports show up automatically
        refetchInterval: 3000,
        retry: false,
    });
}