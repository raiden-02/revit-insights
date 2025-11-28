import { Canvas } from "@react-three/fiber";
import {
    ContactShadows,
    Environment,
    Effects,
    Lightformer,
    OrbitControls,
} from "@react-three/drei";
import { Suspense, useMemo } from "react";
import type { Props } from "./types";

function Bar({ x, height }: { x: number; height: number }) {
    const h = Math.max(0.1, height);
    return (
        <mesh position={[x, h / 2, 0]} castShadow receiveShadow>
            <boxGeometry args={[0.8, h, 0.8]} />
            <meshPhysicalMaterial color="lightblue" roughness={0.25} metalness={0.8} />
        </mesh>
    );
}

export default function Category3D({ data }: Props) {
    const max = useMemo(() => Math.max(1, ...data.map(d => d.count)), [data]);
    const scaled = useMemo(() => {
        const step = 1.1;
        const offset = ((data.length - 1) * step) / 2;
        return data.map((d, i) => ({
            x: i * step - offset,
            h: (d.count / max) * 6 + 0.1,
        }));
    }, [data, max]);

    if (!data?.length) return <div className="text-slate-500">No Category data available.</div>;

    return (
        <div className="w-full min-w-0 h-[360px]">
            <Canvas
                shadows
                gl={{ logarithmicDepthBuffer: true, antialias: false }}
                dpr={[1, 1.5]}
                camera={{ position: [0, 5, 14], fov: 30 }}
            >
                <color attach="background" args={["#15151a"]} />
                <hemisphereLight intensity={0.6} groundColor="#05050a" />
                <directionalLight position={[6, 8, 4]} intensity={1.3} castShadow />

                <Suspense fallback={null}>
                    {scaled.map((b, i) => (
                        <Bar key={`${b.x}-${i}`} x={b.x} height={b.h} />
                    ))}

                    <mesh
                        scale={4}
                        position={[3, -0.02, -2]}
                        rotation={[-Math.PI / 2, 0, Math.PI / 2.5]}
                        receiveShadow
                    >
                        <ringGeometry args={[0.9, 1, 4, 1]} />
                        <meshStandardMaterial color="#f8fafc" roughness={0.6} metalness={0.2} />
                    </mesh>

                    <mesh
                        scale={4}
                        position={[-3, -0.02, -1]}
                        rotation={[-Math.PI / 2, 0, Math.PI / 2.5]}
                        receiveShadow
                    >
                        <ringGeometry args={[0.9, 1, 3, 1]} />
                        <meshStandardMaterial color="#f97316" roughness={0.5} metalness={0.2} />
                    </mesh>

                    <Environment resolution={512}>
                        <Lightformer intensity={2} rotation-x={Math.PI / 2} position={[0, 4, -9]} scale={[10, 1, 1]} />
                        <Lightformer intensity={2} rotation-x={Math.PI / 2} position={[0, 4, -6]} scale={[10, 1, 1]} />
                        <Lightformer intensity={2} rotation-x={Math.PI / 2} position={[0, 4, -3]} scale={[10, 1, 1]} />
                        <Lightformer intensity={2} rotation-x={Math.PI / 2} position={[0, 4, 0]} scale={[10, 1, 1]} />
                        <Lightformer intensity={2} rotation-x={Math.PI / 2} position={[0, 4, 3]} scale={[10, 1, 1]} />
                        <Lightformer intensity={2} rotation-x={Math.PI / 2} position={[0, 4, 6]} scale={[10, 1, 1]} />
                        <Lightformer intensity={2} rotation-x={Math.PI / 2} position={[0, 4, 9]} scale={[10, 1, 1]} />
                        <Lightformer intensity={2} rotation-y={Math.PI / 2} position={[-50, 2, 0]} scale={[100, 2, 1]} />
                        <Lightformer intensity={2} rotation-y={-Math.PI / 2} position={[50, 2, 0]} scale={[100, 2, 1]} />
                        <Lightformer form="ring" color="red" intensity={8} scale={2} position={[10, 5, 10]} onUpdate={self => self.lookAt(0, 0, 0)} />
                    </Environment>

                    <Effects disableGamma />
                </Suspense>

                <ContactShadows position={[0, -0.05, 0]} opacity={0.6} scale={18} blur={2.5} far={15} />

                <OrbitControls
                    enablePan
                    enableZoom
                />
            </Canvas>
        </div>
    );
}
