import { Canvas } from "@react-three/fiber";
import { Environment } from "@react-three/drei";
import { Suspense, useMemo, useState } from "react";
import * as THREE from "three";
import { ViewCube, type ViewPreset } from "./ViewCube";
import { CameraControls, type SceneBounds } from "./CameraControls";

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
        primitive.centerZ, // Revit Z (up) -> Three Y (up)
        -primitive.centerY, // Revit Y -> Three -Z (flip for expected orientation)
    ];
    
    const scale: [number, number, number] = [
        primitive.sizeX,
        primitive.sizeZ, // Revit Z -> Three Y
        primitive.sizeY, // Revit Y -> Three Z
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
    const [viewPreset, setViewPreset] = useState<ViewPreset>("iso");
    const [presetNonce, setPresetNonce] = useState(0);

    const bounds: SceneBounds = useMemo(() => {
        if (!snapshot.primitives?.length) return null;
        let minX = Infinity, maxX = -Infinity;
        let minY = Infinity, maxY = -Infinity;
        let minZ = Infinity, maxZ = -Infinity;

        for (const primitive of snapshot.primitives) {
            // Convert Revit coords (X,Y,Z up) -> Three coords (X,Y up,Z)
            const cx = primitive.centerX;
            const cy = primitive.centerZ;
            const cz = -primitive.centerY;
            const sx = primitive.sizeX;
            const sy = primitive.sizeZ;
            const sz = primitive.sizeY;

            const x0 = cx - sx / 2;
            const x1 = cx + sx / 2;
            const y0 = cy - sy / 2;
            const y1 = cy + sy / 2;
            const z0 = cz - sz / 2;
            const z1 = cz + sz / 2;

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

    const cameraDistance = useMemo(() => {
        if (!bounds) return 150;
        return Math.max(30, bounds.size * 1.6);
    }, [bounds]);

    const target = useMemo(() => {
        if (!bounds) return new THREE.Vector3(0, 0, 0);
        return new THREE.Vector3(bounds.center[0], bounds.center[1], bounds.center[2]);
    }, [bounds]);

    return (
        <div className="fixed inset-0 bg-slate-950 text-slate-100">
            {/* HUD */}
            <div className="absolute left-4 top-4 z-10 rounded-xl bg-slate-950/70 backdrop-blur border border-slate-800 px-3 py-2 text-sm shadow-lg">
                <div className="font-semibold">Revit Geometry Stream</div>
                <div className="text-xs text-slate-300">
                    Project: <b>{snapshot.projectName}</b> • Primitives: <b>{count}</b> • {new Date(snapshot.timestampUtc).toLocaleTimeString()}
                </div>
                <div className="mt-2 text-[11px] text-slate-400">
                    LMB: Orbit • MMB: Pan • Scroll: Zoom • RMB+Mouse: Look • RMB+WASD: Fly
                </div>
            </div>

            <div className="absolute right-4 top-4 z-10">
                <ViewCube onSelect={(p) => { setViewPreset(p); setPresetNonce(n => n + 1); }} />
            </div>

            <Canvas
                shadows
                style={{ width: "100%", height: "100%" }}
                camera={{ position: [0, 80, cameraDistance], fov: 55, near: 0.1, far: 100000 }}
            >
                <color attach="background" args={["#020617"]} />
                <hemisphereLight intensity={0.6} groundColor="#020617" />
                <directionalLight position={[50, 80, 40]} intensity={1.2} castShadow />

                <CameraControls
                    target={target}
                    bounds={bounds}
                    cameraDistance={cameraDistance}
                    viewPreset={viewPreset}
                    presetNonce={presetNonce}
                />

                <Suspense fallback={null}>
                    {snapshot.primitives?.map((primitive, index) => (
                        <PrimitiveBox key={index} primitive={primitive} />
                    ))}
                    <Environment preset="city" />
                </Suspense>
            </Canvas>
        </div>
    );
}
