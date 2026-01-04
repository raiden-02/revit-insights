import { Canvas, useThree } from "@react-three/fiber";
import { Environment, TransformControls } from "@react-three/drei";
import { Suspense, useMemo, useState, useCallback, useEffect, useRef } from "react";
import * as THREE from "three";
import { ViewCube, type ViewPreset } from "./ViewCube";
import { CameraControls, type SceneBounds } from "./CameraControls";
import { useEnqueueCommand } from "../hooks/useEnqueueCommand";

// Properties Panel - displays Revit element properties when selected
function PropertiesPanel({ primitive, onClose }: { primitive: GeometryPrimitive; onClose: () => void }) {
  const properties = primitive.properties ?? {};
  const hasProperties = Object.keys(properties).length > 0;

  // Group properties by type
  const identityProps = ["Name", "Family", "Type", "Mark"];
  const locationProps = ["Level", "Base Offset", "Top Offset", "Height Offset", "Unconnected Height"];
  const geometryProps = ["Length", "Area", "Volume"];
  const phaseProps = ["Phase Created", "Phase Demolished", "Workset"];

  const renderPropertyGroup = (title: string, keys: string[]) => {
    const entries = keys.filter(k => properties[k]).map(k => [k, properties[k]] as const);
    if (entries.length === 0) return null;
    return (
      <div className="mb-3">
        <div className="text-[10px] uppercase tracking-wider text-slate-500 mb-1">{title}</div>
        {entries.map(([key, value]) => (
          <div key={key} className="flex justify-between py-0.5 border-b border-slate-800">
            <span className="text-slate-400 text-xs">{key}</span>
            <span className="text-slate-100 text-xs font-medium truncate ml-2 max-w-[150px]" title={value}>{value}</span>
          </div>
        ))}
      </div>
    );
  };

  // Any properties not in the above groups
  const otherKeys = Object.keys(properties).filter(
    k => ![...identityProps, ...locationProps, ...geometryProps, ...phaseProps].includes(k)
  );

  return (
    <div className="absolute right-4 bottom-4 z-10 w-[280px] max-h-[60vh] overflow-y-auto rounded-xl bg-slate-950/90 backdrop-blur border border-slate-700 shadow-2xl">
      {/* Header */}
      <div className="sticky top-0 bg-slate-900 px-3 py-2 border-b border-slate-700 flex items-center justify-between">
        <div>
          <div className="text-sm font-semibold text-slate-100">Element Properties</div>
          <div className="text-[11px] text-slate-400">ID: {primitive.elementId}</div>
        </div>
        <button
          onClick={onClose}
          className="text-slate-400 hover:text-slate-100 p-1 rounded hover:bg-slate-700 transition-colors"
          title="Close"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      {/* Content */}
      <div className="px-3 py-2">
        {/* Category Badge */}
        <div className="mb-3">
          <span className={`inline-block px-2 py-0.5 rounded text-[11px] font-medium ${
            primitive.isWebCreated 
              ? "bg-purple-900/50 text-purple-300 border border-purple-700" 
              : "bg-blue-900/50 text-blue-300 border border-blue-700"
          }`}>
            {primitive.isWebCreated ? "Web Created" : primitive.category}
          </span>
        </div>

        {hasProperties ? (
          <>
            {renderPropertyGroup("Identity", identityProps)}
            {renderPropertyGroup("Location", locationProps)}
            {renderPropertyGroup("Geometry", geometryProps)}
            {renderPropertyGroup("Phase & Workset", phaseProps)}
            {otherKeys.length > 0 && renderPropertyGroup("Other", otherKeys)}
          </>
        ) : (
          <div className="text-slate-500 text-xs italic py-4 text-center">
            {primitive.isWebCreated 
              ? "Web-created elements have no Revit properties" 
              : "No properties available"}
          </div>
        )}

        {/* Bounding Box Info */}
        <div className="mt-3 pt-3 border-t border-slate-700">
          <div className="text-[10px] uppercase tracking-wider text-slate-500 mb-1">Bounding Box (feet)</div>
          <div className="grid grid-cols-3 gap-2 text-xs">
            <div className="text-center">
              <div className="text-slate-500">X</div>
              <div className="text-slate-300">{primitive.sizeX.toFixed(2)}</div>
            </div>
            <div className="text-center">
              <div className="text-slate-500">Y</div>
              <div className="text-slate-300">{primitive.sizeY.toFixed(2)}</div>
            </div>
            <div className="text-center">
              <div className="text-slate-500">Z</div>
              <div className="text-slate-300">{primitive.sizeZ.toFixed(2)}</div>
            </div>
          </div>
          <div className="text-[10px] text-slate-500 mt-1 text-center">
            Center: ({primitive.centerX.toFixed(1)}, {primitive.centerY.toFixed(1)}, {primitive.centerZ.toFixed(1)})
          </div>
        </div>
      </div>
    </div>
  );
}

