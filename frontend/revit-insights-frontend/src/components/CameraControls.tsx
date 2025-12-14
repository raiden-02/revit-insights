// Unreal Engine like camera controls

import { useFrame, useThree } from "@react-three/fiber";
import { OrbitControls } from "@react-three/drei";
import { useEffect, useRef } from "react";
import * as THREE from "three";
import type { ViewPreset } from "./ViewCube";

export type SceneBounds = {
    center: [number, number, number];
    size: number;
} | null;

export type CameraControlsProps = {
    target: THREE.Vector3;
    bounds: SceneBounds;
    cameraDistance: number;
    viewPreset: ViewPreset;
    presetNonce: number;
};

function getPresetDir(preset: ViewPreset): THREE.Vector3 {
    switch (preset) {
        case "top": return new THREE.Vector3(0, 1, 0);
        case "bottom": return new THREE.Vector3(0, -1, 0);
        case "front": return new THREE.Vector3(0, 0, 1);
        case "back": return new THREE.Vector3(0, 0, -1);
        case "left": return new THREE.Vector3(-1, 0, 0);
        case "right": return new THREE.Vector3(1, 0, 0);
        case "iso": default: return new THREE.Vector3(1, 1, 1).normalize();
    }
}

export function CameraControls({ target, bounds, cameraDistance, viewPreset, presetNonce }: CameraControlsProps) {
    const orbitRef = useRef<any>(null);
    const { camera, gl } = useThree();

    const rmbDown = useRef(false);
    const keysRef = useRef<Record<string, boolean>>({});
    const euler = useRef(new THREE.Euler(0, 0, 0, "YXZ"));

    // Persistent rig state — saves camera position/rotation between mode switches
    const rig = useRef({
        target: new THREE.Vector3(),
        pos: new THREE.Vector3(),
        quat: new THREE.Quaternion(),
    });

    // Initialize camera on first bounds load
    const hasInitialized = useRef(false);
    useEffect(() => {
        if (!bounds || hasInitialized.current) return;
        hasInitialized.current = true;

        const center = new THREE.Vector3(bounds.center[0], bounds.center[1], bounds.center[2]);
        const dir = getPresetDir(viewPreset);
        const pos = center.clone().add(dir.multiplyScalar(cameraDistance));

        camera.position.copy(pos);
        camera.lookAt(center);

        rig.current.pos.copy(camera.position);
        rig.current.quat.copy(camera.quaternion);
        rig.current.target.copy(center);
        euler.current.setFromQuaternion(rig.current.quat);

        if (orbitRef.current) {
            orbitRef.current.target.copy(center);
            orbitRef.current.update();
            orbitRef.current.saveState?.();
        }
    }, [bounds, camera, cameraDistance, viewPreset]);

    // Update orbit target when target prop changes (but don't move camera)
    useEffect(() => {
        rig.current.target.copy(target);
        if (orbitRef.current) {
            orbitRef.current.target.copy(rig.current.target);
            orbitRef.current.update();
        }
    }, [target]);

    // Keyboard events for WASD
    useEffect(() => {
        const onKeyDown = (e: KeyboardEvent) => { keysRef.current[e.code] = true; };
        const onKeyUp = (e: KeyboardEvent) => { keysRef.current[e.code] = false; };
        window.addEventListener("keydown", onKeyDown);
        window.addEventListener("keyup", onKeyUp);
        return () => {
            window.removeEventListener("keydown", onKeyDown);
            window.removeEventListener("keyup", onKeyUp);
        };
    }, []);

    // RMB fly mode + pointer lock with proper state sync
    useEffect(() => {
        const dom = gl.domElement;

        const onContextMenu = (e: MouseEvent) => e.preventDefault();

        const enterFly = () => {
            if (rmbDown.current) return;
            rmbDown.current = true;

            // Disable orbit controls
            if (orbitRef.current) orbitRef.current.enabled = false;

            // Capture current camera into rig state
            rig.current.pos.copy(camera.position);
            rig.current.quat.copy(camera.quaternion);
            euler.current.setFromQuaternion(rig.current.quat);

            // Pointer lock for smooth mouse look
            dom.requestPointerLock?.();
        };

        const exitFly = () => {
            if (!rmbDown.current) return;
            rmbDown.current = false;

            // Exit pointer lock
            if (document.pointerLockElement === dom) document.exitPointerLock?.();

            // Commit rig state back to camera
            camera.position.copy(rig.current.pos);
            camera.quaternion.copy(rig.current.quat);

            // Commit rig state to OrbitControls
            const controls = orbitRef.current;
            if (controls) {
                // Set orbit target in front of camera based on current look direction
                // This ensures OrbitControls' implied lookAt(target) matches our fly orientation
                const forward = new THREE.Vector3(0, 0, -1).applyQuaternion(rig.current.quat).normalize();
                const pivotDist = Math.max(5, cameraDistance * 0.35);
                rig.current.target.copy(rig.current.pos).addScaledVector(forward, pivotDist);

                controls.target.copy(rig.current.target);

                // IMPORTANT: update BEFORE enabling so internal caches are rebuilt
                controls.update();

                // Save state so reset() is consistent
                controls.saveState?.();

                controls.enabled = true;
            }
        };

        const onPointerDown = (e: PointerEvent) => {
            if (e.button === 2) enterFly();
        };

        const onPointerUp = (e: PointerEvent) => {
            if (e.button === 2) exitFly();
        };

        const onMouseMove = (e: MouseEvent) => {
            if (!rmbDown.current) return;

            const sensitivity = 0.002;
            euler.current.y -= e.movementX * sensitivity;
            euler.current.x -= e.movementY * sensitivity;

            // Clamp pitch to avoid flipping
            const limit = Math.PI / 2 - 0.01;
            euler.current.x = Math.max(-limit, Math.min(limit, euler.current.x));

            rig.current.quat.setFromEuler(euler.current);
            camera.quaternion.copy(rig.current.quat);
        };

        // Handle pointer lock being lost unexpectedly
        const onPointerLockChange = () => {
            if (rmbDown.current && document.pointerLockElement !== dom) {
                exitFly();
            }
        };

        // Handle window losing focus
        const onBlur = () => exitFly();

        dom.addEventListener("contextmenu", onContextMenu);
        dom.addEventListener("pointerdown", onPointerDown);
        window.addEventListener("pointerup", onPointerUp);
        window.addEventListener("mousemove", onMouseMove);
        document.addEventListener("pointerlockchange", onPointerLockChange);
        window.addEventListener("blur", onBlur);
        document.addEventListener("visibilitychange", onBlur);

        return () => {
            dom.removeEventListener("contextmenu", onContextMenu);
            dom.removeEventListener("pointerdown", onPointerDown);
            window.removeEventListener("pointerup", onPointerUp);
            window.removeEventListener("mousemove", onMouseMove);
            document.removeEventListener("pointerlockchange", onPointerLockChange);
            window.removeEventListener("blur", onBlur);
            document.removeEventListener("visibilitychange", onBlur);
        };
    }, [gl, camera, cameraDistance]);

    // Fly movement (true camera-forward using quaternion)
    useFrame((_state, dt) => {
        if (!rmbDown.current) return;

        // Ignore if user is typing in an input
        const el = document.activeElement as HTMLElement | null;
        if (el && (el.tagName === "INPUT" || el.tagName === "TEXTAREA")) return;

        const speedBase = Math.max(15, cameraDistance * 0.4);
        const speed = (keysRef.current["ShiftLeft"] || keysRef.current["ShiftRight"])
            ? speedBase * 2.5
            : speedBase;
        const dist = speed * dt;

        // True camera-forward/right from quaternion (not flattened)
        const forward = new THREE.Vector3(0, 0, -1).applyQuaternion(rig.current.quat).normalize();
        const right = new THREE.Vector3(1, 0, 0).applyQuaternion(rig.current.quat).normalize();
        const up = new THREE.Vector3(0, 1, 0); // World up for Q/E

        const move = new THREE.Vector3();

        if (keysRef.current["KeyW"]) move.addScaledVector(forward, dist);
        if (keysRef.current["KeyS"]) move.addScaledVector(forward, -dist);
        if (keysRef.current["KeyD"]) move.addScaledVector(right, dist);
        if (keysRef.current["KeyA"]) move.addScaledVector(right, -dist);
        if (keysRef.current["KeyE"] || keysRef.current["Space"]) move.addScaledVector(up, dist);
        if (keysRef.current["KeyQ"] || keysRef.current["ControlLeft"] || keysRef.current["ControlRight"])
            move.addScaledVector(up, -dist);

        if (move.lengthSq() > 0) {
            rig.current.pos.add(move);
            camera.position.copy(rig.current.pos);
        }
    });

    // Snap camera for view presets — only fires when user clicks ViewCube (presetNonce changes)
    useEffect(() => {
        if (!bounds) return;
        if (presetNonce === 0) return; // Skip initial mount
        
        const center = new THREE.Vector3(bounds.center[0], bounds.center[1], bounds.center[2]);
        const dir = getPresetDir(viewPreset);
        const pos = center.clone().add(dir.multiplyScalar(cameraDistance));

        camera.position.copy(pos);
        camera.lookAt(center);

        // Sync rig state
        rig.current.pos.copy(camera.position);
        rig.current.quat.copy(camera.quaternion);
        rig.current.target.copy(center);
        euler.current.setFromQuaternion(rig.current.quat);

        if (orbitRef.current) {
            orbitRef.current.target.copy(center);
            orbitRef.current.update?.();
            orbitRef.current.saveState?.();
        }
    }, [presetNonce, bounds, cameraDistance, viewPreset, camera]);

    return (
        <OrbitControls
            ref={orbitRef}
            mouseButtons={{
                LEFT: THREE.MOUSE.ROTATE,
                MIDDLE: THREE.MOUSE.PAN,
                RIGHT: undefined, // RMB is fly-look
            }}
            enablePan
            enableZoom
            enableRotate
            makeDefault
        />
    );
}

