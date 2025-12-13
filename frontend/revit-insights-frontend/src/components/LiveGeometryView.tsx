import { Canvas } from "@react-three/fiber";
import { Environment, OrbitControls } from "@react-three/drei";
import { Suspense, useMemo } from "react";

export type GeometryPrimitive = {
    category: string
    centerX: number
    centerY: number
    centerZ: number
    sizeX: number
    sizeY: number
    sizeZ: number
}

export type GeometrySnapshot = {
    projectName: string
    timestampUtc: string
    primitives: GeometryPrimitive[]
}

function colorForCategory(category: string): string {
    const key = category.toLowerCase()
    if (key.includes('wall')) return '#4ade80'
    if (key.includes('column')) return '#60a5fa'
    if (key.includes('floor')) return '#f97316'
    return '#e5e7eb'
}

function PrimitiveBox({ primitive }: { primitive: GeometryPrimitive }) {
    const color = colorForCategory(primitive.category);
    const position: [number, number, number] = [
        primitive.centerX,
        primitive.centerY,
        primitive.centerZ
    ]; // swapped y and z to match Three.js coordinate system
    
    const scale: [number, number, number] = [
        primitive.sizeX,
        primitive.sizeY,
        primitive.sizeZ
    ];

    return (
        <mesh position={position} scale={scale} castShadow receiveShadow>
            <boxGeometry args={[1, 1, 1]} />
            <meshStandardMaterial color={color} metalness={0.1} roughness={0.7} />
        </mesh>
    )
}

export function LiveGeometryView({ snapshot }: { snapshot: GeometrySnapshot }) {
    const count = snapshot.primitives?.length ?? 0;

    const bounds = useMemo(() => {
        if (!snapshot.primitives?.length) return null;
        let minX = Infinity, maxX = -Infinity;
        let minY = Infinity, maxY = -Infinity;
        let minZ = Infinity, maxZ = -Infinity;

        for (const primitive of snapshot.primitives) {
            const x0 = primitive.centerX - primitive.sizeX / 2;
            const x1 = primitive.centerX + primitive.sizeX / 2;
            const y0 = primitive.centerY - primitive.sizeY / 2;
            const y1 = primitive.centerY + primitive.sizeY / 2;
            const z0 = primitive.centerZ - primitive.sizeZ / 2;
            const z1 = primitive.centerZ + primitive.sizeZ / 2;

            if (x0 < minX) minX = x0;
            if (x1 > maxX) maxX = x1;
            if (y0 < minY) minY = y0;
            if (y1 > maxY) maxY = y1;
            if (z0 < minZ) minZ = z0;
            if (z1 > maxZ) maxZ = z1;
        }

        const sizeX = maxX - minX;
        const sizeY = maxY - minY;
        const sizeZ = maxZ - minZ;

        const center: [number, number, number] = [
            (minX + maxX) / 2,
            (minY + maxY) / 2,
            (minZ + maxZ) / 2
        ];

        return { center, size: Math.max(sizeX, sizeY, sizeZ) };
    }, [snapshot.primitives]);

    const cameraPosition: [number, number, number] = useMemo(() => {
        if (!bounds) return [0, 50, 150];
        const d = bounds.size * 1.5;
        return [bounds.center[0], bounds.center[1], bounds.center[2] + d];
    }, [bounds]);

    return (
        <div className="w-full min-w-0 h-[360px]">
        <Canvas
            shadows
            camera={{ position: cameraPosition, fov: 40 }}
        >
            <color attach="background" args={['#020617']} />
            <hemisphereLight intensity={0.6} groundColor="#020617" />
            <directionalLight position={[50, 80, 40]} intensity={1.2} castShadow />

            <Suspense fallback={null}>
            {snapshot.primitives?.map((primitive, index) => (
                <PrimitiveBox key={index} primitive={primitive} />
            ))}
            <Environment preset="city" />
            </Suspense>

            <OrbitControls enablePan enableZoom />
        </Canvas>

        <div className="mt-2 text-xs text-slate-400 flex justify-between">
            <span>
                Primitives: {count} • Project: {snapshot.projectName}
            </span>
            <span>
                Snapshot: {new Date(snapshot.timestampUtc).toLocaleTimeString()}
            </span>
        </div>
        </div>
    )
}