export type GeometryPrimitive = {
  category: string;
  elementId?: string;
  isWebCreated?: boolean;
  color?: string; // color per category
  centerX: number;
  centerY: number;
  centerZ: number;
  sizeX: number;
  sizeY: number;
  sizeZ: number;
  properties?: Record<string, string>;
};

export type GeometrySnapshot = {
  projectName: string;
  timestampUtc: string;
  selectedElementIds?: string[];
  primitives: GeometryPrimitive[];
};

function colorForPrimitive(primitive: GeometryPrimitive): string {
  if (primitive.color) return primitive.color;           // From backend
  if (primitive.isWebCreated) return "#e879f9";          // Fuchsia for pending
  return "#e5e7eb";                                       // Gray fallback (shouldn't happen)
}

function threeToRevit(p: THREE.Vector3) {
  return { x: p.x, y: -p.z, z: p.y };
}

// Box component that reports its mesh via callback
type SelectableBoxProps = {
  primitive: GeometryPrimitive;
  isSelected: boolean;
  isRevitSelected?: boolean; // Selected in Revit (synced from backend)
  isPending?: boolean;
  onSelect: () => void;
  onMeshReady?: (mesh: THREE.Mesh | null) => void;
};

function SelectableBox({ primitive, isSelected, isRevitSelected = false, isPending = false, onSelect, onMeshReady }: SelectableBoxProps) {
  const meshRef = useRef<THREE.Mesh>(null);
  const color = colorForPrimitive(primitive);
  const position: [number, number, number] = [primitive.centerX, primitive.centerZ, -primitive.centerY];
  const scale: [number, number, number] = [primitive.sizeX, primitive.sizeZ, primitive.sizeY];

  // Report mesh to parent when selected
  useEffect(() => {
    if (isSelected && onMeshReady) {
      onMeshReady(meshRef.current);
    }
  }, [isSelected, onMeshReady]);

  // Determine visual state: web selection (yellow) > revit selection (cyan) > normal
  const displayColor = isSelected ? "#facc15" : isRevitSelected ? "#22d3d3" : color;
  const outlineColor = isSelected ? "#facc15" : "#22d3d3";
  const showOutline = isSelected || isRevitSelected;

  return (
    <mesh
      ref={meshRef}
      position={position}
      scale={scale}
      castShadow
      receiveShadow
      onClick={(e) => { e.stopPropagation(); onSelect(); }}
    >
      <boxGeometry args={[1, 1, 1]} />
      <meshStandardMaterial
        color={displayColor}
        metalness={0.1}
        roughness={0.7}
        transparent={isPending || isSelected || isRevitSelected}
        opacity={isPending ? 0.6 : (isSelected || isRevitSelected) ? 0.9 : 1}
      />
      {showOutline && (
        <lineSegments>
          <edgesGeometry args={[new THREE.BoxGeometry(1, 1, 1)]} />
          <lineBasicMaterial color={outlineColor} linewidth={2} />
        </lineSegments>
      )}
    </mesh>
  );
}

