import { Canvas } from "@react-three/fiber";
import { Environment } from "@react-three/drei";
import { Suspense, useMemo, useState, useCallback, useEffect, useRef } from "react";
import * as THREE from "three";
import { ViewCube, type ViewPreset } from "./ViewCube";
import { CameraControls, type SceneBounds } from "./CameraControls";
import { useEnqueueCommand } from "../hooks/useEnqueueCommand";

export type GeometryPrimitive = {
  category: string;
  centerX: number;
  centerY: number;
  centerZ: number;
  sizeX: number;
  sizeY: number;
  sizeZ: number;
};

export type GeometrySnapshot = {
  projectName: string;
  timestampUtc: string;
  primitives: GeometryPrimitive[];
};

function colorForCategory(category: string): string {
  const key = category.toLowerCase();
  if (key.includes("wall")) return "#4ade80";
  if (key.includes("column")) return "#60a5fa";
  if (key.includes("floor")) return "#f97316";
  if (key.includes("webbox") || key.includes("generic")) return "#a855f7"; // Purple for web-created boxes
  return "#e5e7eb";
}

function PrimitiveBox({ primitive, isPending = false }: { primitive: GeometryPrimitive; isPending?: boolean }) {
  const color = colorForCategory(primitive.category);
  const position: [number, number, number] = [
    primitive.centerX,
    primitive.centerZ, // Revit Z (up) -> Three Y (up)
    -primitive.centerY, // Revit Y -> Three -Z
  ];

  const scale: [number, number, number] = [
    primitive.sizeX,
    primitive.sizeZ,
    primitive.sizeY,
  ];

  return (
    <mesh position={position} scale={scale} castShadow receiveShadow>
      <boxGeometry args={[1, 1, 1]} />
      <meshStandardMaterial 
        color={color} 
        metalness={0.1} 
        roughness={0.7}
        transparent={isPending}
        opacity={isPending ? 0.6 : 1}
      />
    </mesh>
  );
}

// Convert Three coords -> Revit coords
function threeToRevit(p: THREE.Vector3) {
  return {
    x: p.x,
    y: -p.z,
    z: p.y,
  };
}