// Placement helper
function PlacementHelper({ enabled, onPlace, boxSize, groundLevel }: { enabled: boolean; onPlace: (p: THREE.Vector3) => void; boxSize: number; groundLevel: number }) {
  const { camera, gl, raycaster, pointer } = useThree();
  const groundPlane = useMemo(() => new THREE.Plane(new THREE.Vector3(0, 1, 0), -groundLevel), [groundLevel]);

  useEffect(() => {
    if (!enabled) return;
    const handleClick = (e: MouseEvent) => {
      if (e.button !== 0) return;
      raycaster.setFromCamera(pointer, camera);
      const intersection = new THREE.Vector3();
      if (raycaster.ray.intersectPlane(groundPlane, intersection)) {
        intersection.y = groundLevel + boxSize / 2;
        onPlace(intersection);
      } else {
        const forward = new THREE.Vector3(0, 0, -1).applyQuaternion(camera.quaternion);
        const point = camera.position.clone().add(forward.multiplyScalar(50));
        point.y = groundLevel + boxSize / 2;
        onPlace(point);
      }
    };
    gl.domElement.addEventListener("click", handleClick);
    return () => gl.domElement.removeEventListener("click", handleClick);
  }, [enabled, camera, gl, raycaster, pointer, groundPlane, groundLevel, boxSize, onPlace]);

  return null;
}

// TransformControls wrapper that disables orbit while dragging
function DraggableTransform({ 
  mesh, 
  onDragStart, 
  onDragEnd 
}: { 
  mesh: THREE.Mesh; 
  onDragStart: () => void; 
  onDragEnd: (position: THREE.Vector3) => void;
}) {
  const transformRef = useRef<any>(null);

  useEffect(() => {
    const controls = transformRef.current;
    if (!controls) return;

    const handleChange = (event: { value: boolean }) => {
      if (event.value) {
        onDragStart();
      } else {
        onDragEnd(mesh.position.clone());
      }
    };

    controls.addEventListener('dragging-changed', handleChange);
    return () => controls.removeEventListener('dragging-changed', handleChange);
  }, [mesh, onDragStart, onDragEnd]);

  return (
    <TransformControls
      ref={transformRef}
      object={mesh}
      mode="translate"
      size={0.8}
    />
  );
}

export function LiveGeometryView({ snapshot }: { snapshot: GeometrySnapshot }) {
  const count = snapshot.primitives?.length ?? 0;
  const [viewPreset, setViewPreset] = useState<ViewPreset>("iso");
  const [presetNonce, setPresetNonce] = useState(0);

  const enqueue = useEnqueueCommand();
  const [boxSize, setBoxSize] = useState(10);
  const [placementMode, setPlacementMode] = useState(false);
  const [isDragging, setIsDragging] = useState(false);

  const [selectedElementId, setSelectedElementId] = useState<string | null>(null);
  const [selectedMesh, setSelectedMesh] = useState<THREE.Mesh | null>(null);
  const [pendingBoxes, setPendingBoxes] = useState<GeometryPrimitive[]>([]);
  const lastTimestampRef = useRef(snapshot.timestampUtc);

  // Clear on new snapshot
  useEffect(() => {
    if (snapshot.timestampUtc !== lastTimestampRef.current) {
      lastTimestampRef.current = snapshot.timestampUtc;
      setPendingBoxes([]);
      setSelectedElementId(null);
      setSelectedMesh(null);
    }
  }, [snapshot.timestampUtc]);

  // Clear mesh when deselected
  useEffect(() => {
    if (!selectedElementId) {
      setSelectedMesh(null);
    }
  }, [selectedElementId]);

  const allPrimitives = useMemo(() => [...(snapshot.primitives ?? []), ...pendingBoxes], [snapshot.primitives, pendingBoxes]);

  const bounds: SceneBounds = useMemo(() => {
    if (!allPrimitives.length) return null;
    let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity, minZ = Infinity, maxZ = -Infinity;
    for (const p of allPrimitives) {
      const cx = p.centerX, cy = p.centerZ, cz = -p.centerY;
      const sx = p.sizeX, sy = p.sizeZ, sz = p.sizeY;
      minX = Math.min(minX, cx - sx / 2); maxX = Math.max(maxX, cx + sx / 2);
      minY = Math.min(minY, cy - sy / 2); maxY = Math.max(maxY, cy + sy / 2);
      minZ = Math.min(minZ, cz - sz / 2); maxZ = Math.max(maxZ, cz + sz / 2);
    }
    return { center: [(minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2] as [number, number, number], size: Math.max(maxX - minX, maxY - minY, maxZ - minZ) };
  }, [allPrimitives]);

  const cameraDistance = useMemo(() => (bounds ? Math.max(30, bounds.size * 1.6) : 150), [bounds]);
  const target = useMemo(() => (bounds ? new THREE.Vector3(...bounds.center) : new THREE.Vector3()), [bounds]);

  const groundLevel = useMemo(() => {
    if (!allPrimitives.length) return 0;
    let minY = Infinity;
    for (const p of allPrimitives) minY = Math.min(minY, p.centerZ - p.sizeZ / 2);
    return minY === Infinity ? 0 : minY;
  }, [allPrimitives]);

  const selectedPrimitive = useMemo(() => {
    if (!selectedElementId) return null;
    return snapshot.primitives?.find((p) => p.elementId === selectedElementId) ?? null;
  }, [selectedElementId, snapshot.primitives]);

  const addBoxAtPoint = useCallback(async (threePoint: THREE.Vector3) => {
    const revit = threeToRevit(threePoint);
    const newBox: GeometryPrimitive = { category: "WebBox", isWebCreated: true, centerX: revit.x, centerY: revit.y, centerZ: revit.z, sizeX: boxSize, sizeY: boxSize, sizeZ: boxSize };
    setPendingBoxes((prev) => [...prev, newBox]);
    setPlacementMode(false);
    try {
      await enqueue.mutateAsync({ projectName: snapshot.projectName, type: "ADD_BOXES", boxes: [newBox] });
    } catch {
      setPendingBoxes((prev) => prev.filter((b) => b !== newBox));
    }
  }, [boxSize, snapshot.projectName, enqueue]);

  const deleteSelected = useCallback(async () => {
    if (!selectedElementId) return;
    try {
      await enqueue.mutateAsync({ projectName: snapshot.projectName, type: "DELETE_ELEMENTS", elementIds: [selectedElementId] });
      setSelectedElementId(null);
      setSelectedMesh(null);
    } catch {}
  }, [selectedElementId, snapshot.projectName, enqueue]);

  const moveElement = useCallback(async (elementId: string, newPosition: THREE.Vector3) => {
    const revit = threeToRevit(newPosition);
    try {
      await enqueue.mutateAsync({ projectName: snapshot.projectName, type: "MOVE_ELEMENT", targetElementId: elementId, newCenterX: revit.x, newCenterY: revit.y, newCenterZ: revit.z });
      setSelectedElementId(null);
      setSelectedMesh(null);
    } catch {}
  }, [snapshot.projectName, enqueue]);

  // Send selection to Revit (Web → Revit selection sync)
  const selectInRevit = useCallback(async (elementIds: string[]) => {
    try {
      await enqueue.mutateAsync({ projectName: snapshot.projectName, type: "SELECT_ELEMENTS", elementIds });
    } catch {}
  }, [snapshot.projectName, enqueue]);

  // Revit selection state from snapshot (Revit → Web selection sync)
  const revitSelectedIds = useMemo(() => new Set(snapshot.selectedElementIds ?? []), [snapshot.selectedElementIds]);

  const handleDragStart = useCallback(() => setIsDragging(true), []);
  const handleDragEnd = useCallback((position: THREE.Vector3) => {
    setIsDragging(false);
    if (selectedElementId) {
      moveElement(selectedElementId, position);
    }
  }, [selectedElementId, moveElement]);

  const totalCount = count + pendingBoxes.length;
  const canMove = selectedPrimitive?.isWebCreated && selectedMesh && !placementMode;

  return (
    <div className="fixed inset-0 bg-slate-950 text-slate-100">
      {/* HUD */}
      <div className="absolute left-4 top-4 z-10 rounded-xl bg-slate-950/70 backdrop-blur border border-slate-800 px-3 py-2 text-sm shadow-lg">
        <div className="font-semibold">RevitSync</div>
        <div className="text-xs text-slate-300">
          Project: <b>{snapshot.projectName}</b> • Primitives: <b>{totalCount}</b>
          {pendingBoxes.length > 0 && <span className="text-purple-400"> ({pendingBoxes.length} pending)</span>}
          {revitSelectedIds.size > 0 && <span className="text-cyan-400"> • Revit: {revitSelectedIds.size} selected</span>}
          {" "}• {new Date(snapshot.timestampUtc).toLocaleTimeString()}
        </div>
        <div className="mt-2 text-[11px] text-slate-400">LMB: Orbit • MMB: Pan • Scroll: Zoom • RMB+Mouse: Look • RMB+WASD: Fly</div>
        {placementMode && <div className="mt-2 text-xs text-yellow-400 font-semibold">Click anywhere to place a box</div>}
        {isDragging && <div className="mt-2 text-xs text-green-400 font-semibold">Dragging... release to move</div>}
        
        {/* Category Legend */}
        <div className="mt-2 pt-2 border-t border-slate-700">
          <div className="text-[10px] uppercase tracking-wider text-slate-500 mb-1">Categories</div>
          <div className="flex flex-wrap gap-x-3 gap-y-1 text-[11px]">
            <span><span className="inline-block w-2 h-2 rounded-sm mr-1" style={{backgroundColor: "#4ade80"}}></span>Walls</span>
            <span><span className="inline-block w-2 h-2 rounded-sm mr-1" style={{backgroundColor: "#f97316"}}></span>Roofs</span>
            <span><span className="inline-block w-2 h-2 rounded-sm mr-1" style={{backgroundColor: "#94a3b8"}}></span>Floors</span>
            <span><span className="inline-block w-2 h-2 rounded-sm mr-1" style={{backgroundColor: "#60a5fa"}}></span>Columns</span>
            <span><span className="inline-block w-2 h-2 rounded-sm mr-1" style={{backgroundColor: "#a78bfa"}}></span>Framing</span>
            <span><span className="inline-block w-2 h-2 rounded-sm mr-1" style={{backgroundColor: "#78716c"}}></span>Foundation</span>
            <span><span className="inline-block w-2 h-2 rounded-sm mr-1" style={{backgroundColor: "#22d3ee"}}></span>Windows</span>
            <span><span className="inline-block w-2 h-2 rounded-sm mr-1" style={{backgroundColor: "#fbbf24"}}></span>Doors</span>
            <span><span className="inline-block w-2 h-2 rounded-sm mr-1" style={{backgroundColor: "#67e8f9"}}></span>Panels</span>
            <span><span className="inline-block w-2 h-2 rounded-sm mr-1" style={{backgroundColor: "#38bdf8"}}></span>Mullions</span>
            <span><span className="inline-block w-2 h-2 rounded-sm mr-1" style={{backgroundColor: "#fb923c"}}></span>Stairs</span>
            <span><span className="inline-block w-2 h-2 rounded-sm mr-1" style={{backgroundColor: "#e879f9"}}></span>Generic</span>
          </div>
        </div>
      </div>

      {/* Controls */}
      <div className="absolute left-4 bottom-4 z-10 w-[340px] rounded-xl bg-slate-950/70 backdrop-blur border border-slate-800 px-3 py-3 text-sm shadow-lg">
        <div className="font-semibold mb-2">Web → Revit</div>
        <label className="block text-xs text-slate-300 mb-1">Box size (feet)</label>
        <input className="w-full rounded-md bg-slate-900 border border-slate-700 px-2 py-1 text-slate-100" type="number" value={boxSize} min={1} step={1} onChange={(e) => setBoxSize(Number(e.target.value))} />
        <button
          className={`w-full mt-2 rounded-md px-3 py-2 font-semibold ${placementMode ? "bg-yellow-600 hover:bg-yellow-500" : "bg-blue-600 hover:bg-blue-500"} disabled:opacity-50`}
          onClick={() => setPlacementMode(!placementMode)}
          disabled={enqueue.isPending || !bounds}
        >
          {placementMode ? "Cancel Placement" : "Click to Place"}
        </button>
        {enqueue.isError && <div className="mt-2 text-xs text-red-400">{(enqueue.error as Error).message}</div>}
        {selectedPrimitive && (
          <div className="mt-3 pt-3 border-t border-slate-700">
            <div className="text-xs text-slate-300 mb-2"><b>Selected:</b> {selectedPrimitive.category} (ID: {selectedPrimitive.elementId})</div>
            
            {/* Select in Revit button - syncs selection to Revit */}
            <button 
              className="w-full rounded-md bg-cyan-600 hover:bg-cyan-500 disabled:opacity-50 px-3 py-2 font-semibold mb-2" 
              onClick={() => selectedPrimitive.elementId && selectInRevit([selectedPrimitive.elementId])} 
              disabled={enqueue.isPending}
            >
              Select in Revit
            </button>
            
            {selectedPrimitive.isWebCreated ? (
              <>
                <div className="text-[11px] text-green-400 mb-2">Drag arrows to move • Click delete to remove</div>
                <button className="w-full rounded-md bg-red-600 hover:bg-red-500 disabled:opacity-50 px-3 py-2 font-semibold" onClick={deleteSelected} disabled={enqueue.isPending}>Delete from Revit</button>
              </>
            ) : (
              <div className="text-[11px] text-slate-500 italic">Only web-created elements can be moved/deleted</div>
            )}
          </div>
        )}
        <div className="mt-2 text-[11px] text-slate-400">Click to select • Drag arrows to move • Re-export to sync</div>
      </div>

      <div className="absolute right-4 top-4 z-10">
        <ViewCube onSelect={(p) => { setViewPreset(p); setPresetNonce((n) => n + 1); }} />
      </div>

      {/* Properties Panel - shows when element is selected */}
      {selectedPrimitive && (
        <PropertiesPanel 
          primitive={selectedPrimitive} 
          onClose={() => { setSelectedElementId(null); setSelectedMesh(null); }} 
        />
      )}

      <Canvas
        shadows
        style={{ width: "100%", height: "100%", cursor: placementMode ? "crosshair" : "auto" }}
        camera={{ position: [0, 80, cameraDistance], fov: 55, near: 0.1, far: 100000 }}
        onPointerMissed={() => { if (!isDragging) { setSelectedElementId(null); setSelectedMesh(null); } }}
      >
        <color attach="background" args={["#020617"]} />
        <hemisphereLight intensity={0.6} groundColor="#020617" />
        <directionalLight position={[50, 80, 40]} intensity={1.2} castShadow />

        <CameraControls target={target} bounds={bounds} cameraDistance={cameraDistance} viewPreset={viewPreset} presetNonce={presetNonce} />
        <PlacementHelper enabled={placementMode} onPlace={addBoxAtPoint} boxSize={boxSize} groundLevel={groundLevel} />
        {placementMode && <gridHelper args={[1000, 100, "#334155", "#1e293b"]} position={[target.x, groundLevel, target.z]} />}

        <Suspense fallback={null}>
          {snapshot.primitives?.map((primitive, index) => {
            const isSelected = primitive.elementId === selectedElementId;
            const isRevitSelected = primitive.elementId ? revitSelectedIds.has(primitive.elementId) : false;
            const isMovable = isSelected && primitive.isWebCreated;

            return (
              <SelectableBox
                key={`box-${primitive.elementId || index}`}
                primitive={primitive}
                isSelected={isSelected}
                isRevitSelected={isRevitSelected}
                onSelect={() => {
                  if (!placementMode && primitive.elementId) {
                    if (primitive.elementId === selectedElementId) {
                      setSelectedElementId(null);
                      setSelectedMesh(null);
                    } else {
                      setSelectedElementId(primitive.elementId);
                    }
                  }
                }}
                onMeshReady={isMovable ? setSelectedMesh : undefined}
              />
            );
          })}

          {pendingBoxes.map((primitive, index) => (
            <SelectableBox key={`pending-${index}`} primitive={primitive} isSelected={false} isPending onSelect={() => {}} />
          ))}

          {/* TransformControls for selected web-created element */}
          {canMove && selectedMesh && (
            <DraggableTransform 
              mesh={selectedMesh} 
              onDragStart={handleDragStart} 
              onDragEnd={handleDragEnd} 
            />
          )}

          <Environment preset="city" />
        </Suspense>
      </Canvas>
    </div>
  );
}