export function LiveGeometryView({ snapshot }: { snapshot: GeometrySnapshot }) {
  const count = snapshot.primitives?.length ?? 0;
  const [viewPreset, setViewPreset] = useState<ViewPreset>("iso");
  const [presetNonce, setPresetNonce] = useState(0);

  const enqueue = useEnqueueCommand();
  const [boxSize, setBoxSize] = useState(10); // feet, demo default
  
  // Optimistic pending boxes - shown immediately in frontend
  const [pendingBoxes, setPendingBoxes] = useState<GeometryPrimitive[]>([]);
  
  // Track snapshot timestamp to clear pending boxes on new export
  const lastTimestampRef = useRef(snapshot.timestampUtc);
  
  // Clear ALL pending boxes when a new snapshot arrives (Revit re-exported)
  useEffect(() => {
    if (snapshot.timestampUtc !== lastTimestampRef.current) {
      lastTimestampRef.current = snapshot.timestampUtc;
      setPendingBoxes([]); // Clear all pending - snapshot is now source of truth
    }
  }, [snapshot.timestampUtc]);

  // Combine snapshot primitives with pending boxes for bounds calculation
  const allPrimitives = useMemo(() => {
    return [...(snapshot.primitives ?? []), ...pendingBoxes];
  }, [snapshot.primitives, pendingBoxes]);

  const bounds: SceneBounds = useMemo(() => {
    if (!allPrimitives.length) return null;

    let minX = Infinity,
      maxX = -Infinity;
    let minY = Infinity,
      maxY = -Infinity;
    let minZ = Infinity,
      maxZ = -Infinity;

    for (const primitive of allPrimitives) {
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
      (minZ + maxZ) / 2,
    ];

    return { center, size: Math.max(sizeX, sizeY, sizeZ) };
  }, [allPrimitives]);

  const cameraDistance = useMemo(() => {
    if (!bounds) return 150;
    return Math.max(30, bounds.size * 1.6);
  }, [bounds]);

  const target = useMemo(() => {
    if (!bounds) return new THREE.Vector3(0, 0, 0);
    return new THREE.Vector3(bounds.center[0], bounds.center[1], bounds.center[2]);
  }, [bounds]);

  const addBox = useCallback(async () => {
    // Place at current scene center
    const revit = threeToRevit(target);

    const newBox: GeometryPrimitive = {
      category: "WebBox",
      centerX: revit.x,
      centerY: revit.y,
      centerZ: revit.z,
      sizeX: boxSize,
      sizeY: boxSize,
      sizeZ: boxSize,
    };

    // Optimistically add to pending boxes immediately
    setPendingBoxes(prev => [...prev, newBox]);

    try {
      await enqueue.mutateAsync({
        projectName: snapshot.projectName,
        type: "ADD_BOXES",
        boxes: [newBox],
      });
    } catch {
      // Remove from pending if failed
      setPendingBoxes(prev => prev.filter(b => b !== newBox));
    }
  }, [target, boxSize, snapshot.projectName, enqueue]);

  const totalCount = count + pendingBoxes.length;

  return (
    <div className="fixed inset-0 bg-slate-950 text-slate-100">
      {/* HUD */}
      <div className="absolute left-4 top-4 z-10 rounded-xl bg-slate-950/70 backdrop-blur border border-slate-800 px-3 py-2 text-sm shadow-lg">
        <div className="font-semibold">RevitSync</div>
        <div className="text-xs text-slate-300">
          Project: <b>{snapshot.projectName}</b> • Primitives: <b>{totalCount}</b>
          {pendingBoxes.length > 0 && <span className="text-purple-400"> ({pendingBoxes.length} pending)</span>}
          {" "}• {new Date(snapshot.timestampUtc).toLocaleTimeString()}
        </div>
        <div className="mt-2 text-[11px] text-slate-400">
          LMB: Orbit • MMB: Pan • Scroll: Zoom • RMB+Mouse: Look • RMB+WASD: Fly
        </div>
      </div>

      {/* Web -> Revit controls */}
      <div className="absolute left-4 bottom-4 z-10 w-[340px] rounded-xl bg-slate-950/70 backdrop-blur border border-slate-800 px-3 py-3 text-sm shadow-lg">
        <div className="font-semibold mb-2">Web → Revit</div>

        <label className="block text-xs text-slate-300 mb-1">Box size (feet)</label>
        <input
          className="w-full rounded-md bg-slate-900 border border-slate-700 px-2 py-1 text-slate-100"
          type="number"
          value={boxSize}
          min={1}
          step={1}
          onChange={(e) => setBoxSize(Number(e.target.value))}
        />

        <button
          className="mt-2 w-full rounded-md bg-blue-600 hover:bg-blue-500 disabled:opacity-50 px-3 py-2 font-semibold"
          onClick={addBox}
          disabled={enqueue.isPending || !bounds}
        >
          {enqueue.isPending ? "Sending…" : "Add Box in Revit (at scene center)"}
        </button>

        {enqueue.isError && (
          <div className="mt-2 text-xs text-red-400">
            {(enqueue.error as Error).message}
          </div>
        )}
        <div className="mt-2 text-[11px] text-slate-400">
          Box appears immediately • Revit creates DirectShape • Re-export to sync
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
          {/* Render snapshot primitives */}
          {snapshot.primitives?.map((primitive, index) => (
            <PrimitiveBox key={`snapshot-${index}`} primitive={primitive} />
          ))}
          {/* Render pending boxes (semi-transparent) */}
          {pendingBoxes.map((primitive, index) => (
            <PrimitiveBox key={`pending-${index}`} primitive={primitive} isPending />
          ))}
          <Environment preset="city" />
        </Suspense>
      </Canvas>
    </div>
  );
}
